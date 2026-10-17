// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PredicateBarrierInactiveLanes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: PredicateBarrierInactiveLanes
// Predicate barriers reached by only some threads of a group: every third
// thread returns early and the others take one of two branches, each with
// its own barrier. Only the threads that reach a barrier take part in it,
// like threads that have exited. The launch extent leaves the last group
// partially filled. The host recomputes every result from the group
// dimension the kernel reports; on the CPU backend a group is a single warp.
// Expected output: mismatches 0

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PredicateBarrierInactiveLanesKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> input,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index * 2 + 1] = Group.Dimension;
        if (index % 3 == 0)
            return;

        int value = input[index];
        bool predicate = value % 2 != 0;
        int result;
        if (value < 4)
        {
            result = Group.BarrierPopCount(predicate) * 4 +
                (Group.BarrierAnd(predicate) ? 2 : 0) +
                (Group.BarrierOr(predicate) ? 1 : 0);
        }
        else
        {
            result = 1000 + Warp.BarrierPopCount(predicate) * 4 +
                (Warp.BarrierAnd(predicate) ? 2 : 0) +
                (Warp.BarrierOr(predicate) ? 1 : 0);
        }
        output[index * 2] = result;
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        const int Length = 61;
        var inputData = new int[Length];
        for (int i = 0; i < Length; i++)
            inputData[i] = (i * 5 + i / 4) % 8;

        using var input = stream.Allocate1D(inputData);
        using var output = stream.Allocate1D<int>(Length * 2);
        output.MemSetToZero(stream);
        stream.Launch(
            (Index1D)Length,
            index => Kernels.PredicateBarrierInactiveLanesKernel(
                index, input.View, output.View));
        stream.Synchronize();

        var values = output.GetAsArray1D();
        int groupSize = values[1];
        int mismatches = 0;
        for (int i = 0; i < Length; i++)
        {
            int expected = 0;
            if (i % 3 != 0)
            {
                // The threads of the group that take the same branch
                bool low = inputData[i] < 4;
                int start = i / groupSize * groupSize;
                int end = Math.Min(start + groupSize, Length);
                int count = 0, trueCount = 0;
                for (int j = start; j < end; j++)
                {
                    if (j % 3 == 0 || inputData[j] < 4 != low)
                        continue;
                    ++count;
                    if (inputData[j] % 2 != 0)
                        ++trueCount;
                }
                expected = (low ? 0 : 1000) + trueCount * 4 +
                    (trueCount == count ? 2 : 0) + (trueCount > 0 ? 1 : 0);
            }
            if (values[i * 2] != expected)
                ++mismatches;
        }

        Console.WriteLine($"mismatches {mismatches}");
    }
}
