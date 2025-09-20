// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Atomic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.Util;
using System;
using System.Diagnostics;

namespace ILGPUC.IR.BasicBlockValues;

/// <summary>
/// Represents flags of an atomic operation.
/// </summary>
[Flags]
enum AtomicFlags
{
    /// <summary>
    /// No special flags (default).
    /// </summary>
    None = 0,

    /// <summary>
    /// The operation has unsigned semantics.
    /// </summary>
    Unsigned = 1,
}

/// <summary>
/// Represents a general atomic value.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
/// <param name="flags">The operation flags.</param>
abstract class AtomicValue(
    in BasicBlockValueInitializer initializer,
    TypeValue type,
    AtomicFlags flags) : MemoryValue(initializer, type)
{
    /// <summary>
    /// Returns the target view.
    /// </summary>
    public Value Target => GetValue<Value>(0);

    /// <summary>
    /// Returns the target address space this atomic operates on.
    /// </summary>
    public MemoryAddressSpace TargetAddressSpace =>
        Target.GetTypeAs<AddressSpaceType>().AddressSpace;

    /// <summary>
    /// Returns the target value.
    /// </summary>
    public Value Value => GetValue<Value>(1);

    /// <summary>
    /// Returns the operation flags.
    /// </summary>
    public AtomicFlags Flags { get; } = flags;

    /// <summary>
    /// Returns the associated arithmetic basic value type.
    /// </summary>
    public ArithmeticBasicValueType ArithmeticBasicValueType =>
        BasicValueType.GetArithmeticBasicValueType(IsUnsigned);

    /// <summary>
    /// Returns true if the operation has enabled unsigned semantics.
    /// </summary>
    public bool IsUnsigned => (Flags & AtomicFlags.Unsigned) ==
        AtomicFlags.Unsigned;
}

/// <summary>
/// Represents a generic atomic operation.
/// </summary>
sealed partial class GenericAtomic : AtomicValue
{
    /// <summary>
    /// Constructs a new generic atomic operation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="target">The target.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="kind">The operation kind.</param>
    /// <param name="flags">The operation flags.</param>
    public GenericAtomic(
        in BasicBlockValueInitializer initializer,
        Value target,
        Value value,
        GenericAtomicKind kind,
        AtomicFlags flags)
        : base(initializer, value.Type, flags)
    {
        Debug.Assert(value.Type.Equals(target.GetTypeAs<PointerType>().ElementType));

        Kind = kind;
        Seal(target, value);
    }

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateAtomic(
            Location,
            rewriter.Rewrite(Target),
            rewriter.Rewrite(Value),
            Kind,
            Flags);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "atomic" + Kind.ToString();

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => $"{Target}, {Value} [{Flags}]";
}

/// <summary>
/// Represents an atomic compare-and-swap operation.
/// </summary>
sealed partial class AtomicCAS : AtomicValue
{
    /// <summary>
    /// Constructs a new atomic compare-and-swap operation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="target">The target.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="compareValue">The comparison value.</param>
    /// <param name="flags">The operation flags.</param>
    public AtomicCAS(
        in BasicBlockValueInitializer initializer,
        Value target,
        Value value,
        Value compareValue,
        AtomicFlags flags)
        : base(initializer, value.Type, flags)
    {
        Debug.Assert(value.Type.Equals(target.GetTypeAs<PointerType>().ElementType));
        Debug.Assert(value.Type.Equals(compareValue.Type));

        // Seal target at index 0 (matches AtomicValue.Target), value at 1,
        // compareValue at 2 (matches Compare accessor below). Previously
        // target was not sealed, leaving Target / Compare reading wrong
        // slots — a latent bug surfaced by LowerCustomAtomic.
        Seal(target, value, compareValue);
    }

    /// <summary>
    /// Returns the comparison value.
    /// </summary>
    public Value Compare => GetValue<Value>(2);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateAtomicCAS(
            Location,
            rewriter.Rewrite(Target),
            rewriter.Rewrite(Value),
            rewriter.Rewrite(Compare),
            Flags);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "atomicCAS";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => $"{Target}, {Value}, {Compare}";
}

/// <summary>
/// Represents a custom atomic operation driven by a user-provided binary
/// operation lambda. The operation is the IR <see cref="Method"/> corresponding
/// to the user's <c>Func&lt;T,T,T&gt;</c> delegate passed to
/// <see cref="ILGPU.Atomic.MakeAtomic{T}"/>. The lowering pass
/// <see cref="IR.Transformations.LowerCustomAtomic"/> expands this into an
/// explicit CAS loop: <c>load → call op → AtomicCAS → compare → branch</c>.
/// </summary>
sealed partial class CustomAtomic : AtomicValue
{
    /// <summary>
    /// Constructs a new custom atomic operation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="target">The pointer to the atomic target.</param>
    /// <param name="value">The value passed to the operation lambda.</param>
    /// <param name="operation">
    /// The IR method for the user's binary operation.
    /// </param>
    /// <param name="flags">The operation flags.</param>
    public CustomAtomic(
        in BasicBlockValueInitializer initializer,
        Value target,
        Value value,
        Value operation,
        AtomicFlags flags)
        : base(initializer, value.Type, flags)
    {
        Debug.Assert(value.Type.Equals(target.GetTypeAs<PointerType>().ElementType));

        Seal(target, value, operation);
    }

    /// <summary>
    /// The IR method representing the user's binary operation lambda
    /// (<c>Func&lt;T,T,T&gt;</c>). Invoked once per CAS retry in the lowered
    /// loop body.
    /// </summary>
    public Value Operation => GetValue<Value>(2);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateCustomAtomic(
            Location,
            rewriter.Rewrite(Target),
            rewriter.Rewrite(Value),
            rewriter.Rewrite(Operation),
            Flags);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "atomicCustom";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{Target}, {Value}, {Operation} [{Flags}]";
}
