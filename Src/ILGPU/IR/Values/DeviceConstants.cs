// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: DeviceConstants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Runtime;

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
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="constantType">The constant type node.</param>
        internal DeviceConstantValue(
            BasicBlock basicBlock,
            TypeNode constantType)
            : base(basicBlock, constantType)
        { }

        #endregion
    }

    /// <summary>
    /// Represents the <see cref="Accelerator.AcceleratorType"/> property.
    /// </summary>
    [ValueKind(ValueKind.AcceleratorType)]
    public sealed class AcceleratorTypeValue : DeviceConstantValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="context">The current IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        internal AcceleratorTypeValue(
            IRContext context,
            BasicBlock basicBlock)
            : base(
                  basicBlock,
                  context.GetPrimitiveType(BasicValueType.Int32))
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.AcceleratorType;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateAcceleratorTypeValue();

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "acclType";

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
        /// <param name="context">The current IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="dimension">The device constant dimension.</param>
        internal DeviceConstantDimensionValue(
            IRContext context,
            BasicBlock basicBlock,
            DeviceConstantDimension3D dimension)
            : base(
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
    /// Represents the <see cref="Grid.Index"/> property.
    /// </summary>
    [ValueKind(ValueKind.GridIndex)]
    public sealed class GridIndexValue : DeviceConstantDimensionValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="context">The current IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="dimension">The constant dimension.</param>
        internal GridIndexValue(
            IRContext context,
            BasicBlock basicBlock,
            DeviceConstantDimension3D dimension)
            : base(
                  context,
                  basicBlock,
                  dimension)
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GridIndex;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGridIndexValue(Dimension);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "gridIdx";

        #endregion
    }

    /// <summary>
    /// Represents the <see cref="Group.Index"/> property.
    /// </summary>
    [ValueKind(ValueKind.GroupIndex)]
    public sealed class GroupIndexValue : DeviceConstantDimensionValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="context">The current IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="dimension">The constant dimension.</param>
        internal GroupIndexValue(
            IRContext context,
            BasicBlock basicBlock,
            DeviceConstantDimension3D dimension)
            : base(
                  context,
                  basicBlock,
                  dimension)
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GroupIndex;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGroupIndexValue(Dimension);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "groupIdx";

        #endregion
    }

    /// <summary>
    /// Represents the <see cref="Grid.Dimension"/> property.
    /// </summary>
    [ValueKind(ValueKind.GridDimension)]
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
                  context,
                  basicBlock,
                  dimension)
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GridDimension;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
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
    [ValueKind(ValueKind.GroupDimension)]
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
                  context,
                  basicBlock,
                  dimension)
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GroupDimension;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
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
    [ValueKind(ValueKind.WarpSize)]
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
                  basicBlock,
                  context.GetPrimitiveType(BasicValueType.Int32))
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.WarpSize;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
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
    [ValueKind(ValueKind.LaneIdx)]
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
                  basicBlock,
                  context.GetPrimitiveType(BasicValueType.Int32))
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.LaneIdx;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
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
