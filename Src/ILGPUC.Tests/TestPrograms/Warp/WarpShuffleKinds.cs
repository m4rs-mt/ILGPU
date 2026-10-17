// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpShuffleKinds.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: WarpShuffleKinds
// Shuffles, broadcasts, reductions and scans of values that differ per thread
// but come straight from the thread indices: Group.Index, the thread index
// parameter of an implicitly grouped kernel and the warp index within a group
// of two warps. A group of one warp shuffles its group indices down, up, by
// xor and from the next lane, reduces and scans them, broadcasts the last one
// across the group and a computed value of the first lane across the warp.
// The implicitly grouped kernel swaps neighbouring thread indices. Groups of
// two warps broadcast the warp index of their last thread and a computed
// value of a thread in the second warp, and each warp broadcasts a computed
// value of its first lane; this needs groups wider than a warp.
// Expected output: warp mismatches 0, index mismatches 0, then the warp index
// broadcast mismatches or that it was skipped.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void WarpShuffleKindsKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        int offset = Group.Index * 9;
        output[offset] = Warp.ShuffleDown(Group.Index, 1);
        output[offset + 1] = Warp.ShuffleUp(Group.Index, 1);
        output[offset + 2] = Warp.ShuffleXor(Group.Index, 1);
        output[offset + 3] =
            Warp.Shuffle(Group.Index, (Warp.LaneIndex + 1) % Warp.Dimension);
        output[offset + 4] = Warp.AllReduce(Group.Index, XMath.Max);
        output[offset + 5] = Group.AllReduce(Group.Index, int.MinValue, XMath.Max);
        output[offset + 6] = Group.Broadcast(Group.Index, Group.Dimension - 1);
        output[offset + 7] = Warp.InclusiveScan(Group.Index, XMath.Max);
        output[offset + 8] = Warp.Broadcast(Group.Index * 2 + 5);
    }

    public static void IndexShuffleKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = Warp.ShuffleXor((int)index, 1);
    }

    public static void WarpIndexBroadcastKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        int offset = (int)Grid.GlobalThreadIndex * 3;
        output[offset] = Group.Broadcast(Warp.Index, Group.Dimension - 1);
        output[offset + 1] = Group.Broadcast(Group.Index * 3 + 1, Warp.Dimension + 1);
        output[offset + 2] = Warp.Broadcast(Group.Index * 2);
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
        int warpSize = accelerator.WarpSize;

        using (var output = stream.Allocate1D<int>(warpSize * 9))
        {
            stream.Launch(new KernelConfig(1, warpSize), index =>
                Kernels.WarpShuffleKindsKernel(output.View));
            stream.Synchronize();

            var values = output.GetAsArray1D();
            int mismatches = 0;
            for (int lane = 0; lane < warpSize; ++lane)
            {
                int offset = lane * 9;
                // Lanes shuffled from outside the warp are left undefined
                if (lane < warpSize - 1 && values[offset] != lane + 1)
                    ++mismatches;
                if (lane > 0 && values[offset + 1] != lane - 1)
                    ++mismatches;
                if (values[offset + 2] != (lane ^ 1) ||
                    values[offset + 3] != (lane + 1) % warpSize ||
                    values[offset + 4] != warpSize - 1 ||
                    values[offset + 5] != warpSize - 1 ||
                    values[offset + 6] != warpSize - 1 ||
                    values[offset + 7] != lane ||
                    values[offset + 8] != 5)
                {
                    ++mismatches;
                }
            }
            Console.WriteLine($"warp mismatches {mismatches}");
        }

        const int Length = 64;
        using (var output = stream.Allocate1D<int>(Length))
        {
            stream.Launch((Index1D)Length, index =>
                Kernels.IndexShuffleKernel(index, output.View));
            stream.Synchronize();

            var values = output.GetAsArray1D();
            int mismatches = 0;
            for (int i = 0; i < Length; ++i)
            {
                if (values[i] != (i ^ 1))
                    ++mismatches;
            }
            Console.WriteLine($"index mismatches {mismatches}");
        }

        int groupSize = 2 * warpSize;
        if (groupSize > accelerator.MaxNumThreadsPerGroup)
        {
            Console.WriteLine("warp index broadcast skipped");
            return;
        }
        using (var output = stream.Allocate1D<int>(2 * groupSize * 3))
        {
            stream.Launch(new KernelConfig(2, groupSize), index =>
                Kernels.WarpIndexBroadcastKernel(output.View));
            stream.Synchronize();

            var values = output.GetAsArray1D();
            int mismatches = 0;
            for (int thread = 0; thread < 2 * groupSize; ++thread)
            {
                int group = thread % groupSize;
                int warpStart = group / warpSize * warpSize;
                if (values[thread * 3] != 1 ||
                    values[thread * 3 + 1] != (warpSize + 1) * 3 + 1 ||
                    values[thread * 3 + 2] != warpStart * 2)
                {
                    ++mismatches;
                }
            }
            Console.WriteLine($"warp index broadcast mismatches {mismatches}");
        }
    }
}
