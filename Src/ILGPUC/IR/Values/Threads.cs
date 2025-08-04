// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Threads.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Construction;
using ILGPUC.IR.Types;

namespace ILGPUC.IR.Values;

/// <summary>
/// Represents a generic barrier operation.
/// </summary>
abstract class BarrierOperation : MemoryValue
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
/// Represents a predicated synchronization barrier.
/// </summary>
sealed partial class PredicateBarrier : BarrierOperation
{
    #region Instance

    /// <summary>
    /// Constructs a new predicate barrier.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="kind">The operation kind.</param>
    /// <param name="predicate">The predicate value.</param>
    /// <param name="predicateKind">The predicate operation kind.</param>
    internal PredicateBarrier(
        in ValueInitializer initializer,
        PredicateBarrierKind kind,
        ValueReference predicate,
        PredicateBarrierPredicateKind predicateKind)
        : base(initializer)
    {
        Location.Assert(predicate.BasicValueType == BasicValueType.Int1);
        Kind = kind;
        PredicateKind = predicateKind;
        Seal(predicate);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the barrier predicate.
    /// </summary>
    public ValueReference Predicate => this[0];

    #endregion

    #region Methods

    /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
    protected override TypeNode ComputeType(in ValueInitializer initializer) =>
        PredicateKind == PredicateBarrierPredicateKind.PopCount
        ? initializer.Context.GetPrimitiveType(BasicValueType.Int32)
        : initializer.Context.GetPrimitiveType(BasicValueType.Int1);

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateBarrier(
            Location,
            Kind,
            rebuilder.Rebuild(Predicate),
            PredicateKind);

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() =>
        $"barrier.{Kind}.{PredicateKind}";

    /// <summary cref="Value.ToArgString"/>
    protected override string ToArgString() => Predicate.ToString();

    #endregion
}

/// <summary>
/// Represents a synchronization barrier.
/// </summary>
sealed partial class Barrier : BarrierOperation
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

    #region Methods

    /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
    protected override TypeNode ComputeType(in ValueInitializer initializer) =>
        initializer.Context.VoidType;

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateBarrier(Location, Kind);

    #endregion
}

/// <summary>
/// Represents a value that is used for communicating values across all threads.
/// </summary>
abstract class ThreadValue : ControlFlowValue
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
sealed partial class Broadcast : ThreadValue
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

    /// <summary>
    /// Returns the thread index origin (group or lane index).
    /// </summary>
    public ValueReference Origin => this[1];

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

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "broadcast" + Kind.ToString();

    /// <summary cref="Value.ToArgString"/>
    protected override string ToArgString() => $"{Variable}, {Origin}";

    #endregion
}


/// <summary>
/// Represents a shuffle operation.
/// </summary>
sealed partial class WarpShuffle : ThreadValue
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
        : base(initializer)
    {
        Kind = kind;
        Seal(variable, origin);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the shuffle origin (depends on the operation).
    /// </summary>
    public ValueReference Origin => this[1];

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

    #endregion

    #region Object

    /// <summary cref="Value.ToArgString"/>
    protected override string ToArgString() => $"{Variable}, {Origin}";

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "shuffle" + Kind.ToString();

    #endregion
}
