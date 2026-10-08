// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerGroupCollectives.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Lowers <see cref="GroupReduce"/> and <see cref="GroupScan"/> nodes into
/// sequences of shared memory allocation, warp-level reduction, barriers,
/// and cross-warp combining. The produced <see cref="WarpReduce"/> and
/// <see cref="WarpScan"/> nodes are subsequently lowered to shuffle
/// sequences by <see cref="LowerWarpCollectives"/>.
///
/// Must run after <see cref="AcceleratorSpecializer"/> (warp size known).
///
/// <para>
/// Algorithm (2 barriers, no conditional stores, no atomics):
/// <list type="number">
/// <item>Allocate shared[warpDim], init all to identity via benign race</item>
/// <item>Barrier</item>
/// <item>Per-warp AllReduce; all lanes store result to shared[warpIdx]</item>
/// <item>Barrier</item>
/// <item>Each thread reads shared[laneIdx], warp reduce across results</item>
/// </list>
/// </para>
/// <para>
/// No final barrier is emitted: each <see cref="GroupReduce"/> call site
/// allocates its own unique shared global, so subsequent operations cannot
/// collide with this call site's shared memory. If the user's kernel has
/// later code that needs ordering with respect to this reduce, it must
/// issue its own barrier.
/// </para>
/// </summary>
/// <param name="args">The transformation args.</param>
/// <param name="specification">The target architecture specification.</param>
sealed class LowerGroupCollectives(
    TransformationArgs args,
    ArchitectureSpecification specification) : Transformation(args)
{
    /// <summary>
    /// Fallback scratchpad slot count when the target warp size is unknown
    /// (e.g., OpenCL compile-time unknown sub_group_size). 32 covers every
    /// supported backend's per-group warp count upper bound.
    /// </summary>
    private const int FallbackScratchSlots = 32;

    private readonly int _scratchSlots =
        specification.WarpSize ?? FallbackScratchSlots;

    /// <summary>
    /// Maps IR values that need lowering.
    /// </summary>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        DeferBlockValueMapping = true;
        MapBasicBlockValue<GroupReduce>(LowerReduce);
        MapBasicBlockValue<GroupScan>(LowerScan);
        MapBasicBlockValue<GroupRadixSort>(LowerRadixSort);
    }

    /// <summary>
    /// Lowers a <see cref="GroupReduce"/> to shared memory + warp reduce
    /// + barrier sequences.
    /// </summary>
    private Value? LowerReduce(
        BasicBlockTransform transform,
        GroupReduce reduce)
    {
        if (!reduce.HasIntrinsicOperation)
        {
            throw new NotSupportedException(
                "Custom group reduce operations are not yet supported. " +
                "Use a recognized binary operation like (a, b) => a + b.");
        }

        var location = reduce.Location;
        var data = transform.Rewrite(reduce.Variable).AsNotNull();
        var identity = transform.Rewrite(reduce.Identity);
        var op = reduce.IntrinsicOp!.Value;

        // Obtain thread indices
        var laneIdx = transform.CreateSubGroupLaneIndexValue(location);
        var warpIdx = transform.CreateSubGroupIndexValue(location);

        // --- Shared memory allocation ---
        // Use a compile-time constant slot count so the backend emitters
        // produce a fixed-size shared array declaration
        // (e.g. `__shared__ int scratch[32]`). A runtime-sized length
        // produces a scalar global on C-family backends, which then
        // breaks the downstream GEP/Load/Store emission.
        var slots = transform.CreatePrimitiveValue(location, _scratchSlots);
        var sharedGlobal = transform.ModuleBuilder.CreateGlobal(
            location,
            data.Type,
            MemoryAddressSpace.Shared,
            slots);
        var view = transform.CreateNewView(location, sharedGlobal, slots).AsNotNull();

        // --- Step 0: Initialize all slots to identity ---
        // Each thread writes identity to shared[laneIdx].
        // Multiple warps write the same value to the same slot = benign
        // race on SIMT architectures (all writers agree on value).
        var initAddr = transform.CreateLoadElementAddress(
            location, view, laneIdx).AsNotNull();
        transform.CreateStore(location, initAddr, identity);
        transform.CreateBarrier(location, BarrierKind.GroupLevel);

        // --- Step 1: Per-warp AllReduce ---
        // All lanes in each warp get their warp's aggregated result.
        var warpResult = transform.CreateWarpReduce(
            location, data, op, WarpReduceKind.AllReduce).AsNotNull();

        // --- Step 2: Store per-warp result ---
        // All lanes in a warp store the same value to shared[warpIdx].
        // Identical stores to the same address = safe on SIMT.
        var storeAddr = transform.CreateLoadElementAddress(
            location,
            view,
            warpIdx).AsNotNull();
        transform.CreateStore(location, storeAddr, warpResult);
        transform.CreateBarrier(location, BarrierKind.GroupLevel);

        // --- Step 3: Final reduce across per-warp results ---
        // Each thread reads shared[laneIdx]:
        //   laneIdx < numWarps → reads a real warp result
        //   laneIdx >= numWarps → reads identity (from step 0, not overwritten)
        // WarpReduce combines them; identity in unused slots is neutral.
        var readAddr = transform.CreateLoadElementAddress(
            location,
            view,
            laneIdx).AsNotNull();
        var val = transform.CreateLoad(location, readAddr).AsNotNull();

        var finalKind = reduce.Kind == GroupReduceKind.AllReduce
            ? WarpReduceKind.AllReduce
            : WarpReduceKind.Reduce;
        var finalResult = transform.CreateWarpReduce(
            location,
            val,
            op,
            finalKind).AsNotNull();

        return finalResult;
    }

    /// <summary>
    /// Lowers a <see cref="GroupScan"/> to shared memory + warp scan
    /// + warp reduce + barrier + shuffle sequences.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Algorithm (2 barriers):
    /// <list type="number">
    /// <item>Allocate shared[warpDim], init all to identity via benign
    /// race</item>
    /// <item>Barrier</item>
    /// <item>Per-warp <see cref="WarpScan"/>(Inclusive) +
    /// <see cref="WarpReduce"/>(AllReduce) for per-warp total; all
    /// lanes store total to shared[warpIdx] (benign race)</item>
    /// <item>Barrier</item>
    /// <item>Each thread loads shared[laneIdx] (per-warp totals),
    /// <see cref="WarpScan"/>(Exclusive) for cross-warp prefix,
    /// <see cref="Shuffle"/>(Generic) to extract this warp's prefix,
    /// combine with per-warp scan result</item>
    /// </list>
    /// </para>
    /// </remarks>
    private Value? LowerScan(
        BasicBlockTransform transform,
        GroupScan scan)
    {
        if (!scan.HasIntrinsicOperation)
        {
            throw new NotSupportedException(
                "Custom group scan operations are not yet supported. " +
                "Use a recognized binary operation like (a, b) => a + b.");
        }

        var location = scan.Location;
        var data = transform.Rewrite(scan.Variable).AsNotNull();
        var identity = transform.Rewrite(scan.Identity);
        var op = scan.IntrinsicOp!.Value;

        // Obtain thread indices
        var laneIdx = transform.CreateSubGroupLaneIndexValue(location);
        var warpIdx = transform.CreateSubGroupIndexValue(location);

        // --- Shared memory allocation ---
        var slots = transform.CreatePrimitiveValue(location, _scratchSlots);
        var sharedGlobal = transform.ModuleBuilder.CreateGlobal(
            location,
            data.Type,
            MemoryAddressSpace.Shared,
            slots);
        var view = transform.CreateNewView(
            location, sharedGlobal, slots).AsNotNull();

        // --- Step 0: Initialize all slots to identity (benign race) ---
        var initAddr = transform.CreateLoadElementAddress(
            location,
            view,
            laneIdx).AsNotNull();
        transform.CreateStore(location, initAddr, identity);
        transform.CreateBarrier(location, BarrierKind.GroupLevel);

        // --- Step 1: Per-warp scan ---
        // Use the requested scan kind directly. LowerWarpCollectives
        // handles the Inclusive→Exclusive conversion internally.
        var warpScanKind = scan.Kind == GroupScanKind.Inclusive
            ? WarpScanKind.Inclusive
            : WarpScanKind.Exclusive;
        var perWarpResult = transform.CreateWarpScan(
            location,
            data,
            op,
            warpScanKind,
            identity).AsNotNull();

        // --- Step 2: Per-warp total + store to shared ---
        // AllReduce gives every lane in the warp the same total,
        // enabling benign-race stores (same pattern as LowerReduce).
        var warpTotal = transform.CreateWarpReduce(
            location,
            data,
            op,
            WarpReduceKind.AllReduce).AsNotNull();
        var storeAddr = transform.CreateLoadElementAddress(
            location,
            view,
            warpIdx).AsNotNull();
        transform.CreateStore(location, storeAddr, warpTotal);
        transform.CreateBarrier(location, BarrierKind.GroupLevel);

        // --- Step 3: Cross-warp exclusive prefix ---
        // Slots beyond numWarps still hold identity from Step 0.
        var readAddr = transform.CreateLoadElementAddress(
            location,
            view,
            laneIdx).AsNotNull();
        var warpTotals = transform.CreateLoad(location, readAddr).AsNotNull();
        var exclusivePrefix = transform.CreateWarpScan(
            location,
            warpTotals,
            op,
            WarpScanKind.Exclusive,
            identity).AsNotNull();

        // --- Step 4: Extract this warp's prefix ---
        // All lanes in warp W have warpIdx == W (uniform).
        var myWarpPrefix = transform.CreateShuffle(
            location,
            exclusivePrefix,
            warpIdx,
            ShuffleKind.Generic).AsNotNull();

        // --- Step 5: Combine prefix with per-warp result ---
        return transform.CreateArithmetic(
            location,
            myWarpPrefix,
            perWarpResult,
            op);
    }

    /// <summary>
    /// Lowers a <see cref="GroupRadixSort"/> into a shared-memory-based
    /// radix sort using group-wide exclusive scans and reductions to compute
    /// target positions, with shared memory scatter for the data exchange.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Algorithm (per bit, fully unrolled over NumBits):
    /// <list type="number">
    /// <item>Store elements to shared data array, barrier</item>
    /// <item>Extract current bit from each element</item>
    /// <item>Group exclusive scan of isZero flags (rank among 0-keyed
    /// elements)</item>
    /// <item>Group all-reduce of isZero flags (total count of zeros)</item>
    /// <item>Compute target: key==0 → rank0, key==1 → totalZeros +
    /// (threadIdx - rank0)</item>
    /// <item>Scatter through shared memory, barrier, read back</item>
    /// </list>
    /// </para>
    /// <para>
    /// The group exclusive scan and all-reduce are inlined as WarpScan +
    /// WarpReduce + shared memory + barrier sequences (same pattern as
    /// <see cref="LowerScan"/> and <see cref="LowerReduce"/>), so
    /// <see cref="LowerWarpCollectives"/> can further lower them to
    /// shuffle sequences.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? LowerRadixSort(
        BasicBlockTransform transform,
        GroupRadixSort sort)
    {
        var location = sort.Location;
        var data = transform.Rewrite(sort.Variable).AsNotNull();
        var defaultValue = transform.Rewrite(sort.DefaultValue).AsNotNull();
        var extractMethod = transform.Rewrite(sort.ExtractRadixBitsMethod)
            .AsNotNull();
        int numBits = sort.NumBits;

        var laneIdx = transform.CreateSubGroupLaneIndexValue(location);
        var warpIdx = transform.CreateSubGroupIndexValue(location);
        var threadIdx = transform.CreateGroupIndexValue(location);
        var zero = transform.CreatePrimitiveValue(location, 0);
        var one = transform.CreatePrimitiveValue(location, 1);
        var bitMaskOne = transform.CreatePrimitiveValue(location, 1);
        var extractTarget = (Method)extractMethod;

        // --- Allocate shared data array (group dimension elements) ---
        // Size the backing storage with a compile-time constant so the CPU
        // backend (which stackallocs the backing buffer) and any other
        // backend that requires a static shared-memory size can allocate
        // correctly. CreateGroupDimensionValue is a runtime value; passing
        // it as the length falls through to "length unknown → size 1" in
        // CPUMethodEmitter.EmitGlobalDeclarations, causing out-of-bounds
        // writes during the cross-lane scatter. On supported backends the
        // group dimension equals the warp size (CPU: VectorWidth == 1 warp
        // per group; Metal: simdgroup == threadgroup for small groups), so
        // _scratchSlots is a safe compile-time upper bound. Runtime-sized
        // groups that exceed one warp still use the runtime groupDim for
        // the view, which drives the index computations.
        var groupDim = transform.CreateGroupDimensionValue(location);
        var staticGroupSize = transform.CreatePrimitiveValue(
            location, _scratchSlots);
        var sharedDataGlobal = transform.ModuleBuilder.CreateGlobal(
            location,
            data.Type,
            MemoryAddressSpace.Shared,
            staticGroupSize);
        var sharedData = transform.CreateNewView(
            location, sharedDataGlobal, groupDim).AsNotNull();

        // --- Allocate shared scratch for scan/reduce (warp-count slots) ---
        var intType = transform.ModuleBuilder.GetPrimitiveType(
            BasicValueType.Int32);
        var slots = transform.CreatePrimitiveValue(location, _scratchSlots);
        var sharedScratchGlobal = transform.ModuleBuilder.CreateGlobal(
            location,
            intType,
            MemoryAddressSpace.Shared,
            slots);
        var sharedScratch = transform.CreateNewView(
            location, sharedScratchGlobal, slots).AsNotNull();

        var value = data;

        // Outer loop: one bit at a time (fully unrolled)
        for (int bitIdx = 0; bitIdx < numBits; ++bitIdx)
        {
            var bitIdxValue = transform.CreatePrimitiveValue(location, bitIdx);

            // --- Step 1: Store to shared data array ---
            var storeAddr = transform.CreateLoadElementAddress(
                location, sharedData, threadIdx).AsNotNull();
            transform.CreateStore(location, storeAddr, value);
            transform.CreateBarrier(location, BarrierKind.GroupLevel);

            // --- Step 2: Read and extract bit ---
            var element = transform.CreateLoad(location, storeAddr).AsNotNull();
            var callBuilder = transform.CreateCall(location, extractTarget);
            callBuilder.Add(element);
            callBuilder.Add(bitIdxValue);
            callBuilder.Add(bitMaskOne);
            Value key = callBuilder.Seal();

            var keyIsZero = transform.CreateCompare(
                location, key, zero, CompareKind.Equal);
            var isZero = transform.CreatePredicate(
                location, keyIsZero, one, zero).AsNotNull();

            // --- Step 3: Group exclusive scan of isZero (Add) ---
            // Inlined group exclusive scan pattern (same as LowerScan):
            //   init scratch to 0, barrier, per-warp scan, store totals,
            //   barrier, cross-warp prefix, combine
            var initScratchAddr = transform.CreateLoadElementAddress(
                location, sharedScratch, laneIdx).AsNotNull();
            transform.CreateStore(location, initScratchAddr, zero);
            transform.CreateBarrier(location, BarrierKind.GroupLevel);

            var warpExclScan = transform.CreateWarpScan(
                location, isZero, BinaryArithmeticKind.Add,
                WarpScanKind.Exclusive, zero).AsNotNull();
            var warpTotal = transform.CreateWarpReduce(
                location, isZero, BinaryArithmeticKind.Add,
                WarpReduceKind.AllReduce).AsNotNull();
            var storeScratchAddr = transform.CreateLoadElementAddress(
                location, sharedScratch, warpIdx).AsNotNull();
            transform.CreateStore(location, storeScratchAddr, warpTotal);
            transform.CreateBarrier(location, BarrierKind.GroupLevel);

            var readScratchAddr = transform.CreateLoadElementAddress(
                location, sharedScratch, laneIdx).AsNotNull();
            var warpTotals = transform.CreateLoad(
                location, readScratchAddr).AsNotNull();
            var crossWarpPrefix = transform.CreateWarpScan(
                location, warpTotals, BinaryArithmeticKind.Add,
                WarpScanKind.Exclusive, zero).AsNotNull();
            var myWarpPrefix = transform.CreateShuffle(
                location, crossWarpPrefix, warpIdx,
                ShuffleKind.Generic).AsNotNull();
            var rank0 = transform.CreateArithmetic(
                location, myWarpPrefix, warpExclScan,
                BinaryArithmeticKind.Add).AsNotNull();

            // --- Step 4: Group all-reduce of isZero (Add) ---
            // Inlined group all-reduce pattern (same as LowerReduce):
            //   reuse scratch (already has warp totals from step 3),
            //   read back and do final warp reduce
            var readReduceAddr = transform.CreateLoadElementAddress(
                location, sharedScratch, laneIdx).AsNotNull();
            var reduceVal = transform.CreateLoad(
                location, readReduceAddr).AsNotNull();
            var totalZeros = transform.CreateWarpReduce(
                location, reduceVal, BinaryArithmeticKind.Add,
                WarpReduceKind.AllReduce).AsNotNull();

            // --- Step 5: Compute target position ---
            // key==0: target = rank0
            // key==1: target = totalZeros + (threadIdx - rank0)
            var rank1 = transform.CreateArithmetic(
                location, threadIdx, rank0,
                BinaryArithmeticKind.Sub).AsNotNull();
            var rank1Offset = transform.CreateArithmetic(
                location, totalZeros, rank1,
                BinaryArithmeticKind.Add).AsNotNull();
            var target = transform.CreatePredicate(
                location, keyIsZero, rank0, rank1Offset).AsNotNull();

            // --- Step 6: Scatter through shared memory ---
            var targetAddr = transform.CreateLoadElementAddress(
                location, sharedData, target).AsNotNull();
            transform.CreateStore(location, targetAddr, element);
            transform.CreateBarrier(location, BarrierKind.GroupLevel);

            // --- Step 7: Read back sorted element ---
            value = transform.CreateLoad(location, storeAddr).AsNotNull();

            // Barrier before next iteration (shared data reused)
            if (bitIdx < numBits - 1)
                transform.CreateBarrier(location, BarrierKind.GroupLevel);
        }

        return value;
    }
}
