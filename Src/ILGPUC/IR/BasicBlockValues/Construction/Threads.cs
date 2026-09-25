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

using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.IR.BasicBlockValues.Construction;

partial class BasicBlockBuilder
{
    /// <summary>
    /// Creates a new predicated barrier.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="kind">The barrier kind.</param>
    /// <param name="predicate">The barrier predicate.</param>
    /// <param name="predicateKind">The predicate barrier kind.</param>
    /// <returns>A node that represents the barrier.</returns>
    public BarrierOperation? CreateBarrier(
        Location location,
        PredicateBarrierKind kind,
        Value? predicate,
        PredicateBarrierPredicateKind predicateKind)
    {
        if (predicate is null) return null;

        location.Assert(predicate.BasicValueType == BasicValueType.Int1);

        return Append(new PredicateBarrier(
            GetInitializer(location),
            kind,
            predicate,
            predicateKind));
    }

    /// <summary>
    /// Creates a new barrier.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="kind">The barrier kind.</param>
    /// <returns>A node that represents the barrier.</returns>
    public MemoryValue CreateBarrier(
        Location location,
        BarrierKind kind) =>
        Append(new Barrier(
            GetInitializer(location),
            kind));

    /// <summary>
    /// Returns true if the given variable is a constant with respect to a broadcast
    /// or shuffle value operating on the warp or the group level.
    /// </summary>
    /// <param name="variable">The variable to test.</param>
    /// <param name="groupLevel">
    /// True for operations across a group, false for operations across a warp.
    /// </param>
    /// <returns>
    /// True, if the given variable is a constant in the parent broadcast or shuffle
    /// context.
    /// </returns>
    private static bool IsShuffleOrBroadcastConstant(Value variable, bool groupLevel) =>
        variable switch
        {
            // Entry-point parameters can be considered uniform, except for the
            // first one, which may be the index of the thread
            Parameter param => param.Method.IsEntryPoint && param.Index > 0,
            PrimitiveValue _ => true,
            GridDimensionValue _ => true,
            GroupDimensionValue _ => true,
            SubGroupDimensionValue _ => true,
            GridIndexValue _ => true,
            // The warp index is uniform within a warp but differs within a group
            SubGroupIndexValue _ => !groupLevel,
            UnaryArithmeticValue unary =>
                IsShuffleOrBroadcastConstant(unary.Value, groupLevel),
            BinaryArithmeticValue binary =>
                IsShuffleOrBroadcastConstant(binary.Left, groupLevel) &&
                IsShuffleOrBroadcastConstant(binary.Right, groupLevel),
            TernaryArithmeticValue ternary =>
                IsShuffleOrBroadcastConstant(ternary.First, groupLevel) &&
                IsShuffleOrBroadcastConstant(ternary.Second, groupLevel) &&
                IsShuffleOrBroadcastConstant(ternary.Third, groupLevel),
            _ => false
        };

    /// <summary>
    /// Creates a new broadcast operation.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="variable">The variable.</param>
    /// <param name="origin">
    /// The broadcast origin (thread index within a group or a warp).
    /// </param>
    /// <param name="kind">The operation kind.</param>
    /// <returns>A node that represents the broadcast operation.</returns>
    public Value? CreateBroadcast(
        Location location,
        Value? variable,
        Value? origin,
        BroadcastKind kind)
    {
        if (variable is null || origin is null) return null;

        return IsShuffleOrBroadcastConstant(
                variable,
                groupLevel: kind == BroadcastKind.GroupLevel)
            ? variable
            : Append(new Broadcast(
                GetInitializer(location),
                variable,
                origin,
                kind));
    }

    /// <summary>
    /// Creates a new shuffle operation involving all lanes of a warp.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="variable">The variable.</param>
    /// <param name="origin">The shuffle origin (depends on the operation).</param>
    /// <param name="kind">The operation kind.</param>
    /// <returns>A node that represents the shuffle operation.</returns>
    public Value? CreateShuffle(
        Location location,
        Value? variable,
        Value? origin,
        ShuffleKind kind)
    {
        if (variable is null || origin is null) return null;

        return IsShuffleOrBroadcastConstant(variable, groupLevel: false)
            ? variable
            : Append(new Shuffle(
                GetInitializer(location),
                variable,
                origin,
                kind));
    }

    /// <summary>
    /// Attempts to recognize the <see cref="BinaryArithmeticKind"/> from the
    /// operation of a collective, which is the <see cref="Method"/> of its lambda
    /// or method group: the frontend creates every collective with that method,
    /// and every rebuild of a collective keeps a method.
    /// </summary>
    /// <remarks>
    /// A recognized collective computes the operation and nothing else, so only an
    /// operation that is exactly what the method computes is recognized; any other
    /// method keeps the custom operation, which the lowering rejects with an error.
    /// </remarks>
    /// <param name="operation">The operation value to inspect.</param>
    /// <returns>The recognized kind, or null if unrecognized.</returns>
    internal static BinaryArithmeticKind? TryRecognizeOperation(Value? operation)
    {
        // The operation is a Method whose compiled body returns a binary
        // arithmetic of its parameters. Note: do NOT check NumParameters here —
        // during module rebuilds the Method reference may point to a fresh
        // declaration whose parameters have not yet been populated, while the
        // underlying compiled body still exists.
        if (operation is not Method method || !method.HasImplementation)
            return null;
        return TryRecognizeMethodBody(method)?.Kind;
    }

