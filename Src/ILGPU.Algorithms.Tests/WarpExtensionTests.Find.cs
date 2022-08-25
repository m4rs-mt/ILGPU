// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpExtensionTests.Find.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.AtomicOperations;
using ILGPU.Runtime;
using ILGPU.Tests;
using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;
using Xunit;

#pragma warning disable xUnit1026
#pragma warning disable CA1815

namespace ILGPU.Algorithms.Tests
{
    partial class WarpExtensionTests
    {
        internal readonly struct LaneEntry :
            IScanReduceOperation<LaneEntry>,
            IAtomicOperation<LaneEntry>,
            ICompareExchangeOperation<LaneEntry>
        {
            public LaneEntry(int distance, int laneIndex)
            {
                Distance = distance;
                LaneIndex = laneIndex;
            }

            public string CLCommand => string.Empty;
            public LaneEntry Identity => new LaneEntry(int.MaxValue, int.MaxValue);

            public int Distance { get; }
            public int LaneIndex { get; }

            public LaneEntry Apply(LaneEntry first, LaneEntry second) =>
                Utilities.Select(first.Distance < second.Distance, first, second);

            public LaneEntry Operation(LaneEntry current, LaneEntry value) =>
                Apply(current, value);

            public void AtomicApply(ref LaneEntry target, LaneEntry value) =>
                Atomic.MakeAtomic(ref target, value, this, this);

            public LaneEntry CompareExchange(
                ref LaneEntry target,
                LaneEntry compare,
                LaneEntry value)
            {
                ref long targetL = ref Unsafe.As<LaneEntry, long>(ref target);
                long compareL = Unsafe.As<LaneEntry, long>(ref compare);
                long valueL = Unsafe.As<LaneEntry, long>(ref value);
                long result = Atomic.CompareExchange(ref targetL, compareL, valueL);
                return Unsafe.As<long, LaneEntry>(ref result);
            }

            public bool IsSame(LaneEntry left, LaneEntry right) =>
                left.Distance == right.Distance & left.LaneIndex == right.LaneIndex;

            public override string ToString() => $"{LaneIndex}: {Distance}d";
        }

        public static void FindKernel(
            ArrayView<int> values,
            ArrayView<int> origins,
            ArrayView<int> results)
        {
            const int MaxGroupSize = 1024;

            int localDistance = int.MaxValue;
            ref var bestSharedDistance = ref SharedMemory.Allocate<int>();
            var sharedDistanceValues = SharedMemory.Allocate<int>(MaxGroupSize);
            for (int i = Group.IdxX; i < MaxGroupSize; i += Group.DimX)
                sharedDistanceValues[Group.IdxX] = localDistance;
            Group.Barrier();

            int source = values[Grid.IdxX];
            if (source < 0)
                return;
            for (int i = Group.IdxX; i < origins.IntLength; i += Group.DimX)
            {
                int origin = origins[i];
                int result = Math.Abs(origin - source);

                localDistance = Utilities.Select(
                    result < localDistance,
                    result,
                    localDistance);
            }

            // Commit changes to shared memory
            sharedDistanceValues[Group.IdxX] = localDistance;
            Group.Barrier();

            // Determine the best value in the first warp
            if (Warp.WarpIdx == 0)
            {
                LaneEntry entry = default(LaneEntry).Identity;
                for (int i = Warp.LaneIdx; i < Group.DimX; i += Warp.WarpSize)
                {
                    var bestFit = WarpExtensions.Reduce<LaneEntry, LaneEntry>(
                        new(sharedDistanceValues[i], i));
                    entry = entry.Apply(entry, bestFit);
                }
                Warp.Barrier();

                // First lane contains the actual result
                if (Warp.IsFirstLane)
                    bestSharedDistance = sharedDistanceValues[entry.LaneIndex];
            }
            Group.Barrier();

            // First thread should have all results
            if (Group.IsFirstThread)
                results[Grid.IdxX] = bestSharedDistance;
        }

        [Fact]
        [KernelMethod(nameof(FindKernel))]
        public void FindDistances()
        {
            var values = new int[] { 1, 2, 3 };
            var origins = new int[] { 7, 5, 1, 4, 5, 1, 2, 4, 6, 7, 8 };
            var results = new int[] { 0, 0, 1 };

            using var valuesBuffer = Accelerator.Allocate1D<int>(values);
            using var originsBuffer = Accelerator.Allocate1D<int>(origins);
            using var resultsBuffer = Accelerator.Allocate1D<int>(values.Length);

            Execute(
                new KernelConfig(
                    values.Length,
                    Accelerator.MaxNumThreadsPerGroup),
                valuesBuffer.View.AsContiguous(),
                originsBuffer.View.AsContiguous(),
                resultsBuffer.View.AsContiguous());

            Verify(resultsBuffer.View, results);
        }
    }
}

#pragma warning restore xUnit1026
#pragma warning restore CA1815
