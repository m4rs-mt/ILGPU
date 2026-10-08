// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DeviceConstants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPUC.IR.ModuleValues;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// Represents a device constant inside a kernel.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="constantType">The constant type node.</param>
abstract class DeviceConstantValue(
    in PureValueInitializer initializer,
    TypeValue constantType) : ConstantNode(initializer, constantType);

/// <summary>
/// Represents the <see cref="Accelerator.AcceleratorType"/> property.
/// </summary>
/// <param name="initializer">The value initializer.</param>
sealed partial class AcceleratorTypeValue(in PureValueInitializer initializer) :
    DeviceConstantValue(
        initializer,
        initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32))
{
    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateAcceleratorTypeValue(Location);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "{AcceleratorType}";
}

/// <summary>
/// Represents the <see cref="XMath.FastMath"/> property.
/// </summary>
/// <param name="initializer">The value initializer.</param>
sealed partial class FastMathValue(in PureValueInitializer initializer) :
    DeviceConstantValue(
        initializer,
        initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int1))
{
    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateFastMathValue(Location);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "{FastMath}";
}

/// <summary>
/// Represents the <see cref="XMath.FlushToZero"/> property.
/// </summary>
/// <param name="initializer">The value initializer.</param>
sealed partial class FlushToZeroValue(in PureValueInitializer initializer) :
    DeviceConstantValue(
        initializer,
        initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int1))
{
    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateFlushToZeroValue(Location);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "{FlushToZero}";
}

/// <summary>
/// Represents the <see cref="AcceleratorArchitecture.Current"/> property.
/// </summary>
/// <param name="initializer">The value initializer.</param>
sealed partial class AcceleratorArchitectureValue(in PureValueInitializer initializer) :
    DeviceConstantValue(
        initializer,
        GetCudaArchitectureType(initializer))
{
    /// <summary>
    /// Determines the managed structure type of a Cuda architecture tuple.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <returns>The Cuda architecture type.</returns>
    private static TypeValue GetCudaArchitectureType(in PureValueInitializer initializer)
    {
        var int32Type = initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32);
        var structureBuilder = initializer.ModuleBuilder.CreateStructureType(2);
        structureBuilder.Add(int32Type);
        structureBuilder.Add(int32Type);
        return structureBuilder.Seal();
    }

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateAcceleratorArchitectureValue(Location);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "[CudaArch]";
}

/// <summary>
/// Represents the <see cref="Grid.Index"/> property.
/// </summary>
/// <remarks>
/// Constructs a new value.
/// </remarks>
/// <param name="initializer">The value initializer.</param>
sealed partial class GridIndexValue(in PureValueInitializer initializer) :
    DeviceConstantValue(
        initializer,
        initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32))
{
    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateGridIndexValue(Location);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "gridIdx";
}

/// <summary>
/// Represents the <see cref="Group.Index"/> property.
/// </summary>
/// <param name="initializer">The value initializer.</param>
sealed partial class GroupIndexValue(in PureValueInitializer initializer) :
    DeviceConstantValue(
        initializer,
        initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32))
{
    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateGroupIndexValue(Location);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "groupIdx";
}

/// <summary>
/// Represents the <see cref="Warp.Index"/> property.
/// </summary>
/// <param name="initializer">The value initializer.</param>
sealed partial class SubGroupIndexValue(in PureValueInitializer initializer) :
    DeviceConstantValue(
        initializer,
        initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32))
{
    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateSubGroupIndexValue(Location);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "subGroupIdx";
}

/// <summary>
/// Represents the <see cref="Warp.LaneIndex"/> property.
/// </summary>
/// <param name="initializer">The value initializer.</param>
sealed partial class SubGroupLaneIndexValue(in PureValueInitializer initializer) :
    DeviceConstantValue(
        initializer,
        initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32))
{
    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateSubGroupLaneIndexValue(Location);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "subGroupLaneIdx";
}

/// <summary>
/// Represents the <see cref="Grid.Dimension"/> property.
/// </summary>
/// <param name="initializer">The value initializer.</param>
sealed partial class GridDimensionValue(in PureValueInitializer initializer) :
    DeviceConstantValue(
        initializer,
        initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32))
{
    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateGridDimensionValue(Location);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "gridDim";
}

/// <summary>
/// Represents the <see cref="Group.Dimension"/> property.
/// </summary>
/// <param name="initializer">The value initializer.</param>
sealed partial class GroupDimensionValue(in PureValueInitializer initializer) :
    DeviceConstantValue(
        initializer,
        initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32))
{
    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateGroupDimensionValue(Location);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "groupDim";
}

/// <summary>
/// Represents the <see cref="Warp.Dimension"/> property.
/// </summary>
/// <param name="initializer">The value initializer.</param>
sealed partial class SubGroupDimensionValue(in PureValueInitializer initializer) :
    DeviceConstantValue(
        initializer,
        initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32))
{
    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateSubGroupDimensionValue(Location);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "groupDim";
}
