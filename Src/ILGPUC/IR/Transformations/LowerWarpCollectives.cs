// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerWarpCollectives.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Lowers <see cref="WarpReduce"/> and <see cref="WarpScan"/> nodes into unrolled
/// shuffle-based sequences for backends without native warp collective support.
/// Runs after <see cref="AcceleratorSpecializer"/> so the warp size is known.
/// </summary>
/// <param name="args">The transformation args.</param>
/// <param name="specification">The target architecture specification.</param>
sealed class LowerWarpCollectives(
    TransformationArgs args,
    ArchitectureSpecification specification) : Transformation(args)
{
    private readonly int? _warpSize = specification.WarpSize;

    /// <summary>
    /// Returns true if the target backend supports native emission for the
    /// given warp reduce operation, meaning the node should NOT be lowered.
    /// </summary>
    private bool SupportsNativeWarpReduce(BinaryArithmeticKind op) =>
        specification.AcceleratorType switch
        {
            // Metal: simd_sum, simd_min, simd_max, simd_and, simd_or,
            // simd_xor, simd_product
            AcceleratorType.Metal => op is
                BinaryArithmeticKind.Add or BinaryArithmeticKind.Min or
                BinaryArithmeticKind.Max or BinaryArithmeticKind.And or
                BinaryArithmeticKind.Or or BinaryArithmeticKind.Xor or
                BinaryArithmeticKind.Mul,
            // OpenCL: sub_group_reduce_add, sub_group_reduce_min,
            // sub_group_reduce_max
            AcceleratorType.OpenCL => op is
                BinaryArithmeticKind.Add or BinaryArithmeticKind.Min or
                BinaryArithmeticKind.Max,
            // CPU uses a direct masked-reduce helper so inactive lanes
            // (for partial SIMD groups in the last launch chunk)
            // contribute the identity instead of polluting the result.
            AcceleratorType.CPU => true,
            // CUDA, ROCm: no native warp reduce, must be lowered
            _ => false
        };

    /// <summary>
    /// Returns true if the target backend supports native emission for the
    /// given warp scan operation.
    /// </summary>
    private bool SupportsNativeWarpScan(BinaryArithmeticKind op) =>
        specification.AcceleratorType switch
        {
            // Metal: simd_prefix_{inclusive,exclusive}_{sum,product}
            AcceleratorType.Metal => op is
                BinaryArithmeticKind.Add or BinaryArithmeticKind.Mul,
            // OpenCL: sub_group_scan_{inclusive,exclusive}_add etc.
            AcceleratorType.OpenCL => op is
                BinaryArithmeticKind.Add or BinaryArithmeticKind.Min or
                BinaryArithmeticKind.Max,
            // CPU uses a direct masked-scan helper.
            AcceleratorType.CPU => true,
            _ => false
        };

    /// <summary>
    /// Maps IR values that need lowering.
    /// </summary>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        DeferBlockValueMapping = true;
        MapBasicBlockValue<WarpReduce>(LowerReduce);
        MapBasicBlockValue<WarpScan>(LowerScan);
        MapBasicBlockValue<WarpRadixSort>(LowerRadixSort);
    }

    /// <summary>
    /// Inlines a binary operation, producing a single
    /// <see cref="BinaryArithmeticValue"/> for intrinsic operations or
    /// falling back to a <see cref="MethodCall"/> for custom operations.
    /// </summary>
    private static Value InlineApply(
        BasicBlockTransform transform,
        Location location,
        WarpReduce source,
        Value left,
        Value right)
    {
        if (source.HasIntrinsicOperation)
        {
            return transform.CreateArithmetic(
                location,
                left,
                right,
                source.IntrinsicOp!.Value).AsNotNull();
        }

        // Custom operation: the Operation value is a Method — emit a call.
        // The inliner will inline it in a subsequent pass.
        throw new NotSupportedException(
            "Custom warp reduce operations are not yet supported for " +
            "shuffle-based lowering. Use a recognized binary operation " +
            "like (a, b) => a + b.");
    }

    /// <summary>
    /// Inlines a binary operation for a scan node.
    /// </summary>
    private static Value InlineApply(
        BasicBlockTransform transform,
        Location location,
        WarpScan source,
        Value left,
        Value right)
    {
        if (source.HasIntrinsicOperation)
        {
            return transform.CreateArithmetic(
                location,
                left,
                right,
                source.IntrinsicOp!.Value).AsNotNull();
        }

        throw new NotSupportedException(
            "Custom warp scan operations are not yet supported for " +
            "shuffle-based lowering. Use a recognized binary operation " +
            "like (a, b) => a + b.");
    }

    /// <summary>
    /// Lowers a <see cref="WarpReduce"/> to an unrolled shuffle sequence.
    /// Skips lowering when the backend supports native emission for the
    /// recognized intrinsic operation.
    /// </summary>
    /// <summary>
    /// Attempts late recognition of a custom WarpReduce's operation.
    /// At frontend time, the lambda Method body may not be compiled yet,
    /// so TryRecognizeOperation deferred. By backend transform time, all
    /// methods are compiled and the body can be inspected.
    /// </summary>
    private static WarpReduce? TryConvertToIntrinsic(
        BasicBlockTransform transform,
        WarpReduce reduce)
    {
        if (reduce.HasIntrinsicOperation)
            return reduce;

        // Try late recognition of the custom operation
        var op = BasicBlockBuilder.TryRecognizeOperation(reduce.Operation);
        if (op.HasValue)
        {
            // Create intrinsic variant to replace the custom one
            return transform.CreateWarpReduce(
                reduce.Location,
                transform.Rewrite(reduce.Variable),
                op.Value,
                reduce.Kind) as WarpReduce;
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? LowerReduce(BasicBlockTransform transform, WarpReduce reduce)
    {
        // Try late recognition for custom operations whose Method body
        // wasn't compiled at frontend time
        var recognized = TryConvertToIntrinsic(transform, reduce);
        if (recognized is null)
        {
            throw new NotSupportedException(
                "Cannot recognize the binary operation in the WarpReduce lambda. " +
                "Use a simple binary operation like (a, b) => a + b.");
        }
        reduce = recognized;

        // If the backend supports this operation natively, leave the node
        // for the ExpressionEmitter to emit via IntrinsicEmitter.
        if (reduce.HasIntrinsicOperation
            && SupportsNativeWarpReduce(reduce.IntrinsicOp!.Value))
        {
            return reduce;
        }

        if (!_warpSize.HasValue)
        {
            // No warp size known — can't lower. Keep the node as-is for
            // the backend emitter to handle (or fail at emit time).
            return reduce;
        }

        var location = reduce.Location;
        var data = transform.Rewrite(reduce.Variable).AsNotNull();
        int ws = _warpSize.Value;

        // Choose shuffle pattern based on reduce kind
        var shuffleKind = reduce.Kind == WarpReduceKind.AllReduce
            ? ShuffleKind.Xor   // butterfly: all lanes get result
            : ShuffleKind.Down; // linear: lane 0 gets result

        // Unroll: for offset in [ws/2, ws/4, ..., 1]
        var value = data;
        for (int offset = ws / 2; offset >= 1; offset >>= 1)
        {
            var offsetValue = transform.CreatePrimitiveValue(location, offset);
            var shuffled = transform.CreateShuffle(
                location, value, offsetValue, shuffleKind).AsNotNull();
            value = InlineApply(transform, location, reduce, value, shuffled);
        }

        return value;
    }

    /// <summary>
    /// Lowers a <see cref="WarpScan"/> to an unrolled shuffle sequence.
    /// Skips lowering when the backend supports native emission.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? LowerScan(BasicBlockTransform transform, WarpScan scan)
    {
        if (scan.HasIntrinsicOperation
            && SupportsNativeWarpScan(scan.IntrinsicOp!.Value))
        {
            return scan;
        }

        if (!_warpSize.HasValue)
            return scan;

        var location = scan.Location;
        var data = transform.Rewrite(scan.Variable)!;
        int ws = _warpSize.Value;

        if (scan.Kind == WarpScanKind.Inclusive)
            return LowerInclusiveScan(transform, scan, location, data, ws);

        // Exclusive scan: compute inclusive scan, then shift right by 1
        // and insert identity at lane 0 using Predicate (select).
        var inclusive = LowerInclusiveScan(transform, scan, location, data, ws);
        var one = transform.CreatePrimitiveValue(location, 1);
        var shifted = transform.CreateShuffle(
            location, inclusive, one, ShuffleKind.Up)!;

        if (scan.Identity is not null)
        {
            var identity = transform.Rewrite(scan.Identity).AsNotNull();
            // Lane 0 receives identity, all others receive shifted value.
            var laneIdx = transform.CreateSubGroupLaneIndexValue(location);
            var zero = transform.CreatePrimitiveValue(location, 0);
            var isFirstLane = transform.CreateCompare(
                location, laneIdx, zero, CompareKind.Equal);
            return transform.CreatePredicate(
                location, isFirstLane, identity, shifted);
        }

        return shifted;
    }

    /// <summary>
    /// Lowers an inclusive scan using the Hillis-Steele algorithm with ShuffleUp.
    /// Uses <see cref="Predicate"/> (select) to guard lanes below the current
    /// delta from applying the operation with undefined shuffle results.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Value LowerInclusiveScan(
        BasicBlockTransform transform,
        WarpScan scan,
        Location location,
        Value data,
        int warpSize)
    {
        // Hillis-Steele: for delta in [1, 2, 4, ..., warpSize/2]:
        //   other = ShuffleUp(value, delta)
        //   applied = op(value, other)
        //   value = (laneIdx >= delta) ? applied : value
        var laneIdx = transform.CreateSubGroupLaneIndexValue(location);
        var value = data;

        for (int delta = 1; delta < warpSize; delta <<= 1)
        {
            var deltaValue = transform.CreatePrimitiveValue(location, delta);
            var other = transform.CreateShuffle(
                location, value, deltaValue, ShuffleKind.Up)!;
            var applied = InlineApply(transform, location, scan, value, other);

            // Guard: only apply on lanes where laneIdx >= delta
            var cmp = transform.CreateCompare(
                location, laneIdx, deltaValue, CompareKind.GreaterEqual);
            value = transform.CreatePredicate(location, cmp, applied, value)!;
        }

        return value;
    }

    /// <summary>
    /// Lowers a <see cref="WarpRadixSort"/> into an unrolled shuffle-based
    /// radix sort sequence. The algorithm processes one bit at a time,
    /// performing an inclusive scan and per-lane scatter via shuffles.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? LowerRadixSort(
        BasicBlockTransform transform,
        WarpRadixSort sort)
    {
        if (!_warpSize.HasValue)
            return sort;

        var location = sort.Location;
        int ws = _warpSize.Value;
        int numBits = sort.NumBits;
        var defaultValue = transform.Rewrite(sort.DefaultValue).AsNotNull();
        var extractMethod = transform.Rewrite(sort.ExtractRadixBitsMethod)
            .AsNotNull();

        var laneIdx = transform.CreateSubGroupLaneIndexValue(location);
        var value = transform.Rewrite(sort.Variable).AsNotNull();
        var zero = transform.CreatePrimitiveValue(location, 0);
        var one = transform.CreatePrimitiveValue(location, 1);
        var lastLane = transform.CreatePrimitiveValue(location, ws - 1);
        var bitMaskOne = transform.CreatePrimitiveValue(location, 1);
        var extractTarget = (ModuleValues.Method)extractMethod;

        // Outer loop: one bit at a time (fully unrolled)
        for (int bitIdx = 0; bitIdx < numBits; ++bitIdx)
        {
            var bitIdxValue = transform.CreatePrimitiveValue(location, bitIdx);

            // key = ExtractRadixBits(value, bitIdx, 1)
            var callBuilder = transform.CreateCall(location, extractTarget);
            callBuilder.Add(value);
            callBuilder.Add(bitIdxValue);
            callBuilder.Add(bitMaskOne);
            Value key = callBuilder.Seal();

            // key0 = (key == 0) ? 1 : 0
            var keyIsZero = transform.CreateCompare(
                location, key, zero, CompareKind.Equal);
            var key0 = transform.CreatePredicate(
                location, keyIsZero, one, zero).AsNotNull();
            // key1 = 1 - key0
            var key1 = transform.CreateArithmetic(
                location, one, key0, BinaryArithmeticKind.Sub).AsNotNull();

            // Inclusive scan of key0, key1 via ShuffleUp (Hillis-Steele)
            for (int offset = 1; offset < ws - 1; offset <<= 1)
            {
                var offsetValue = transform.CreatePrimitiveValue(location, offset);
                var partialKey0 = transform.CreateShuffle(
                    location, key0, offsetValue, ShuffleKind.Up).AsNotNull();
                var partialKey1 = transform.CreateShuffle(
                    location, key1, offsetValue, ShuffleKind.Up).AsNotNull();

                // Guard: only apply on lanes where laneIdx >= offset
                var guard = transform.CreateCompare(
                    location, laneIdx, offsetValue, CompareKind.GreaterEqual);
                var guardedKey0 = transform.CreatePredicate(
                    location, guard, partialKey0, zero).AsNotNull();
                var guardedKey1 = transform.CreatePredicate(
                    location, guard, partialKey1, zero).AsNotNull();

                key0 = transform.CreateArithmetic(
                    location, key0, guardedKey0, BinaryArithmeticKind.Add)
                    .AsNotNull();
                key1 = transform.CreateArithmetic(
                    location, key1, guardedKey1, BinaryArithmeticKind.Add)
                    .AsNotNull();
            }

            // key1 += Shuffle(key0, warpSize - 1) (total count of 0-bits)
            var totalKey0 = transform.CreateShuffle(
                location, key0, lastLane, ShuffleKind.Generic).AsNotNull();
            key1 = transform.CreateArithmetic(
                location, key1, totalKey0, BinaryArithmeticKind.Add)
                .AsNotNull();

            // target = (key == 0) ? (key0 - 1) : (key1 - 1)
            var key0Minus1 = transform.CreateArithmetic(
                location, key0, one, BinaryArithmeticKind.Sub).AsNotNull();
            var key1Minus1 = transform.CreateArithmetic(
                location, key1, one, BinaryArithmeticKind.Sub).AsNotNull();
            var target = transform.CreatePredicate(
                location, keyIsZero, key0Minus1, key1Minus1).AsNotNull();

            // Scatter via per-lane shuffle (unrolled over warp size)
            var newElement = defaultValue;
            for (int k = 0; k < ws; k++)
            {
                var kValue = transform.CreatePrimitiveValue(location, k);
                var targetLane = transform.CreateShuffle(
                    location, target, kValue, ShuffleKind.Generic).AsNotNull();
                var retrievedElement = transform.CreateShuffle(
                    location, value, kValue, ShuffleKind.Generic).AsNotNull();
                var isMyLane = transform.CreateCompare(
                    location, targetLane, laneIdx, CompareKind.Equal);
                newElement = transform.CreatePredicate(
                    location, isMyLane, retrievedElement, newElement)
                    .AsNotNull();
            }

            value = newElement;
        }

        return value;
    }
}
