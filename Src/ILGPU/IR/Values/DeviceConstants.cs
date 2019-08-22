// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: DeviceConstants.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a device constant inside a kernel.
    /// </summary>
    public abstract class DeviceConstantValue : ConstantNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="kind">The value kind.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="constantType">The constant type node.</param>
        internal DeviceConstantValue(
            ValueKind kind,
            BasicBlock basicBlock,
            TypeNode constantType)
            : base(kind, basicBlock, constantType)
        { }

        #endregion
    }

    /// <summary>
    /// Represents a dimension of a 3D device constant.
    /// </summary>
    public enum DeviceConstantDimension3D
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
    public abstract class DeviceConstantDimensionValue : DeviceConstantValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="kind">The value kind.</param>
        /// <param name="context">The current IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="dimension">The device constant dimension.</param>
        internal DeviceConstantDimensionValue(
            ValueKind kind,
            IRContext context,
            BasicBlock basicBlock,
            DeviceConstantDimension3D dimension)
            : base(
                  kind,
                  basicBlock,
                  context.GetPrimitiveType(BasicValueType.Int32))
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
    /// Represents the <see cref="Grid.Dimension"/> property.
    /// </summary>
    public sealed class GridDimensionValue : DeviceConstantDimensionValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="context">The current IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="dimension">The constant dimension.</param>
        internal GridDimensionValue(
            IRContext context,
            BasicBlock basicBlock,
            DeviceConstantDimension3D dimension)
            : base(
                  ValueKind.GridDimension,
                  context,
                  basicBlock,
                  dimension)
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateGridDimensionValue(Dimension);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "gridDim";

        #endregion
    }

    /// <summary>
    /// Represents the <see cref="Group.Dimension"/> property.
    /// </summary>
    public sealed class GroupDimensionValue : DeviceConstantDimensionValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="context">The current IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="dimension">The constant dimension.</param>
        internal GroupDimensionValue(
            IRContext context,
            BasicBlock basicBlock,
            DeviceConstantDimension3D dimension)
            : base(
                  ValueKind.GroupDimension,
                  context,
                  basicBlock,
                  dimension)
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateGroupDimensionValue(Dimension);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "groupDim";

        #endregion
    }

    /// <summary>
    /// Represents the <see cref="Warp.WarpSize"/> property.
    /// </summary>
    public sealed class WarpSizeValue : DeviceConstantValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="context">The current IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        internal WarpSizeValue(
            IRContext context,
            BasicBlock basicBlock)
            : base(
                  ValueKind.WarpSize,
                  basicBlock,
                  context.GetPrimitiveType(BasicValueType.Int32))
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateWarpSizeValue();

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "warpSize";

        #endregion
    }

    /// <summary>
    /// Represents the <see cref="Warp.LaneIdx"/> property.
    /// </summary>
    public sealed class LaneIdxValue : DeviceConstantValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="context">The current IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        internal LaneIdxValue(
            IRContext context,
            BasicBlock basicBlock)
            : base(
                  ValueKind.LaneIdx,
                  basicBlock,
                  context.GetPrimitiveType(BasicValueType.Int32))
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateLaneIdxValue();

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "laneIdx";

        #endregion
    }
}
