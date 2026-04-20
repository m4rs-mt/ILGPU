// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Warp.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles warp barrier operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Warp_Barrier(ref InvocationContext context) =>
        context.Builder.CreateBarrier(context.Location, BarrierKind.WarpLevel);

    /// <summary>
    /// Handles warp barrier pop-count operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Warp_BarrierPopCount(ref InvocationContext context) =>
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
    private static Value? Warp_BarrierAnd(ref InvocationContext context) =>
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
    private static Value? Warp_BarrierOr(ref InvocationContext context) =>
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
    private static Value? Warp_Broadcast(ref InvocationContext context) =>
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
    private static Value? Warp_Dimension(ref InvocationContext context) =>
        context.Builder.CreateSubGroupDimensionValue(context.Location);

    /// <summary>
    /// Handles warp index operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Warp_WarpIndex(ref InvocationContext context) =>
        context.Builder.CreateSubGroupIndexValue(context.Location);

    /// <summary>
    /// Handles warp lane index operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Warp_LaneIndex(ref InvocationContext context) =>
        context.Builder.CreateSubGroupLaneIndexValue(context.Location);

    /// <summary>
    /// Handles warp shuffle operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="kind">Kind of the shuffle operation.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Warp_Shuffle(
        ref InvocationContext context,
        ShuffleKind kind = ShuffleKind.Generic) =>
        context.Builder.CreateShuffle(
            context.Location,
            context.Pull(),
            context.Pull(),
            kind);

    /// <summary>
    /// Handles warp reduce/all-reduce operations with lambda binary operations.
    /// The lambda is resolved via delegate devirtualization. The frontend
    /// always creates a custom <see cref="WarpReduce"/> referencing the
    /// lambda <see cref="IR.ModuleValues.Method"/>; recognition of the
    /// underlying binary arithmetic kind happens later in
    /// <see cref="IR.Transformations.RecognizeCollectiveOps"/> once the
    /// lambda body has been fully compiled by the IL frontend.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="kind">Reduce or AllReduce.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Warp_Reduce(
        ref InvocationContext context,
        WarpReduceKind kind = WarpReduceKind.Reduce)
    {
        var data = context.Pull();
        var methodBase = context.PullDelegateMethodBase();
        var method = methodBase is not null
            ? context.CodeGenerator.GetMethod(methodBase)
            : null;
        return context.Builder.CreateWarpReduce(
            context.Location,
            data,
            method,
            kind);
    }

    /// <summary>
    /// Handles warp scan operations with lambda binary operations.
    /// Always emits a custom <see cref="WarpScan"/> referencing the lambda
    /// method — <see cref="IR.Transformations.RecognizeCollectiveOps"/>
    /// converts to intrinsic form after the body is compiled.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="kind">Inclusive or Exclusive.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Warp_Scan(
        ref InvocationContext context,
        WarpScanKind kind = WarpScanKind.Inclusive)
    {
        var data = context.Pull();
        Value? identity = kind == WarpScanKind.Exclusive
            ? context.Pull()
            : null;
        var methodBase = context.PullDelegateMethodBase();
        var method = methodBase is not null
            ? context.CodeGenerator.GetMethod(methodBase)
            : null;
        return context.Builder.CreateWarpScan(
            context.Location,
            data,
            method,
            kind,
            identity);
    }

    /// <summary>
    /// Handles warp radix sort operations.
    /// Resolves IRadixSortOperation static abstract members via reflection
    /// at frontend time, since the concrete operation type is fully known.
    /// </summary>
    private static Value? Warp_RadixSort(ref InvocationContext context)
    {
        var data = context.Pull();

        // Get generic type arguments: [T, TRadixSortOperation]
        var operationType = context.GetMethodGenericArguments()[1];

        // Resolve NumBits (static abstract int property)
        var numBits = (int)operationType
            .GetProperty(
                "NumBits",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;

        // Resolve DefaultValue (static abstract T property)
        var defaultValueObj = operationType
            .GetProperty(
                "DefaultValue",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        var defaultValue = context.Builder.CreatePrimitiveValue(
            context.Location,
            defaultValueObj);

        // Resolve ExtractRadixBits method
        var extractMethodInfo = operationType.GetMethod(
            "ExtractRadixBits",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Static)!;
        var extractMethod = context.CodeGenerator.GetMethod(extractMethodInfo);

        return context.Builder.CreateWarpRadixSort(
            context.Location,
            data,
            numBits,
            defaultValue,
            extractMethod);
    }
}
