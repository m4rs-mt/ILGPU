// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Threads.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a generic barrier operation.
    /// </summary>
    public abstract class BarrierOperation : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new generic barrier operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        internal BarrierOperation(in ValueInitializer initializer)
            : base(initializer)
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
    [ValueKind(ValueKind.PredicateBarrier)]
    public sealed class PredicateBarrier : BarrierOperation
    {
        #region Instance

        /// <summary>
        /// Constructs a new predicate barrier.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="predicate">The predicate value.</param>
        /// <param name="kind">The operation kind.</param>
        internal PredicateBarrier(
            in ValueInitializer initializer,
            ValueReference predicate,
            PredicateBarrierKind kind)
            : base(initializer)
        {
            Location.Assert(predicate.BasicValueType == BasicValueType.Int1);
            Kind = kind;
            Seal(predicate);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.PredicateBarrier;

        /// <summary>
        /// Returns the barrier predicate.
        /// </summary>
        public ValueReference Predicate => this[0];

        /// <summary>
        /// Returns the kind of the barrier operation.
        /// </summary>
        public PredicateBarrierKind Kind { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            Kind == PredicateBarrierKind.PopCount
                ? initializer.Context.GetPrimitiveType(BasicValueType.Int32)
                : initializer.Context.GetPrimitiveType(BasicValueType.Int1);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateBarrier(
                Location,
                rebuilder.Rebuild(Predicate),
                Kind);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

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
    [ValueKind(ValueKind.Barrier)]
    public sealed class Barrier : BarrierOperation
    {
        #region Instance

        /// <summary>
        /// Constructs a new barrier.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="barrierKind">The barrier kind.</param>
        internal Barrier(
            in ValueInitializer initializer,
            BarrierKind barrierKind)
            : base(initializer)
        {
            Kind = barrierKind;
            Seal();
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Barrier;

        /// <summary>
        /// Return the associated barrier kind.
        /// </summary>
        public BarrierKind Kind { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.VoidType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateBarrier(Location, Kind);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion
    }

    /// <summary>
    /// Represents the kind of a broadcast operation.
    /// </summary>
    public enum BroadcastKind
    {
        /// <summary>
        /// A broadcast operation that operates on warp level.
        /// </summary>
        WarpLevel,

        /// <summary>
        /// A broadcast operation that operates on group level.
        /// </summary>
        GroupLevel
    }

    /// <summary>
    /// Represents a value that is used for communicating values across all threads.
    /// </summary>
    public abstract class ThreadValue : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new communication operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        internal ThreadValue(in ValueInitializer initializer)
            : base(initializer)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the variable reference.
        /// </summary>
        public ValueReference Variable => this[0];

        /// <summary>
        /// Returns true if this communication operation works on intrinsic primitive
        /// types.
        /// </summary>
        public bool IsBuiltIn => BasicValueType >= BasicValueType.Int32;

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            Variable.Type;

        #endregion
    }

    /// <summary>
    /// Represents a broadcast operation.
    /// </summary>
    [ValueKind(ValueKind.Broadcast)]
    public sealed class Broadcast : ThreadValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new broadcast operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="value">The value to broadcast.</param>
        /// <param name="origin">
        /// The source thread index within the group or warp.
        /// </param>
        /// <param name="broadcastKind">The operation kind.</param>
        internal Broadcast(
            in ValueInitializer initializer,
            ValueReference value,
            ValueReference origin,
            BroadcastKind broadcastKind)
            : base(initializer)
        {
            Kind = broadcastKind;
            Seal(value, origin);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Broadcast;

        /// <summary>
        /// Returns the thread index origin (group or lane index).
        /// </summary>
        public ValueReference Origin => this[1];

        /// <summary>
        /// Returns the kind of the broadcast operation.
        /// </summary>
        public BroadcastKind Kind { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateBroadcast(
                Location,
                rebuilder.Rebuild(Variable),
                rebuilder.Rebuild(Origin),
                Kind);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "broadcast" + Kind.ToString();

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Variable}, {Origin}";

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
    public abstract class ShuffleOperation : ThreadValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new shuffle operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="shuffleKind">The operation kind.</param>
        internal ShuffleOperation(
            in ValueInitializer initializer,
            ShuffleKind shuffleKind)
            : base(initializer)
        {
            Kind = shuffleKind;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the shuffle origin (depends on the operation).
        /// </summary>
        public ValueReference Origin => this[1];

        /// <summary>
        /// Returns the kind of the shuffle operation.
        /// </summary>
        public ShuffleKind Kind { get; }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "shuffle" + Kind.ToString();

        #endregion
    }

    /// <summary>
    /// Represents a shuffle operation.
    /// </summary>
    [ValueKind(ValueKind.WarpShuffle)]
    public sealed class WarpShuffle : ShuffleOperation
    {
        #region Instance

        /// <summary>
        /// Constructs a new shuffle operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="variable">The source variable value.</param>
        /// <param name="origin">The shuffle origin.</param>
        /// <param name="kind">The operation kind.</param>
        internal WarpShuffle(
            in ValueInitializer initializer,
            ValueReference variable,
            ValueReference origin,
            ShuffleKind kind)
            : base(initializer, kind)
        {
            Seal(variable, origin);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.WarpShuffle;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateShuffle(
                Location,
                rebuilder.Rebuild(Variable),
                rebuilder.Rebuild(Origin),
                Kind);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Variable}, {Origin}";

        #endregion
    }

    /// <summary>
    /// Represents an sub-warp shuffle operation.
    /// </summary>
    [ValueKind(ValueKind.SubWarpShuffle)]
    public sealed class SubWarpShuffle : ShuffleOperation
    {
        #region Instance

        /// <summary>
        /// Constructs a new shuffle operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="variable">The source variable value.</param>
        /// <param name="origin">The shuffle origin.</param>
        /// <param name="width">The sub-warp width.</param>
        /// <param name="kind">The operation kind.</param>
        internal SubWarpShuffle(
            in ValueInitializer initializer,
            ValueReference variable,
            ValueReference origin,
            ValueReference width,
            ShuffleKind kind)
            : base(initializer, kind)
        {
            Seal(variable, origin, width);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.SubWarpShuffle;

        /// <summary>
        /// Returns the intra-warp width.
        /// </summary>
        public ValueReference Width => this[2];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateShuffle(
                Location,
                rebuilder.Rebuild(Variable),
                rebuilder.Rebuild(Origin),
                rebuilder.Rebuild(Width),
                Kind);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Variable}, {Origin} [{Width}]";

        #endregion
    }
}