    /// <summary>
    /// Returns true if a collective can compute the given operation, which is
    /// associative and commutative.
    /// </summary>
    private static bool IsCollectiveOperation(BinaryArithmeticKind kind) =>
        kind is BinaryArithmeticKind.Add
            or BinaryArithmeticKind.Mul
            or BinaryArithmeticKind.And
            or BinaryArithmeticKind.Or
            or BinaryArithmeticKind.Xor
            or BinaryArithmeticKind.Min
            or BinaryArithmeticKind.Max;

    /// <summary>
    /// Inspects a method body for a binary arithmetic return value: the method
    /// must return the operation applied to two of its parameters and do nothing
    /// else, as <c>(a, b) => a + b</c> or <c>Math.Max</c> do. A body such as
    /// <c>(a, b) => a + b + 1</c>, one with side effects, or one that computes the
    /// operation on converted parameters is not recognized. Returns null if the
    /// method body is not yet compiled (Blocks collection uninitialized).
    /// </summary>
    private static BinaryArithmeticValue? TryRecognizeMethodBody(Method method)
    {
        try
        {
            if (method.ExitBlock?.TerminationValue is not BinaryArithmeticValue arith
                || !IsCollectiveOperation(arith.Kind)
                || arith.Left is not Parameter left
                || arith.Right is not Parameter right
                || left == right
                || left.Method != method
                || right.Method != method)
            {
                return null;
            }

            // The IL frontend may create entry + exit blocks even for trivial
            // lambda bodies; none of them may hold a value with an effect
            foreach (var block in method.Blocks)
            {
                if (block.FirstValue is not null)
                    return null;
            }
            return arith;
        }
        catch (System.InvalidOperationException)
        {
            // Blocks collection not yet initialized (method body not compiled)
            return null;
        }
    }

    /// <summary>
    /// Returns true if the recognized operation is idempotent:
    /// f(x, x) == x for all x (Min, Max, And, Or).
    /// </summary>
    private static bool IsIdempotentOperation(BinaryArithmeticKind? kind) =>
        kind is BinaryArithmeticKind.Min
            or BinaryArithmeticKind.Max
            or BinaryArithmeticKind.And
            or BinaryArithmeticKind.Or;

    /// <summary>
    /// Creates a warp reduce with a known intrinsic binary operation.
    /// </summary>
    public Value? CreateWarpReduce(
        Location location,
        Value? data,
        BinaryArithmeticKind intrinsicOp,
        WarpReduceKind kind)
    {
        if (data is null) return null;

        if (IsShuffleOrBroadcastConstant(data, groupLevel: false)
            && IsIdempotentOperation(intrinsicOp))
        {
            return data;
        }

        return Append(new WarpReduce(
            GetInitializer(location),
            data,
            intrinsicOp,
            kind));
    }

    /// <summary>
    /// Creates a warp reduce from an operation value. Automatically infers
    /// a <see cref="BinaryArithmeticKind"/> when the operation is a recognized
    /// primitive, eliminating the lambda. Falls back to a custom operation
    /// reference otherwise.
    /// </summary>
    public Value? CreateWarpReduce(
        Location location,
        Value? data,
        Value? operation,
        WarpReduceKind kind)
    {
        if (data is null || operation is null) return null;

        // Try to infer a primitive binary operation
        var intrinsicOp = TryRecognizeOperation(operation);
        if (intrinsicOp.HasValue)
            return CreateWarpReduce(location, data, intrinsicOp.Value, kind);

        // Custom operation — keep the value reference
        return Append(new WarpReduce(
            GetInitializer(location),
            data,
            operation,
            kind));
    }

    /// <summary>
    /// Creates a warp scan with a known intrinsic binary operation.
    /// </summary>
    public Value? CreateWarpScan(
        Location location,
        Value? data,
        BinaryArithmeticKind intrinsicOp,
        WarpScanKind kind,
        Value? identity = null)
    {
        if (data is null) return null;

        if (kind == WarpScanKind.Inclusive
            && IsShuffleOrBroadcastConstant(data, groupLevel: false)
            && IsIdempotentOperation(intrinsicOp))
        {
            return data;
        }

        return Append(new WarpScan(
            GetInitializer(location),
            data,
            intrinsicOp,
            kind,
            identity));
    }

