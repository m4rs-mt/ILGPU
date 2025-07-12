// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Warp.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Values;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles warp barrier operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Warp_Barrier(ref InvocationContext context) =>
        context.Builder.CreateBarrier(context.Location, BarrierKind.WarpLevel);

    /// <summary>
    /// Handles warp barrier pop-count operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Warp_BarrierPopCount(ref InvocationContext context) =>
        context.Builder.CreateBarrier(
            context.Location,
            PredicateBarrierKind.WarpLevel,
            context.Pull(),
            PredicateBarrierPredicateKind.PopCount);

    /// <summary>
    /// Handles warp barrier and operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Warp_BarrierAnd(ref InvocationContext context) =>
        context.Builder.CreateBarrier(
            context.Location,
            PredicateBarrierKind.WarpLevel,
            context.Pull(),
            PredicateBarrierPredicateKind.And);

    /// <summary>
    /// Handles warp barrier or operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Warp_BarrierOr(ref InvocationContext context) =>
        context.Builder.CreateBarrier(
            context.Location,
            PredicateBarrierKind.WarpLevel,
            context.Pull(),
            PredicateBarrierPredicateKind.Or);

    /// <summary>
    /// Handles warp broadcast operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Warp_Broadcast(ref InvocationContext context) =>
        context.Builder.CreateBroadcast(
            context.Location,
            context.Pull(),
            context.Builder.CreatePrimitiveValue(context.Location, 0),
            BroadcastKind.WarpLevel);

    /// <summary>
    /// Handles warp dimension operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Warp_Dimension(ref InvocationContext context) =>
        context.Builder.CreateWarpSizeValue(context.Location);

    /// <summary>
    /// Handles warp index operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Warp_WarpIndex(ref InvocationContext context)
    {
        var builder = context.Builder;
        // Get the current group index
        var groupIndex = builder.CreateGroupIndexValue(
            context.Location,
            DeviceConstantDimension3D.X);
        // Get warp size
        var warpSize = builder.CreateWarpSizeValue(context.Location);

        // Determine relative index
        return builder.CreateArithmetic(
            context.Location,
            groupIndex,
            warpSize,
            BinaryArithmeticKind.Div);
    }

    /// <summary>
    /// Handles warp lane index operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Warp_LaneIndex(ref InvocationContext context) =>
        context.Builder.CreateLaneIdxValue(context.Location);

    /// <summary>
    /// Handles warp shuffle operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="kind">Kind of the shuffle operation.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Warp_Shuffle(
        ref InvocationContext context,
        ShuffleKind kind = ShuffleKind.Generic) =>
        context.Builder.CreateShuffle(
            context.Location,
            context.Pull(),
            context.Pull(),
            kind);
}
