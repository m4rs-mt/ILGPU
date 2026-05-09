// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Threads.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.IR.BasicBlockValues;

/// <summary>
/// Represents a generic barrier operation.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class BarrierOperation(
    in BasicBlockValueInitializer initializer,
    TypeValue type) : MemoryValue(initializer, type)
{
    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "barrier";
}

/// <summary>
/// Represents a predicated synchronization barrier.
/// </summary>
sealed partial class PredicateBarrier : BarrierOperation
{
    /// <summary>
    /// Determines the type of a new predicate barrier operation.
    /// </summary>
    private static PrimitiveType GetType(
        in BasicBlockValueInitializer initializer,
        PredicateBarrierPredicateKind predicateKind)
    {
        var type = predicateKind == PredicateBarrierPredicateKind.PopCount
            ? initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32)
            : initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int1);
        return type;
    }

    /// <summary>
    /// Constructs a new predicate barrier.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="kind">The operation kind.</param>
    /// <param name="predicate">The predicate value.</param>
    /// <param name="predicateKind">The predicate operation kind.</param>
    public PredicateBarrier(
        in BasicBlockValueInitializer initializer,
        PredicateBarrierKind kind,
        Value predicate,
        PredicateBarrierPredicateKind predicateKind)
        : base(initializer, GetType(initializer, predicateKind))
    {
        Location.Assert(predicate.BasicValueType == BasicValueType.Int1);
        Kind = kind;
        PredicateKind = predicateKind;

        Seal(predicate);
    }

    /// <summary>
    /// Returns the barrier predicate.
    /// </summary>
    public Value Predicate => GetValue<Value>(0);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateBarrier(
            Location,
            Kind,
            rewriter.Rewrite(Predicate),
            PredicateKind);

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => $"barrier.{Kind}.{PredicateKind}";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => Predicate.ToString();
}

/// <summary>
/// Represents a synchronization barrier.
/// </summary>
sealed partial class Barrier : BarrierOperation
{
    /// <summary>
    /// Constructs a new barrier.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="barrierKind">The barrier kind.</param>
    public Barrier(
        in BasicBlockValueInitializer initializer,
        BarrierKind barrierKind)
        : base(initializer, initializer.ModuleBuilder.VoidType)
    {
        Kind = barrierKind;

        Seal();
    }

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateBarrier(Location, Kind);
}

/// <summary>
/// Represents a value that is used for communicating values across all threads.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class ThreadValue(in BasicBlockValueInitializer initializer, TypeValue type) :
    BasicBlockValue(initializer, type)
{
    /// <summary>
    /// Returns the variable reference.
    /// </summary>
    public Value Variable => GetValue<Value>(0);

    /// <summary>
    /// Returns true if this communication operation works on intrinsic primitive
    /// types.
    /// </summary>
    public bool IsBuiltIn => BasicValueType >= BasicValueType.Int32;
}

/// <summary>
/// Represents a broadcast operation.
/// </summary>
sealed partial class Broadcast : ThreadValue
{
    /// <summary>
    /// Constructs a new broadcast operation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="variable">The value to broadcast.</param>
    /// <param name="origin">
    /// The source thread index within the group or warp.
    /// </param>
    /// <param name="broadcastKind">The operation kind.</param>
    public Broadcast(
        in BasicBlockValueInitializer initializer,
        Value variable,
        Value origin,
        BroadcastKind broadcastKind)
        : base(initializer, variable.Type)
    {
        Kind = broadcastKind;
        Seal(variable, origin);
    }

    /// <summary>
    /// Returns the thread index origin (group or lane index).
    /// </summary>
    public Value Origin => GetValue<Value>(1);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateBroadcast(
            Location,
            rewriter.Rewrite(Variable),
            rewriter.Rewrite(Origin),
            Kind);

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "broadcast" + Kind.ToString();

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => $"{Variable}, {Origin}";
}

