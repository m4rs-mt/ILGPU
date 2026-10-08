// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PureValueTransform.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.PureValues;
using ILGPUC.IR.PureValues.Construction;
using ILGPUC.IR.Rewriting;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// A transformation of a pure value.
/// </summary>
/// <param name="transform">The parent method transform.</param>
sealed class PureValueTransform(MethodTransform transform) :
    PureValueBuilder(transform.Generation, transform.Location),
    IPureValueRewriter,
    ITypeRewriter
{
    /// <inheritdoc/>
    public PureValueBuilder Builder => transform;

    /// <summary>
    /// Returns the parent method transform.
    /// </summary>
    public MethodTransform MethodTransform => transform;

    /// <summary>
    /// Returns the parent module transform.
    /// </summary>
    public ModuleTransform ModuleTransform => transform.ModuleTransform;

    /// <inheritdoc/>
    public override Method Method => transform.Method;

    /// <inheritdoc/>
    public override ModuleBuilder ModuleBuilder => ModuleTransform;

    /// <inheritdoc/>
    ModuleBuilder ITypeRewriter.ModuleBuilder => ModuleTransform;

    /// <inheritdoc/>
    public bool IsReplacedOrRemoved(Value? oldValue) => TryGetReplaced(oldValue, out _);

    /// <inheritdoc/>
    public Value? Rewrite(Value oldValue) =>
        transform.Rewrite(oldValue);

    /// <summary>
    /// Replaces the specified value with the given value during rewriting.
    /// </summary>
    /// <param name="oldValue">The old value to replace.</param>
    /// <param name="value">The new value to replace it with.</param>
    public void Replace(Value<Method>? oldValue, Value? value)
    {
        if (oldValue is not null)
            transform.Replace(oldValue, value);
    }

    /// <inheritdoc/>
    public bool TryGetReplaced(Value? oldValue, out Value? newValue) =>
        transform.TryGetReplaced(oldValue, out newValue);

    /// <inheritdoc/>
    public FieldSpan Rewrite(StructureType structureType, FieldSpan fieldSpan) =>
        transform.Rewrite(structureType, fieldSpan);

    /// <inheritdoc/>
    public TypeValue Rewrite(TypeValue oldValue) =>
        transform.Rewrite(oldValue);
}