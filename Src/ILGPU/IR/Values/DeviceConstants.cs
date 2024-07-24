// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: DeviceConstants.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
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
    [ValueKind(ValueKind.AcceleratorType)]
    public sealed class AcceleratorTypeValue : DeviceConstantValue, IValueReader
    {
        #region Static

        /// <summary cref="IValueReader.Read(ValueHeader, IIRReader)"/>
        public static Value? Read(ValueHeader header, IIRReader reader)
        {
            var methodBuilder = header.Method?.MethodBuilder;
            if (methodBuilder is not null &&
                header.Block is not null &&
                header.Block.GetOrCreateBuilder(methodBuilder,
                out BasicBlock.Builder? blockBuilder))
            {
                return blockBuilder.CreateAcceleratorTypeValue(
                    Location.Unknown);
            }
            else
            {
                return null;
            }
        }

        #endregion

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

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.AcceleratorType;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateAcceleratorTypeValue(Location);

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer) { }

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

        #region Methods

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer) =>
            writer.Write(nameof(Dimension), Dimension);

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
    public sealed class GridIndexValue : DeviceConstantDimensionValue, IValueReader
    {
        #region Static

        /// <summary cref="IValueReader.Read(ValueHeader, IIRReader)"/>
        public static Value? Read(ValueHeader header, IIRReader reader)
        {
            var methodBuilder = header.Method?.MethodBuilder;
            if (methodBuilder is not null &&
                header.Block is not null &&
                header.Block.GetOrCreateBuilder(methodBuilder,
                out BasicBlock.Builder? blockBuilder) &&
                reader.Read(out DeviceConstantDimension3D dimension))
            {
                return blockBuilder.CreateGridIndexValue(
                    Location.Unknown, dimension);
            }
            else
            {
                return null;
            }
        }

        #endregion

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

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GridIndex;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGridIndexValue(Location, Dimension);

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
    public sealed class GroupIndexValue : DeviceConstantDimensionValue, IValueReader
    {
        #region Static

        /// <summary cref="IValueReader.Read(ValueHeader, IIRReader)"/>
        public static Value? Read(ValueHeader header, IIRReader reader)
        {
            var methodBuilder = header.Method?.MethodBuilder;
            if (methodBuilder is not null &&
                header.Block is not null &&
                header.Block.GetOrCreateBuilder(methodBuilder,
                out BasicBlock.Builder? blockBuilder) &&
                reader.Read(out DeviceConstantDimension3D dimension))
            {
                return blockBuilder.CreateGroupIndexValue(
                    Location.Unknown, dimension);
            }
            else
            {
                return null;
            }
        }

        #endregion

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

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GroupIndex;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGroupIndexValue(Location, Dimension);

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
    public sealed class GridDimensionValue : DeviceConstantDimensionValue, IValueReader
    {
        #region Static

        /// <summary cref="IValueReader.Read(ValueHeader, IIRReader)"/>
        public static Value? Read(ValueHeader header, IIRReader reader)
        {
            var methodBuilder = header.Method?.MethodBuilder;
            if (methodBuilder is not null &&
                header.Block is not null &&
                header.Block.GetOrCreateBuilder(methodBuilder,
                out BasicBlock.Builder? blockBuilder) &&
                reader.Read(out DeviceConstantDimension3D dimension))
            {
                return blockBuilder.CreateGridDimensionValue(
                    Location.Unknown, dimension);
            }
            else
            {
                return null;
            }
        }

        #endregion

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

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GridDimension;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGridDimensionValue(Location, Dimension);

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
    public sealed class GroupDimensionValue : DeviceConstantDimensionValue, IValueReader
    {
        #region Static

        /// <summary cref="IValueReader.Read(ValueHeader, IIRReader)"/>
        public static Value? Read(ValueHeader header, IIRReader reader)
        {
            var methodBuilder = header.Method?.MethodBuilder;
            if (methodBuilder is not null &&
                header.Block is not null &&
                header.Block.GetOrCreateBuilder(methodBuilder,
                out BasicBlock.Builder? blockBuilder) &&
                reader.Read(out DeviceConstantDimension3D dimension))
            {
                return blockBuilder.CreateGroupDimensionValue(
                    Location.Unknown, dimension);
            }
            else
            {
                return null;
            }
        }

        #endregion

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

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GroupDimension;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGroupDimensionValue(Location, Dimension);

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
    public sealed class WarpSizeValue : DeviceConstantValue, IValueReader
    {
        #region Static

        /// <summary cref="IValueReader.Read(ValueHeader, IIRReader)"/>
        public static Value? Read(ValueHeader header, IIRReader reader)
        {
            var methodBuilder = header.Method?.MethodBuilder;
            if (methodBuilder is not null &&
                header.Block is not null &&
                header.Block.GetOrCreateBuilder(methodBuilder,
                out BasicBlock.Builder? blockBuilder))
            {
                return blockBuilder.CreateWarpSizeValue(
                    Location.Unknown);
            }
            else
            {
                return null;
            }
        }

        #endregion

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

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.WarpSize;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateWarpSizeValue(Location);

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer) { }

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
    public sealed class LaneIdxValue : DeviceConstantValue, IValueReader
    {
        #region Static

        /// <summary cref="IValueReader.Read(ValueHeader, IIRReader)"/>
        public static Value? Read(ValueHeader header, IIRReader reader)
        {
            var methodBuilder = header.Method?.MethodBuilder;
            if (methodBuilder is not null &&
                header.Block is not null &&
                header.Block.GetOrCreateBuilder(methodBuilder,
                out BasicBlock.Builder? blockBuilder))
            {
                return blockBuilder.CreateLaneIdxValue(
                    Location.Unknown);
            }
            else
            {
                return null;
            }
        }

        #endregion

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

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.LaneIdx;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateLaneIdxValue(Location);

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer) { }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

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
    [ValueKind(ValueKind.DynamicMemoryLength)]
    public sealed class DynamicMemoryLengthValue : DeviceConstantValue, IValueReader
    {
        #region Static

        /// <summary cref="IValueReader.Read(ValueHeader, IIRReader)"/>
        public static Value? Read(ValueHeader header, IIRReader reader)
        {
            var methodBuilder = header.Method?.MethodBuilder;
            if (methodBuilder is not null &&
                header.Block is not null &&
                header.Block.GetOrCreateBuilder(methodBuilder,
                out BasicBlock.Builder? blockBuilder) &&
                reader.Read(out long elementTypeId) &&
                reader.Read(out MemoryAddressSpace addressSpace))
            {
                return blockBuilder.CreateDynamicMemoryLengthValue(
                    Location.Unknown,
                    reader.Context.Types[elementTypeId],
                    addressSpace);
            }
            else
            {
                return null;
            }
        }

        #endregion

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

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.DynamicMemoryLength;

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

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer)
        {
            writer.Write(nameof(ElementType), ElementType.Id);
            writer.Write(nameof(AddressSpace), AddressSpace);
        }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "dynamicMemLength";

        #endregion
    }
}