/// <summary>
/// Represents a shuffle operation.
/// </summary>
sealed partial class Shuffle : ThreadValue
{
    /// <summary>
    /// Constructs a new shuffle operation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="variable">The source variable value.</param>
    /// <param name="origin">The shuffle origin.</param>
    /// <param name="kind">The operation kind.</param>
    public Shuffle(
        in BasicBlockValueInitializer initializer,
        Value variable,
        Value origin,
        ShuffleKind kind)
        : base(initializer, variable.Type)
    {
        Kind = kind;
        Seal(variable, origin);
    }

    /// <summary>
    /// Returns the shuffle origin (depends on the operation).
    /// </summary>
    public Value Origin => GetValue<Value>(1);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateShuffle(
            Location,
            rewriter.Rewrite(Variable),
            rewriter.Rewrite(Origin),
            Kind);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "shuffle" + Kind.ToString();

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => $"{Variable}, {Origin}";
}

/// <summary>
/// Represents a warp-wide reduction operation. The binary operation is either:
/// <list type="bullet">
/// <item><b>Intrinsic</b>: a recognized <see cref="BinaryArithmeticKind"/> (Add,
/// Min, Max, etc.) stored directly on the node — the lambda is eliminated.</item>
/// <item><b>Custom</b>: an unrecognized operation stored as a <see cref="Value"/>
/// reference (typically a <see cref="Method"/>) for backend-specific handling.</item>
/// </list>
/// The creation helper automatically infers the intrinsic path when possible.
/// </summary>
sealed partial class WarpReduce : ThreadValue
{
    /// <summary>
    /// Constructs a warp reduce with a recognized intrinsic binary operation.
    /// </summary>
    public WarpReduce(
        in BasicBlockValueInitializer initializer,
        Value data,
        BinaryArithmeticKind intrinsicOp,
        WarpReduceKind kind)
        : base(initializer, data.Type)
    {
        Kind = kind;
        IntrinsicOp = intrinsicOp;
        Seal(data);
    }

    /// <summary>
    /// Constructs a warp reduce with a custom (unrecognized) operation.
    /// </summary>
    public WarpReduce(
        in BasicBlockValueInitializer initializer,
        Value data,
        Value operation,
        WarpReduceKind kind)
        : base(initializer, data.Type)
    {
        Kind = kind;
        IntrinsicOp = null;
        Seal(data, operation);
    }

    /// <summary>
    /// The recognized intrinsic binary operation, or null for custom operations.
    /// </summary>
    public BinaryArithmeticKind? IntrinsicOp { get; }

    /// <summary>
    /// Returns true if this reduction uses a recognized intrinsic operation.
    /// </summary>
    public bool HasIntrinsicOperation => IntrinsicOp.HasValue;

    /// <summary>
    /// The custom operation value (only for non-intrinsic reductions).
    /// </summary>
    public Value? Operation => HasIntrinsicOperation ? null : GetValue<Value>(1);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var data = rewriter.Rewrite(Variable);
        if (HasIntrinsicOperation)
        {
            return rewriter.Builder.CreateWarpReduce(
                Location,
                data,
                IntrinsicOp!.Value,
                Kind);
        }
        return rewriter.Builder.CreateWarpReduce(
            Location,
            data,
            rewriter.Rewrite(Operation.AsNotNull()),
            Kind);
    }

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() =>
        HasIntrinsicOperation
        ? $"warpReduce.{Kind}.{IntrinsicOp}"
        : $"warpReduce.{Kind}.custom";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        HasIntrinsicOperation
        ? Variable.ToString()
        : $"{Variable}, {Operation}";
}

