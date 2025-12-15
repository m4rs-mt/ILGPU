// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AlignValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using System;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// Represents an abstract alignment operation value.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class BaseAlignOperationValue(
    in PureValueInitializer initializer,
    TypeValue type) : PointerValue(initializer, type)
{
    /// <summary>
    /// Returns the alignment in bytes.
    /// </summary>
    public Value AlignmentInBytes => GetValue<Value>(1);

    /// <summary>
    /// Returns true if the current operation works on a view.
    /// </summary>
    public bool IsViewOperation => Source.Type is ViewType;

    /// <summary>
    /// Returns true if the current operation works on a pointer.
    /// </summary>
    public bool IsPointerOperation => Source.Type is PointerType;

    /// <summary>
    /// Tries to determine an explicit alignment compile-time constant (primarily
    /// for compiler analysis purposes). If this alignment information could not be
    /// resolved, the function returns the worst-case alignment of 1.
    /// </summary>
    public int GetAlignmentConstant() =>
        TryGetAlignmentConstant(out int constant) ? constant : 1;

    /// <summary>
    /// Tries to determine a compile-time known alignment constant.
    /// </summary>
    /// <param name="alignmentConstant">
    /// The determined alignment constant (if any).
    /// </param>
    /// <returns>True, if an alignment constant could be determined.</returns>
    public bool TryGetAlignmentConstant(out int alignmentConstant)
    {
        if (AlignmentInBytes is PrimitiveValue primitive)
        {
            alignmentConstant = Math.Max(primitive.Int32Value, 1);
            return true;
        }
        alignmentConstant = 0;
        return false;
    }

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{base.ToArgString()}, {AlignmentInBytes}";
}

/// <summary>
/// Aligns a pointer or a view to a specified alignment in bytes.
/// </summary>
sealed partial class AlignTo : BaseAlignOperationValue
{
    /// <summary>
    /// Determines the type of a new align operation.
    /// </summary>
    private static TypeValue GetType(in PureValueInitializer initializer, Value view)
    {
        var type = view.Type;
        if (view.Type is ViewType)
        {
            // The return type will be a structure type on an unaligned prefix part
            // and an aligned main view part
            var builder = initializer.ModuleBuilder.CreateStructureType(2);
            builder.Add(view.Type);
            builder.Add(view.Type);
            type = builder.Seal();
        }
        return type;
    }

    /// <summary>
    /// Constructs an aligned pointer/view.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="source">The underlying view.</param>
    /// <param name="alignmentInBytes">The alignment in bytes.</param>
    public AlignTo(
        in PureValueInitializer initializer,
        Value source,
        Value alignmentInBytes)
        : base(initializer, GetType(initializer, source))
    {
        Seal(source, alignmentInBytes);
    }

    /// <inheritdoc/>/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateAlignTo(
            Location,
            rewriter.Rewrite(Source),
            rewriter.Rewrite(AlignmentInBytes));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() =>
        IsViewOperation ? "alignViewTo" : "alignPtrTo";
}

/// <summary>
/// Interprets the given pointer or view to be aligned to the given alignment in
/// bytes.
/// </summary>
sealed partial class AsAligned : BaseAlignOperationValue
{
    /// <summary>
    /// Constructs an alignment interpretation value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="source">The underlying view.</param>
    /// <param name="alignmentInBytes">The alignment in bytes.</param>
    public AsAligned(
        in PureValueInitializer initializer,
        Value source,
        Value alignmentInBytes)
        : base(initializer, source.Type)
    {
        Seal(source, alignmentInBytes);
    }

    /// <inheritdoc/>/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateAsAligned(
            Location,
            rewriter.Rewrite(Source),
            rewriter.Rewrite(AlignmentInBytes));


    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() =>
        IsViewOperation ? "asAlignedView" : "asAlignedPtr";
}
