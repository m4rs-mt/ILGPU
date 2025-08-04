// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: DeviceConstants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.IR.Construction;
using ILGPUC.IR.Types;

namespace ILGPUC.IR.Values;

/// <summary>
/// Represents a device constant inside a kernel.
/// </summary>
abstract class DeviceConstantValue : ConstantNode
{
    #region Instance

    /// <summary>
    /// Constructs a new value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="constantType">The constant type node.</param>
    internal DeviceConstantValue(
        in ValueInitializer initializer,
        TypeNode constantType)
        : base(initializer, constantType)
    { }

    #endregion
}

/// <summary>
/// Represents the <see cref="Accelerator.AcceleratorType"/> property.
/// </summary>
sealed partial class AcceleratorTypeValue : DeviceConstantValue
{
    #region Instance

    /// <summary>
    /// Constructs a new value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    internal AcceleratorTypeValue(in ValueInitializer initializer)
        : base(
              initializer,
              initializer.Context.GetPrimitiveType(BasicValueType.Int32))
    { }

    #endregion

    #region Methods

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateAcceleratorTypeValue(Location);

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "typeof(accelerator)";

    #endregion
}

/// <summary>
/// Represents a dimension of a 3D device constant.
/// </summary>
enum DeviceConstantDimension3D
{
    /// <summary>
    /// The X dimension.
    /// </summary>
    X,

    /// <summary>
    /// The Y dimension.
    /// </summary>
    Y,

    /// <summary>
    /// The Z dimension.
    /// </summary>
    Z,
}

/// <summary>
/// Represents a device constant inside a kernel.
/// </summary>
abstract class DeviceConstantDimensionValue : DeviceConstantValue
{
    #region Instance

    /// <summary>
    /// Constructs a new value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="dimension">The device constant dimension.</param>
    internal DeviceConstantDimensionValue(
        in ValueInitializer initializer,
        DeviceConstantDimension3D dimension)
        : base(
              initializer,
              initializer.Context.GetPrimitiveType(BasicValueType.Int32))
    {
        Dimension = dimension;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the constant dimension.
    /// </summary>
    public DeviceConstantDimension3D Dimension { get; }

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToArgString() => Dimension.ToString();

    #endregion
}

/// <summary>
/// Represents the <see cref="Grid.Index"/> property.
/// </summary>
sealed partial class GridIndexValue : DeviceConstantDimensionValue
{
    #region Instance

    /// <summary>
    /// Constructs a new value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="dimension">The constant dimension.</param>
    internal GridIndexValue(
        in ValueInitializer initializer,
        DeviceConstantDimension3D dimension)
        : base(initializer, dimension)
    { }

    #endregion

    #region Methods

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateGridIndexValue(Location, Dimension);

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "gridIdx";

    #endregion
}

/// <summary>
/// Represents the <see cref="Group.Index"/> property.
/// </summary>
sealed partial class GroupIndexValue : DeviceConstantDimensionValue
{
    #region Instance

    /// <summary>
    /// Constructs a new value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="dimension">The constant dimension.</param>
    internal GroupIndexValue(
        in ValueInitializer initializer,
        DeviceConstantDimension3D dimension)
        : base(initializer, dimension)
    { }

    #endregion

    #region Methods

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateGroupIndexValue(Location, Dimension);

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "groupIdx";

    #endregion
}

/// <summary>
/// Represents the <see cref="Grid.Dimension"/> property.
/// </summary>
sealed partial class GridDimensionValue : DeviceConstantDimensionValue
{
    #region Instance

    /// <summary>
    /// Constructs a new value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="dimension">The constant dimension.</param>
    internal GridDimensionValue(
        in ValueInitializer initializer,
        DeviceConstantDimension3D dimension)
        : base(initializer, dimension)
    { }

    #endregion

    #region Methods

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateGridDimensionValue(Location, Dimension);

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "gridDim";

    #endregion
}

/// <summary>
/// Represents the <see cref="Group.Dimension"/> property.
/// </summary>
sealed partial class GroupDimensionValue : DeviceConstantDimensionValue
{
    #region Instance

    /// <summary>
    /// Constructs a new value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="dimension">The constant dimension.</param>
    internal GroupDimensionValue(
        in ValueInitializer initializer,
        DeviceConstantDimension3D dimension)
        : base(initializer, dimension)
    { }

    #endregion

    #region Methods

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateGroupDimensionValue(Location, Dimension);

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "groupDim";

    #endregion
}

/// <summary>
/// Represents the <see cref="Warp.Dimension"/> property.
/// </summary>
sealed partial class WarpSizeValue : DeviceConstantValue
{
    #region Instance

    /// <summary>
    /// Constructs a new value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    internal WarpSizeValue(in ValueInitializer initializer)
        : base(
              initializer,
              initializer.Context.GetPrimitiveType(BasicValueType.Int32))
    { }

    #endregion

    #region Methods

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateWarpSizeValue(Location);

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "warpSize";

    #endregion
}

/// <summary>
/// Represents the <see cref="Warp.LaneIndex"/> property.
/// </summary>
sealed partial class LaneIdxValue : DeviceConstantValue
{
    #region Instance

    /// <summary>
    /// Constructs a new value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    internal LaneIdxValue(in ValueInitializer initializer)
        : base(
              initializer,
              initializer.Context.GetPrimitiveType(BasicValueType.Int32))
    { }

    #endregion

    #region Methods

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateLaneIdxValue(Location);

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "laneIdx";

    #endregion
}

/// <summary>
/// Represents the value returned by calling the <see cref="ArrayView{T}.Length"/>
/// property on a dynamic memory view.
/// </summary>
sealed partial class DynamicMemoryLengthValue : DeviceConstantValue
{
    #region Instance

    /// <summary>
    /// Constructs a new value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="elementType">The element type node.</param>
    /// <param name="addressSpace">The target address space.</param>
    internal DynamicMemoryLengthValue(
        in ValueInitializer initializer,
        TypeNode elementType,
        MemoryAddressSpace addressSpace)
        : base(
              initializer,
              initializer.Context.GetPrimitiveType(BasicValueType.Int32))
    {
        ElementType = elementType;
        AddressSpace = addressSpace;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the element type node.
    /// </summary>
    public TypeNode ElementType { get; }

    /// <summary>
    /// Returns the address space of this allocation.
    /// </summary>
    public MemoryAddressSpace AddressSpace { get; }

    #endregion

    #region Methods

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateDynamicMemoryLengthValue(Location, ElementType, AddressSpace);

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "dynamicMemLength";

    #endregion
}
