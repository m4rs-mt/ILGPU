// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PredicateBarrierGroups.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: PredicateBarrierGroups
// Group and warp predicate barriers over many groups with divergent
// predicates and a last group that is only partially filled. The first two
// blocks of 2048 threads have all-true and all-false predicates, the rest a
// pattern that differs between neighbouring threads; a uniform true and a
// uniform false predicate are combined as well. The host recomputes every
// result from the group and warp dimensions the kernel reports, so the test
// holds for any group size.
// Expected output: group mismatches 0, warp mismatches 0, uniform mismatches 0,
// partial True, groups True

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PredicateBarrierGroupsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> flags,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        bool predicate = input[index] != 0;
        int groupPopCount = Group.BarrierPopCount(predicate);
        bool groupAnd = Group.BarrierAnd(predicate);
        bool groupOr = Group.BarrierOr(predicate);
        int warpPopCount = Warp.BarrierPopCount(predicate);
        bool warpAnd = Warp.BarrierAnd(predicate);
        bool warpOr = Warp.BarrierOr(predicate);

        bool uniformTrue = flags[0] != 0;
        bool uniformFalse = flags[1] != 0;
        int truePopCount = Group.BarrierPopCount(uniformTrue);
        bool trueAnd = Group.BarrierAnd(uniformTrue);
        bool trueOr = Warp.BarrierOr(uniformTrue);
        int falsePopCount = Warp.BarrierPopCount(uniformFalse);
        bool falseAnd = Warp.BarrierAnd(uniformFalse);
        bool falseOr = Group.BarrierOr(uniformFalse);

        int offset = index * 6;
        output[offset] = groupPopCount * 4 + (groupAnd ? 2 : 0) + (groupOr ? 1 : 0);
        output[offset + 1] = warpPopCount * 4 + (warpAnd ? 2 : 0) + (warpOr ? 1 : 0);
        output[offset + 2] = truePopCount * 4 + (trueAnd ? 2 : 0) + (trueOr ? 1 : 0);
        output[offset + 3] =
            falsePopCount * 4 + (falseAnd ? 2 : 0) + (falseOr ? 1 : 0);
        output[offset + 4] = Warp.Index;
        output[offset + 5] = Group.Dimension;
    }
}

static class Program
{
    static int Encode(int popCount, bool all, bool any) =>
        popCount * 4 + (all ? 2 : 0) + (any ? 1 : 0);

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        const int Block = 2048;
        const int Length = 2 * Block + 1237;
        var inputData = new int[Length];
        for (int i = 0; i < Length; i++)
        {
            inputData[i] = i < Block ? 1
                : i < 2 * Block ? 0
                : (i * 7 + i / 3) % 5 < 2 ? 1 : 0;
        }

        using var input = stream.Allocate1D(inputData);
        using var flags = stream.Allocate1D(new int[] { 1, 0 });
        using var output = stream.Allocate1D<int>(Length * 6);
        stream.Launch(
            (Index1D)Length,
            index => Kernels.PredicateBarrierGroupsKernel(
                index, input.View, flags.View, output.View));
        stream.Synchronize();

        var values = output.GetAsArray1D();
        int groupSize = values[5];
        int groupMismatches = 0, warpMismatches = 0, uniformMismatches = 0;
        for (int i = 0; i < Length; i++)
        {
            int start = i / groupSize * groupSize;
            int end = Math.Min(start + groupSize, Length);
            int groupCount = 0, groupTrue = 0, warpCount = 0, warpTrue = 0;
            for (int j = start; j < end; j++)
            {
                bool predicate = inputData[j] != 0;
                ++groupCount;
                if (predicate)
                    ++groupTrue;
                if (values[j * 6 + 4] == values[i * 6 + 4])
                {
                    ++warpCount;
                    if (predicate)
                        ++warpTrue;
                }
            }

            int expectedGroup =
                Encode(groupTrue, groupTrue == groupCount, groupTrue > 0);
            int expectedWarp = Encode(warpTrue, warpTrue == warpCount, warpTrue > 0);
            if (values[i * 6] != expectedGroup)
                ++groupMismatches;
            if (values[i * 6 + 1] != expectedWarp)
                ++warpMismatches;
            if (values[i * 6 + 2] != Encode(groupCount, true, true) ||
                values[i * 6 + 3] != 0)
            {
                ++uniformMismatches;
            }
        }

        Console.WriteLine($"group mismatches {groupMismatches}");
        Console.WriteLine($"warp mismatches {warpMismatches}");
        Console.WriteLine($"uniform mismatches {uniformMismatches}");
        Console.WriteLine($"partial {Length % groupSize != 0}");
        Console.WriteLine($"groups {Length / groupSize > 1}");
    }
}
