// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2019-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AcceleratorSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Specifies an architecture.
/// </summary>
/// <param name="Capabilities">The accelerator capabilities.</param>
/// <param name="WarpSize">The warp size (if known).</param>
/// <param name="Architecture">The accelerator architecture (or (0, 0)).</param>
/// <param name="SupportsViews">True if this accelerator supports views.</param>
sealed record class ArchitectureSpecification(
    AcceleratorCapabilities Capabilities,
    int? WarpSize,
    AcceleratorArchitecture Architecture,
    bool SupportsViews)
{
    public AcceleratorType AcceleratorType => Capabilities.AcceleratorType;
}

/// <summary>
/// Represents a device specializer that instantiates device-specific constants
/// and updates device-specific functionality.
/// </summary>
/// <remarks>
/// Note that this class does not perform recursive specialization operations.
/// Debug assertion and IO stripping is handled earlier by <see cref="DebugSetter"/>.
/// </remarks>
/// <param name="args">The transformation args.</param>
/// <param name="specification">The architecture specification.</param>
sealed class AcceleratorSpecializer(
    TransformationArgs args,
    ArchitectureSpecification specification) :
    Transformation(args)
{
    private readonly bool _flushToZero = args.Properties.EnableMathFlushToZero;
    private readonly bool _fastMath = args.Properties.MathMode != MathMode.Default;
    private readonly TypeValue _intPointerType = args.Module.IntPointerType;

    /// <summary>
    /// Maps accelerator-specific values.
    /// </summary>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        MapPureValue<AcceleratorTypeValue>(
            (rewriter, value) => rewriter.Builder.CreatePrimitiveValue(
                value.Location,
                (int)specification.AcceleratorType));
        MapPureValue<FastMathValue>(
            (rewriter, value) => rewriter.Builder.CreatePrimitiveValue(
                value.Location,
                _fastMath));
        MapPureValue<FlushToZeroValue>(
            (rewriter, value) => rewriter.Builder.CreatePrimitiveValue(
                value.Location,
                _flushToZero));
        MapPureValue<AcceleratorArchitectureValue>(
            (rewriter, value) => rewriter.Builder.CreatePrimitiveValue(
                value.Location,
                (specification.Architecture.Major, specification.Architecture.Minor)));

        if (specification.WarpSize.HasValue)
        {
            MapPureValue<SubGroupDimensionValue>(
                (rewriter, value) => rewriter.Builder.CreatePrimitiveValue(
                    value.Location,
                    specification.WarpSize.Value));
        }

        MapPureValue<IntAsPointerCast>(Map);
        MapPureValue<PointerAsIntCast>(Map);
    }

    /// <summary>
    /// Specializes int to native pointer casts.
    /// </summary>
    private Value? Map(PureValueTransform transform, IntAsPointerCast value)
    {
        if (value.TargetType.Equals(_intPointerType))
            return value;

        // Convert from int -> native int type -> pointer
        var builder = transform.Builder;

        // int -> native int type
        var convertToNativeInt = builder.CreateConvert(
            value.Location,
            transform.Rewrite(value.Source),
            transform.Rewrite(_intPointerType));

        // native int type -> pointer
        var convert = builder.CreateIntAsPointerCast(
            value.Location,
            convertToNativeInt);

        return convert;
    }

    /// <summary>
    /// Specializes native pointer to int casts.
    /// </summary>
    private Value? Map(PureValueTransform transform, PointerAsIntCast value)
    {
        if (value.TargetType.Equals(_intPointerType))
            return value;

        // Convert from ptr -> native int type -> desired int type
        var builder = transform.Builder;

        // ptr -> native int type
        var convertToNativeType = builder.CreatePointerAsIntCast(
            value.Location,
            transform.Rewrite(value.Source),
            _intPointerType.BasicValueType);

        // native int type -> desired int type
        var convert = builder.CreateConvert(
            value.Location,
            convertToNativeType,
            value.TargetType);

        return convert;
    }
}
