// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Threads.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a device constant inside a kernel.
    /// </summary>
    public abstract class DeviceConstantValue : InstantiatedConstantNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="type">The constant type.</param>
        internal DeviceConstantValue(ValueGeneration generation, TypeNode type)
            : base(generation, type)
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
        /// <param name="generation">The current generation.</param>
        /// <param name="type">The constant type.</param>
        /// <param name="dimension">The device constant dimension.</param>
        internal DeviceConstantDimensionValue(
            ValueGeneration generation,
            TypeNode type,
            DeviceConstantDimension3D dimension)
            : base(generation, type)
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

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            if (obj is DeviceConstantDimensionValue dimValue)
                return dimValue.Dimension == Dimension;
            return false;
        }

        /// <summary cref="UnifiedValue.GetHashCode"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ (int)Dimension;
        }

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
        /// <param name="generation">The current generation.</param>
        /// <param name="dimension">The constant dimension.</param>
        /// <param name="intType">The default integer type.</param>
        internal GridDimensionValue(
            ValueGeneration generation,
            DeviceConstantDimension3D dimension,
            PrimitiveType intType)
            : base(generation, intType, dimension)
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateGridDimensionValue(Dimension);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            return obj is GridDimensionValue &&
                base.Equals(obj);
        }

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0x2008513C;
        }

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
        /// <param name="generation">The current generation.</param>
        /// <param name="dimension">The constant dimension.</param>
        /// <param name="intType">The default integer type.</param>
        internal GroupDimensionValue(
            ValueGeneration generation,
            DeviceConstantDimension3D dimension,
            PrimitiveType intType)
            : base(generation, intType, dimension)
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateGroupDimensionValue(Dimension);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            return obj is GroupDimensionValue &&
                base.Equals(obj);
        }

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0x4DB4E85A;
        }

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
        /// <param name="generation">The current generation.</param>
        /// <param name="intType">The default integer type.</param>
        internal WarpSizeValue(
            ValueGeneration generation,
            PrimitiveType intType)
            : base(generation, intType)
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateWarpSizeValue();

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            return obj is WarpSizeValue;
        }

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0x3C2EBBC8;
        }

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
        /// <param name="generation">The current generation.</param>
        /// <param name="intType">The default integer type.</param>
        internal LaneIdxValue(
            ValueGeneration generation,
            PrimitiveType intType)
            : base(generation, intType)
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateLaneIdxValue();

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            return obj is LaneIdxValue;
        }

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0x148F123B;
        }

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "laneIdx";

        #endregion
    }

    /// <summary>
    /// Represents a generic barrier operation.
    /// </summary>
    public abstract class BarrierOperation : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new generic barrier operation.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="parent">The parent memory value.</param>
        /// <param name="values">Additional values.</param>
        /// <param name="type">The operation type.</param>
        internal BarrierOperation(
            ValueGeneration generation,
            ValueReference parent,
            ImmutableArray<ValueReference> values,
            TypeNode type)
            : base(generation, parent, values, type)
        { }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "barrier";

        #endregion
    }

    /// <summary>
    /// Represents a predicate-barrier kind.
    /// </summary>
    public enum PredicateBarrierKind
    {
        /// <summary>
        /// Returns the number of threads in the group
        /// for which the predicate evaluates to true.
        /// </summary>
        PopCount,

        /// <summary>
        /// Returns the logical and result of the predicate
        /// of all threads in the group.
        /// </summary>
        And,

        /// <summary>
        /// Returns the logical or result of the predicate
        /// of all threads in the group.
        /// </summary>
        Or,
    }

    /// <summary>
    /// Represents a predicated synchronization barrier.
    /// </summary>
    public sealed class PredicateBarrier : BarrierOperation
    {
        #region Instance

        /// <summary>
        /// Constructs a new predicate barrier.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="parent">The parent memory value.</param>
        /// <param name="predicate">The predicate value.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="intType">The default integer type.</param>
        internal PredicateBarrier(
            ValueGeneration generation,
            ValueReference parent,
            ValueReference predicate,
            PredicateBarrierKind kind,
            PrimitiveType intType)
            : base(
                  generation,
                  parent,
                  ImmutableArray.Create(predicate),
                  intType)
        {
            Debug.Assert(
                predicate.BasicValueType == BasicValueType.Int1,
                "Invalid predicate");
            Kind = kind;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the barrier predicate.
        /// </summary>
        public ValueReference Predicate => this[1];

        /// <summary>
        /// Returns the kind of the barrier operation.
        /// </summary>
        public PredicateBarrierKind Kind { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateBarrier(
                rebuilder.RebuildAs<MemoryRef>(Parent),
                rebuilder.Rebuild(Predicate),
                Kind);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "barrier." + Kind.ToString();

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => Predicate.ToString();

        #endregion
    }

    /// <summary>
    /// Represents a barrier kind.
    /// </summary>
    public enum BarrierKind
    {
        /// <summary>
        /// A barrier that operates on warp level.
        /// </summary>
        WarpLevel,

        /// <summary>
        /// A barrier that operates on group level.
        /// </summary>
        GroupLevel
    }

    /// <summary>
    /// Represents a synchronization barrier.
    /// </summary>
    public sealed class Barrier : BarrierOperation
    {
        #region Instance

        /// <summary>
        /// Constructs a new barrier.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="parent">The parent memory value.</param>
        /// <param name="barrierKind">The barrier kind.</param>
        /// <param name="voidType">The void type.</param>
        internal Barrier(
            ValueGeneration generation,
            ValueReference parent,
            BarrierKind barrierKind,
            VoidType voidType)
            : base(
                  generation,
                  parent,
                  ImmutableArray<ValueReference>.Empty,
                  voidType)
        {
            Kind = barrierKind;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Return the associated barrier kind.
        /// </summary>
        public BarrierKind Kind { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateBarrier(
                rebuilder.RebuildAs<MemoryRef>(Parent),
                Kind);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion
    }

    /// <summary>
    /// Represents the kind of a shuffle operation.
    /// </summary>
    public enum ShuffleKind
    {
        /// <summary>
        /// A generic shuffle operation.
        /// </summary>
        Generic,

        /// <summary>
        /// A down-shuffle operation.
        /// </summary>
        Down,

        /// <summary>
        /// An up-shuffle operation.
        /// </summary>
        Up,

        /// <summary>
        /// A xor-shuffle operation.
        /// </summary>
        Xor
    }

    /// <summary>
    /// Represents a shuffle operation.
    /// </summary>
    public sealed class Shuffle : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new shuffle operation.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="parent">The parent memory value.</param>
        /// <param name="variable">The source variable value.</param>
        /// <param name="origin">The shuffle origin.</param>
        /// <param name="kind">The operation kind.</param>
        internal Shuffle(
            ValueGeneration generation,
            ValueReference parent,
            ValueReference variable,
            ValueReference origin,
            ShuffleKind kind)
            : base(
                  generation,
                  parent,
                  ImmutableArray.Create(variable, origin),
                  variable.Type)
        {
            Kind = kind;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the variable reference.
        /// </summary>
        public ValueReference Variable => this[1];

        /// <summary>
        /// Returns the shuffle origin (depends on the operation).
        /// </summary>
        public ValueReference Origin => this[2];

        /// <summary>
        /// Returns the kind of the shuffle operation.
        /// </summary>
        public ShuffleKind Kind { get; }

        /// <summary cref="Value.Type"/>
        public override TypeNode Type => Variable.Type;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateShuffle(
                rebuilder.RebuildAs<MemoryRef>(Parent),
                rebuilder.Rebuild(Variable),
                rebuilder.Rebuild(Origin),
                Kind);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            if (obj is Shuffle shuffle)
                return shuffle.Kind == Kind &&
                        base.Equals(obj);
            return false;
        }

        /// <summary cref="UnifiedValue.GetHashCode"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ (int)Kind;
        }

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "shuffle" + Kind.ToString();

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Variable}, {Origin}";

        #endregion
    }
}