    /// <summary>
    /// Creates a warp scan from an operation value. Automatically infers
    /// a <see cref="BinaryArithmeticKind"/> when the operation is a recognized
    /// primitive, eliminating the lambda. Falls back to a custom operation
    /// reference otherwise.
    /// </summary>
    public Value? CreateWarpScan(
        Location location,
        Value? data,
        Value? operation,
        WarpScanKind kind,
        Value? identity = null)
    {
        if (data is null || operation is null) return null;

        // Try to infer a primitive binary operation
        var intrinsicOp = TryRecognizeOperation(operation);
        if (intrinsicOp.HasValue)
            return CreateWarpScan(location, data, intrinsicOp.Value, kind, identity);

        // Custom operation — keep the value reference
        return Append(new WarpScan(
            GetInitializer(location),
            data,
            operation,
            kind,
            identity));
    }

    // ---- Group-level collectives ----

    /// <summary>
    /// Creates a group reduce with a known intrinsic binary operation.
    /// </summary>
    public Value? CreateGroupReduce(
        Location location,
        Value? data,
        BinaryArithmeticKind intrinsicOp,
        GroupReduceKind kind,
        Value? identity)
    {
        if (data is null || identity is null) return null;

        if (IsShuffleOrBroadcastConstant(data, groupLevel: true)
            && IsIdempotentOperation(intrinsicOp))
        {
            return data;
        }

        return Append(new GroupReduce(
            GetInitializer(location),
            data,
            intrinsicOp,
            kind,
            identity));
    }

    /// <summary>
    /// Creates a group reduce from an operation value. Automatically infers
    /// a <see cref="BinaryArithmeticKind"/> when the operation is a recognized
    /// primitive.
    /// </summary>
    public Value? CreateGroupReduce(
        Location location,
        Value? data,
        Value? operation,
        GroupReduceKind kind,
        Value? identity)
    {
        if (data is null || operation is null || identity is null) return null;

        var intrinsicOp = TryRecognizeOperation(operation);
        if (intrinsicOp.HasValue)
            return CreateGroupReduce(location, data, intrinsicOp.Value, kind, identity);

        return Append(new GroupReduce(
            GetInitializer(location),
            data,
            operation,
            kind,
            identity));
    }

    /// <summary>
    /// Creates a group scan with a known intrinsic binary operation.
    /// </summary>
    public Value? CreateGroupScan(
        Location location,
        Value? data,
        BinaryArithmeticKind intrinsicOp,
        GroupScanKind kind,
        Value? identity)
    {
        if (data is null || identity is null) return null;

        if (kind == GroupScanKind.Inclusive
            && IsShuffleOrBroadcastConstant(data, groupLevel: true)
            && IsIdempotentOperation(intrinsicOp))
        {
            return data;
        }

        return Append(new GroupScan(
            GetInitializer(location),
            data,
            intrinsicOp,
            kind,
            identity));
    }

    /// <summary>
    /// Creates a group scan from an operation value. Automatically infers
    /// a <see cref="BinaryArithmeticKind"/> when the operation is a recognized
    /// primitive.
    /// </summary>
    public Value? CreateGroupScan(
        Location location,
        Value? data,
        Value? operation,
        GroupScanKind kind,
        Value? identity)
    {
        if (data is null || operation is null || identity is null) return null;

        var intrinsicOp = TryRecognizeOperation(operation);
        if (intrinsicOp.HasValue)
            return CreateGroupScan(location, data, intrinsicOp.Value, kind, identity);

        return Append(new GroupScan(
            GetInitializer(location),
            data,
            operation,
            kind,
            identity));
    }

    /// <summary>
    /// Creates a warp-wide radix sort value.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="data">The data value to sort.</param>
    /// <param name="numBits">The number of bits to sort.</param>
    /// <param name="defaultValue">The default/padding value.</param>
    /// <param name="extractRadixBitsMethod">
    /// The ExtractRadixBits method reference.
    /// </param>
    /// <returns>The created value or null.</returns>
    public Value? CreateWarpRadixSort(
        Location location,
        Value? data,
        int numBits,
        Value? defaultValue,
        Value? extractRadixBitsMethod)
    {
        if (data is null || defaultValue is null ||
            extractRadixBitsMethod is null)
        {
            return null;
        }

        return Append(new WarpRadixSort(
            GetInitializer(location),
            data,
            numBits,
            defaultValue,
            extractRadixBitsMethod));
    }

    /// <summary>
    /// Creates a group-wide radix sort value.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="data">The data value to sort.</param>
    /// <param name="numBits">The number of bits to sort.</param>
    /// <param name="defaultValue">The default/padding value.</param>
    /// <param name="extractRadixBitsMethod">
    /// The ExtractRadixBits method reference.
    /// </param>
    /// <returns>The created value or null.</returns>
    public Value? CreateGroupRadixSort(
        Location location,
        Value? data,
        int numBits,
        Value? defaultValue,
        Value? extractRadixBitsMethod)
    {
        if (data is null || defaultValue is null ||
            extractRadixBitsMethod is null)
        {
            return null;
        }

        return Append(new GroupRadixSort(
            GetInitializer(location),
            data,
            numBits,
            defaultValue,
            extractRadixBitsMethod));
    }
}
