// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IPureValueRewriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.PureValues.Construction;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Rewriting;

/// <summary>
/// An abstract rewriter for pure values.
/// </summary>
interface IPureValueRewriter : IRewriter<PureValueBuilder>, IBaseTypeRewriter;

/// <summary>
/// A stand alone pure value rewriter to rewrite pure values only.
/// </summary>
/// <param name="methodBuilder">The parent method builder.</param>
readonly struct PureValueRewriter(MethodBuilder methodBuilder) : IPureValueRewriter
{
    private readonly GlobalValueMap<Value?> _replaced =
        new(methodBuilder.Generation, 16);

    /// <inheritdoc/>
    public PureValueBuilder Builder => methodBuilder;

    /// <inheritdoc/>
    public Generation Generation => methodBuilder.Generation;

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
        if (oldValue is not PureValue pureValue)
        {
            Generation.ValidateCurrentOrPreviousGeneration(oldValue);
            return oldValue;
        }

        newValue = pureValue.Rewrite(this);
        _replaced.Add(pureValue, newValue);
        return newValue;
    }

    /// <summary>
    /// Replaces the specified value with the given value during rewriting.
    /// </summary>
    /// <param name="oldValue">The old value to replace.</param>
    /// <param name="value">The new value to replace it with.</param>
    public void Replace(Value<Method>? oldValue, Value? value)
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
    FieldSpan IBaseTypeRewriter.Rewrite(
        StructureType structureType,
        FieldSpan fieldSpan) => fieldSpan;
}