/// <summary>
/// Represents a warp-wide scan (prefix sum) operation. The binary operation
/// follows the same two-tier design as <see cref="WarpReduce"/>: intrinsic
/// operations are stored as a <see cref="BinaryArithmeticKind"/>, custom
/// operations keep a <see cref="Value"/> reference.
/// Exclusive scans require an identity element.
/// </summary>
sealed partial class WarpScan : ThreadValue
{
    /// <summary>
    /// Constructs a warp scan with a recognized intrinsic binary operation.
    /// </summary>
    public WarpScan(
        in BasicBlockValueInitializer initializer,
        Value data,
        BinaryArithmeticKind intrinsicOp,
        WarpScanKind kind,
        Value? identity = null)
        : base(initializer, data.Type)
    {
        Kind = kind;
        IntrinsicOp = intrinsicOp;
        if (identity is not null)
            Seal(data, identity);
        else
            Seal(data);
    }

    /// <summary>
    /// Constructs a warp scan with a custom (unrecognized) operation.
    /// </summary>
    public WarpScan(
        in BasicBlockValueInitializer initializer,
        Value data,
        Value operation,
        WarpScanKind kind,
        Value? identity = null)
        : base(initializer, data.Type)
    {
        Kind = kind;
        IntrinsicOp = null;
        if (identity is not null)
            Seal(data, operation, identity);
        else
            Seal(data, operation);
    }

    /// <summary>
    /// The recognized intrinsic binary operation, or null for custom operations.
    /// </summary>
    public BinaryArithmeticKind? IntrinsicOp { get; }

    /// <summary>
    /// Returns true if this scan uses a recognized intrinsic operation.
    /// </summary>
    public bool HasIntrinsicOperation => IntrinsicOp.HasValue;

    /// <summary>
    /// The custom operation value (only for non-intrinsic scans).
    /// </summary>
    public Value? Operation => HasIntrinsicOperation ? null : GetValue<Value>(1);

    /// <summary>
    /// The identity element for exclusive scans, or null for inclusive scans.
    /// </summary>
    public Value? Identity => Kind != WarpScanKind.Exclusive ? null
        : HasIntrinsicOperation ? GetValue<Value>(1) : GetValue<Value>(2);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var data = rewriter.Rewrite(Variable);
        var identity = Identity is not null ? rewriter.Rewrite(Identity) : null;

        if (HasIntrinsicOperation)
        {
            return rewriter.Builder.CreateWarpScan(
                Location,
                data,
                IntrinsicOp!.Value,
                Kind,
                identity);
        }

        return rewriter.Builder.CreateWarpScan(
            Location,
            data,
            rewriter.Rewrite(Operation.AsNotNull()),
            Kind,
            identity);
    }

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() =>
        HasIntrinsicOperation
        ? $"warpScan.{Kind}.{IntrinsicOp}"
        : $"warpScan.{Kind}.custom";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString()
    {
        var args = HasIntrinsicOperation
            ? Variable.ToString()
            : $"{Variable}, {Operation}";
        return Identity is not null ? $"{args}, identity={Identity}" : args;
    }
}

/// <summary>
/// Represents a group-wide (workgroup/threadgroup) reduction operation.
/// Group reductions always require an identity element for shared memory
/// initialization. All backends lower these to shared memory + warp reduce
/// + barrier sequences.
/// </summary>
sealed partial class GroupReduce : ThreadValue
{
    /// <summary>
    /// Constructs a group reduce with a recognized intrinsic binary operation.
    /// </summary>
    public GroupReduce(
        in BasicBlockValueInitializer initializer,
        Value data,
        BinaryArithmeticKind intrinsicOp,
        GroupReduceKind kind,
        Value identity)
        : base(initializer, data.Type)
    {
        Kind = kind;
        IntrinsicOp = intrinsicOp;
        Seal(data, identity);
    }

    /// <summary>
    /// Constructs a group reduce with a custom (unrecognized) operation.
    /// </summary>
    public GroupReduce(
        in BasicBlockValueInitializer initializer,
        Value data,
        Value operation,
        GroupReduceKind kind,
        Value identity)
        : base(initializer, data.Type)
    {
        Kind = kind;
        IntrinsicOp = null;
        Seal(data, operation, identity);
    }

    /// <summary>
    /// The recognized intrinsic binary operation, or null for custom operations.
    /// </summary>
    public BinaryArithmeticKind? IntrinsicOp { get; }

