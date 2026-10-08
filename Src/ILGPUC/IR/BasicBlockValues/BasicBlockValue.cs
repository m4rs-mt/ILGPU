// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicBlockValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.Rewriting;

namespace ILGPUC.IR.BasicBlockValues;

/// <summary>
/// The base interface of all values.
/// </summary>
interface IBasicBlockValue : IValue<Method>
{
    /// <summary>
    /// Rewrites the current node.
    /// </summary>
    /// <typeparam name="TRewriter">
    /// The rewriter type to control rewriting of values and value classes.
    /// </typeparam>
    /// <param name="rewriter">The current rewriter.</param>
    /// <returns>The rewritten value.</returns>
    Value? Rewrite<TRewriter>(in TRewriter rewriter)
        where TRewriter : IBasicBlockRewriter, allows ref struct;
}

/// <summary>
/// A general value initializer.
/// </summary>
/// <param name="Builder">The parent builder.</param>
/// <param name="Previous">The previous value.</param>
/// <param name="Location">The associated location.</param>
readonly record struct BasicBlockValueInitializer(
    BasicBlockBuilder Builder,
    BasicBlockValue? Previous,
    Location Location) : ILocation
{
    /// <summary>
    /// Returns the parent module.
    /// </summary>
    public Module Module => Builder.ModuleBuilder.Module;

    /// <summary>
    /// Returns the parent module builder.
    /// </summary>
    public ModuleBuilder ModuleBuilder => Builder.ModuleBuilder;

    /// <summary>
    /// Returns the current basic block.
    /// </summary>
    public BasicBlock BasicBlock => Builder.BasicBlock;

    /// <summary>
    /// Returns the current generation.
    /// </summary>
    public Generation Generation => Builder.Generation;

    /// <summary>
    /// Formats an error message to include specific location information.
    /// </summary>
    string ILocation.FormatErrorMessage(string message) =>
        Location.FormatErrorMessage(
            $"{message} [block '{BasicBlock.Name}', method '{BasicBlock.Method.Name}'," +
            $" generation {Generation.Index}]");

    /// <summary>
    /// Implicitly converts the given initializer into a value initializer.
    /// </summary>
    /// <param name="initializer">The initializer to convert.</param>
    public static implicit operator ValueInitializer(
        BasicBlockValueInitializer initializer) =>
        new(initializer.Module, initializer.Location);
}

/// <summary>
/// Represents a basic intermediate-representation value.
/// It is the base class for all values in the scope of this IR.
/// </summary>
/// <remarks>
/// Constructs a new value.
/// </remarks>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class BasicBlockValue(
    in BasicBlockValueInitializer initializer,
    TypeValue type) :
    Value<Method>(initializer, type),
    IBasicBlockValue,
    IValueClassInformation
{
    private readonly Method _methodScope = initializer.Builder.Method;

    /// <summary>
    /// Returns the basic block value class.
    /// </summary>
    static ValueClass IValueClassInformation.ValueClass => ValueClass.BasicBlock;

    /// <summary>
    /// Returns the basic block this value belongs to.
    /// </summary>
    public BasicBlock BasicBlock { get; internal set; } =
        initializer.Previous?.BasicBlock ?? initializer.BasicBlock;

    /// <summary>
    /// Returns the previous value in the monad chain (or null if this is the first
    /// value).
    /// </summary>
    public BasicBlockValue? Previous { get; internal set; } = initializer.Previous;

    /// <summary>
    /// Returns the next value in the monad chain (or null if this is the first
    /// value).
    /// </summary>
    public BasicBlockValue? Next { get; private set; }

    /// <summary>
    /// Returns the parent scope (the method containing this basic block).
    /// </summary>
    public sealed override Method Scope => _methodScope;

    /// <summary>
    /// Returns the parent method.
    /// </summary>
    Method IValue<Method>.Scope => _methodScope;

    /// <summary>
    /// Returns the basic block value class.
    /// </summary>
    public sealed override ValueClass ValueClass => ValueClass.BasicBlock;

    /// <inheritdoc/>
    public sealed override bool ComputeUses(GlobalValueSet visited) =>
        base.ComputeUses(visited);

    /// <summary>
    /// Rewrites the current node.
    /// </summary>
    /// <typeparam name="TRewriter">
    /// The rewriter type to control rewriting of values and value classes.
    /// </typeparam>
    /// <param name="rewriter">The current rewriter.</param>
    /// <returns>The rewritten value.</returns>
    public abstract Value? Rewrite<TRewriter>(in TRewriter rewriter)
        where TRewriter : IBasicBlockRewriter, allows ref struct;

    /// <summary>
    /// Sets the internal next link during sealing.
    /// </summary>
    /// <param name="next">The next value to use.</param>
    /// <param name="basicBlock">The parent basic block.</param>
    internal void SetNextInternal(BasicBlockValue? next, BasicBlock basicBlock)
    {
        Next = next;
        BasicBlock = basicBlock;
    }

    /// <summary>
    /// Returns the hash code of the value id.
    /// </summary>
    public sealed override int GetHashCode() => Id.GetHashCode();

    /// <summary>
    /// Returns true if the given value is exactly this value.
    /// </summary>
    public sealed override bool Equals(object? obj) =>
        obj is BasicBlockValue bbValue && bbValue.Id == Id;
}
