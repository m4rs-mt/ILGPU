// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IBasicBlockRewriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.PureValues.Construction;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Rewriting;

/// <summary>
/// An abstract rewriter for basic blocks.
/// </summary>
interface IBasicBlockRewriter : IRewriter<BasicBlockBuilder>
{
    /// <summary>
    /// Returns the underlying new basic block.
    /// </summary>
    BasicBlock BasicBlock { get; }

    /// <summary>
    /// Returns the old block.
    /// </summary>
    BasicBlock OldBasicBlock { get; }

    /// <summary>
    /// Tries to get a relinked previous value for the given value.
    /// </summary>
    /// <param name="basicBlockValue">The old value.</param>
    /// <param name="newPreviousValue">The newly mapped value to link to.</param>
    /// <returns>
    /// True if the given basic block value should get a different previous value.
    /// </returns>
    bool TryGetRelinked(
        BasicBlockValue basicBlockValue,
        [NotNullWhen(true)] out BasicBlockValue? newPreviousValue);

    /// <summary>
    /// Copies termination from the given basic block.
    /// </summary>
    /// <param name="basicBlock">The basic block to copy the termination from.</param>
    /// <param name="returnBlock">The return block to use for returns.</param>
    void CopyTerminationFrom(BasicBlock basicBlock, BasicBlock? returnBlock = null);
}

/// <summary>
/// A stand alone basic-block and pure-value rewriter to rewrite pure values only.
/// </summary>
/// <param name="target">The parent method builder.</param>
/// <param name="oldBlock">The current (old version) of the basic block.</param>
sealed class BasicBlockRewriter(BasicBlockBuilder target, BasicBlock oldBlock) :
    IBasicBlockRewriter, IPureValueRewriter
{
    private readonly GlobalValueMap<Value?> _replaced =
        new(target.Generation, Math.Max(oldBlock.Count * 2, 32));
    /// <inheritdoc/>
    public BasicBlock BasicBlock => target.BasicBlock;

    /// <inheritdoc/>
    public BasicBlock OldBasicBlock => oldBlock;

    /// <inheritdoc/>
    public BasicBlockBuilder Builder => target;

    /// <inheritdoc/>
    public Generation Generation => target.Generation;

    /// <summary>
    /// Returns the underlying pure value builder.
    /// </summary>
    PureValueBuilder IRewriter<PureValueBuilder>.Builder => target;

    /// <inheritdoc/>
    public bool TryGetRelinked(
        BasicBlockValue basicBlockValue,
        [NotNullWhen(true)] out BasicBlockValue? newPreviousValue)
    {
        newPreviousValue = null;
        return false;
    }

    /// <inheritdoc/>
    public bool IsReplacedOrRemoved(Value? oldValue) => TryGetReplaced(oldValue, out _);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Value? Rewrite(Value oldValue)
    {
        // Check whether this value has been replaced
        if (_replaced.TryGetValue(oldValue, out var newValue))
            return newValue;

        // Check whether this value type is out of scope
        if (oldValue is not BasicBlockValue or PureValue)
            return oldValue;

        // Check whether we can rewrite a pure value
        if (oldValue is PureValue pureValue)
        {
            newValue = pureValue.Rewrite(this);
            _replaced.Add(pureValue, newValue);
            return newValue;
        }

        // Rewrite the value
        var bbValue = oldValue.AsNotNullCast<BasicBlockValue>();
        newValue = bbValue.Rewrite(this);
        _replaced.Add(bbValue, newValue);
        return newValue;
    }

    /// <summary>
    /// Replaces the specified value with the given value during rewriting.
    /// </summary>
    /// <param name="oldValue">The old value to replace.</param>
    /// <param name="value">The new value to replace it with.</param>
    public void Replace(Value? oldValue, Value? value)
    {
        if (oldValue is null) return;
        _replaced[oldValue] = value;
    }

    /// <inheritdoc/>
    public bool TryGetReplaced(Value? oldValue, out Value? newValue)
    {
        newValue = null;
        return oldValue is not null && _replaced.TryGetValue(oldValue, out newValue);
    }

    /// <inheritdoc/>
    public void CopyTerminationFrom(
        BasicBlock basicBlock,
        BasicBlock? returnBlock = null) =>
        basicBlock.CopyTerminationTo(this, returnBlock);

    /// <inheritdoc/>
    FieldSpan IBaseTypeRewriter.Rewrite(
        StructureType structureType,
        FieldSpan fieldSpan) => fieldSpan;
}