    /// <summary>
    /// Returns true if this reduction uses a recognized intrinsic operation.
    /// </summary>
    public bool HasIntrinsicOperation => IntrinsicOp.HasValue;

    /// <summary>
    /// The custom operation value (only for non-intrinsic reductions).
    /// </summary>
    public Value? Operation => HasIntrinsicOperation ? null : GetValue<Value>(1);

    /// <summary>
    /// The identity element for the operation (used to initialize shared memory).
    /// </summary>
    public Value Identity => HasIntrinsicOperation
        ? GetValue<Value>(1)
        : GetValue<Value>(2);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var data = rewriter.Rewrite(Variable);
        var identity = rewriter.Rewrite(Identity);
        if (HasIntrinsicOperation)
        {
            return rewriter.Builder.CreateGroupReduce(
                Location,
                data,
                IntrinsicOp!.Value,
                Kind,
                identity);
        }
        return rewriter.Builder.CreateGroupReduce(
            Location,
            data,
            rewriter.Rewrite(Operation.AsNotNull()),
            Kind,
            identity);
    }

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() =>
        HasIntrinsicOperation
        ? $"groupReduce.{Kind}.{IntrinsicOp}"
        : $"groupReduce.{Kind}.custom";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        HasIntrinsicOperation
        ? $"{Variable}, identity={Identity}"
        : $"{Variable}, {Operation}, identity={Identity}";
}

/// <summary>
/// Represents a group-wide scan (prefix sum) operation.
/// Group scans always require an identity element.
/// </summary>
sealed partial class GroupScan : ThreadValue
{
    /// <summary>
    /// Constructs a group scan with a recognized intrinsic binary operation.
    /// </summary>
    public GroupScan(
        in BasicBlockValueInitializer initializer,
        Value data,
        BinaryArithmeticKind intrinsicOp,
        GroupScanKind kind,
        Value identity)
        : base(initializer, data.Type)
    {
        Kind = kind;
        IntrinsicOp = intrinsicOp;
        Seal(data, identity);
    }

    /// <summary>
    /// Constructs a group scan with a custom (unrecognized) operation.
    /// </summary>
    public GroupScan(
        in BasicBlockValueInitializer initializer,
        Value data,
        Value operation,
        GroupScanKind kind,
        Value identity)
        : base(initializer, data.Type)
    {
        Kind = kind;
        IntrinsicOp = null;
        Seal(data, operation, identity);
    }

    /// <summary>
    /// The recognized intrinsic binary operation, or null for custom operations.
    /// </summary>
    public BinaryArithmeticKind? IntrinsicOp { get; }

    /// <summary>
    /// Returns true if this scan uses a recognized intrinsic operation.
    /// </summary>
    public bool HasIntrinsicOperation => IntrinsicOp.HasValue;

    /// <summary>
    /// The custom operation value (only for non-intrinsic scans).
    /// </summary>
    public Value? Operation => HasIntrinsicOperation ? null : GetValue<Value>(1);

    /// <summary>
    /// The identity element for the operation.
    /// </summary>
    public Value Identity => HasIntrinsicOperation
        ? GetValue<Value>(1)
        : GetValue<Value>(2);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var data = rewriter.Rewrite(Variable);
        var identity = rewriter.Rewrite(Identity);
        if (HasIntrinsicOperation)
        {
            return rewriter.Builder.CreateGroupScan(
                Location,
                data,
                IntrinsicOp!.Value,
                Kind,
                identity);
        }
        return rewriter.Builder.CreateGroupScan(
            Location,
            data,
            rewriter.Rewrite(Operation.AsNotNull()),
            Kind,
            identity);
    }

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() =>
        HasIntrinsicOperation
        ? $"groupScan.{Kind}.{IntrinsicOp}"
        : $"groupScan.{Kind}.custom";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        HasIntrinsicOperation
        ? $"{Variable}, identity={Identity}"
        : $"{Variable}, {Operation}, identity={Identity}";
}

