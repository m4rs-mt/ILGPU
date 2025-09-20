// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Memory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.IR.BasicBlockValues;

/// <summary>
/// Represents an abstract value operating on memory.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class MemoryValue(in BasicBlockValueInitializer initializer, TypeValue type) :
    BasicBlockValue(initializer, type);

/// <summary>
/// Represents an allocation operation on the stack.
/// </summary>
sealed partial class Alloca : MemoryValue, IAllocationValue
{
    /// <summary>
    /// Constructs a new alloc node.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="arrayLengthValue">The array length value to allocate.</param>
    /// <param name="arrayLength">The array length to allocate.</param>
    /// <param name="allocType">The allocation type.</param>
    internal Alloca(
        in BasicBlockValueInitializer initializer,
        Value? arrayLengthValue,
        PrimitiveValueBox? arrayLength,
        TypeValue allocType)
        : base(
            initializer,
            initializer.ModuleBuilder.CreatePointerType(
                allocType,
                MemoryAddressSpace.Local))
    {
        ArrayLength = arrayLength;

        if (!arrayLength.HasValue)
            Seal(arrayLengthValue.AsNotNull());
        else
            Seal();
    }

    /// <summary>
    /// Returns the allocation type.
    /// </summary>
    public TypeValue AllocType => GetTypeAs<PointerType>().ElementType;

    /// <summary>
    /// Returns the address space of this allocation.
    /// </summary>
    public MemoryAddressSpace AddressSpace => MemoryAddressSpace.Local;

    /// <summary>
    /// Returns the array length value (if applicable).
    /// </summary>
    public Value<Method>? ArrayLengthValue =>
        !ArrayLength.HasValue ? GetValue<Value<Method>>(0) : null;

    /// <summary>
    /// Returns the current <see cref="ArrayLengthValue"/>.
    /// </summary>
    Value? IAllocationValue.ArrayLengthValue => ArrayLengthValue;

    /// <summary>
    /// Returns the underlying array length (if any).
    /// </summary>
    public PrimitiveValueBox? ArrayLength { get; }

    /// <inheritdoc cref="IBasicBlockValue.Rewrite{TRewriter}(in TRewriter)"/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        if (this.IsStaticAllocation(out var arrayLength))
        {
            return rewriter.Builder.CreateAlloca(
                Location,
                rewriter.RewriteAs<TypeValue>(AllocType),
                arrayLength);
        }
        else
        {
            return rewriter.Builder.CreateAlloca(
                Location,
                rewriter.RewriteAs<TypeValue>(AllocType),
                rewriter.RewriteAs<Value<Method>>(ArrayLengthValue.AsNotNull()));
        }
    }

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "alloc";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        this.IsStaticAllocation(out var arrayLength)
        ? $"{Type} [{arrayLength}]"
        : $"{Type} [{ArrayLengthValue.AsNotNull().ToReferenceString()}]";
}

/// <summary>
/// Represents a memory barrier that hinders reordering of memory operations with side
/// effects.
/// </summary>
sealed partial class MemoryBarrier : MemoryValue
{
    /// <summary>
    /// Constructs a new memory barrier.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="kind">The barrier kind.</param>
    public MemoryBarrier(
        in BasicBlockValueInitializer initializer,
        MemoryBarrierKind kind)
        : base(initializer, initializer.ModuleBuilder.VoidType)
    {
        Kind = kind;

        Seal();
    }

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateMemoryBarrier(Location, Kind);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "memBarrier";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => Kind.ToString();
}

/// <summary>
/// Represents a load operation with side effects.
/// </summary>
sealed partial class Load : MemoryValue
{
    /// <summary>
    /// Constructs a new load operation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="source">The source view.</param>
    public Load(in BasicBlockValueInitializer initializer, Value source)
        : base(initializer, source.GetTypeAs<AddressSpaceType>().ElementType)
    {
        Seal(source);
    }

    /// <summary>
    /// Returns the source view.
    /// </summary>
    public Value Source => GetValue<Value>(0);

    /// <summary>
    /// Returns the source address space this load reads from.
    /// </summary>
    public MemoryAddressSpace SourceAddressSpace =>
        Source.GetTypeAs<AddressSpaceType>().AddressSpace;

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateLoad(Location, rewriter.Rewrite(Source));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "ld";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => Source.ToReferenceString();
}

/// <summary>
/// Represents a store operation with side effects.
/// </summary>
sealed partial class Store : MemoryValue
{
    /// <summary>
    /// Constructs a new store operation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="target">The target view.</param>
    /// <param name="value">The value to store.</param>
    public Store(in BasicBlockValueInitializer initializer, Value target, Value value)
        : base(initializer, initializer.ModuleBuilder.VoidType)
    {
        Seal(target, value);
    }

    /// <summary>
    /// Returns the target view.
    /// </summary>
    public Value Target => GetValue<Value>(0);

    /// <summary>
    /// Returns the target address space this store writes to.
    /// </summary>
    public MemoryAddressSpace TargetAddressSpace =>
        Target.GetTypeAs<AddressSpaceType>().AddressSpace;

    /// <summary>
    /// Returns the value to store.
    /// </summary>
    public Value Value => GetValue<Value>(1);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var newTarget = rewriter.Rewrite(Target);
        var newValue = rewriter.Rewrite(Value);
        return rewriter.Builder.CreateStore(Location, newTarget, newValue);
    }

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "st";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{Target.ToReferenceString()} -> {Value.ToReferenceString()}";
}