/// <summary>
/// Represents a warp-wide radix sort operation.
/// The operation is defined by an IRadixSortOperation type parameter
/// whose members are stored as: NumBits (compile-time int), DefaultValue
/// (identity Value), and ExtractRadixBits (Method reference).
/// </summary>
sealed partial class WarpRadixSort : ThreadValue
{
    /// <summary>
    /// Constructs a new warp radix sort value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="data">The data value to sort.</param>
    /// <param name="numBits">The number of bits to sort.</param>
    /// <param name="defaultValue">The default/padding value.</param>
    /// <param name="extractRadixBitsMethod">
    /// The ExtractRadixBits method reference.
    /// </param>
    public WarpRadixSort(
        in BasicBlockValueInitializer initializer,
        Value data,
        int numBits,
        Value defaultValue,
        Value extractRadixBitsMethod)
        : base(initializer, data.Type)
    {
        NumBits = numBits;
        Seal(data, defaultValue, extractRadixBitsMethod);
    }

    /// <summary>
    /// The number of bits to sort (compile-time constant from
    /// IRadixSortOperation.NumBits).
    /// </summary>
    public int NumBits { get; }

    /// <summary>
    /// The default/identity value for padding (from
    /// IRadixSortOperation.DefaultValue).
    /// </summary>
    public Value DefaultValue => GetValue<Value>(1);

    /// <summary>
    /// The ExtractRadixBits method reference.
    /// </summary>
    public Value ExtractRadixBitsMethod => GetValue<Value>(2);

    /// <inheritdoc cref="BasicBlockValue.Rewrite{TRewriter}(in TRewriter)"/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateWarpRadixSort(
            Location,
            rewriter.Rewrite(Variable),
            NumBits,
            rewriter.Rewrite(DefaultValue),
            rewriter.Rewrite(ExtractRadixBitsMethod));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() =>
        $"warpRadixSort.{NumBits}bits";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{Variable}, default={DefaultValue}, extract={ExtractRadixBitsMethod}";
}

/// <summary>
/// Represents a group-wide radix sort operation.
/// The operation is defined by an IRadixSortOperation type parameter
/// whose members are stored as: NumBits (compile-time int), DefaultValue
/// (identity Value), and ExtractRadixBits (Method reference).
/// </summary>
sealed partial class GroupRadixSort : ThreadValue
{
    /// <summary>
    /// Constructs a new group radix sort value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="data">The data value to sort.</param>
    /// <param name="numBits">The number of bits to sort.</param>
    /// <param name="defaultValue">The default/padding value.</param>
    /// <param name="extractRadixBitsMethod">
    /// The ExtractRadixBits method reference.
    /// </param>
    public GroupRadixSort(
        in BasicBlockValueInitializer initializer,
        Value data,
        int numBits,
        Value defaultValue,
        Value extractRadixBitsMethod)
        : base(initializer, data.Type)
    {
        NumBits = numBits;
        Seal(data, defaultValue, extractRadixBitsMethod);
    }

    /// <summary>
    /// The number of bits to sort (compile-time constant from
    /// IRadixSortOperation.NumBits).
    /// </summary>
    public int NumBits { get; }

    /// <summary>
    /// The default/identity value for padding (from
    /// IRadixSortOperation.DefaultValue).
    /// </summary>
    public Value DefaultValue => GetValue<Value>(1);

    /// <summary>
    /// The ExtractRadixBits method reference.
    /// </summary>
    public Value ExtractRadixBitsMethod => GetValue<Value>(2);

    /// <inheritdoc cref="BasicBlockValue.Rewrite{TRewriter}(in TRewriter)"/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateGroupRadixSort(
            Location,
            rewriter.Rewrite(Variable),
            NumBits,
            rewriter.Rewrite(DefaultValue),
            rewriter.Rewrite(ExtractRadixBitsMethod));

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() =>
        $"groupRadixSort.{NumBits}bits";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{Variable}, default={DefaultValue}, extract={ExtractRadixBitsMethod}";
}
