// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: EarlyReturnLoopWithBarrier.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: EarlyReturnLoopWithBarrier
// A loop exchanges values between neighbouring threads of a group through
// shared memory and group barriers, and returns early in an iteration given
// by a kernel parameter, so all threads of a group return together. The
// first launch returns in the fourth iteration, the second never returns
// early.
// Each thread starts from thread % 9. Expected output: one result per thread
// for the first twelve threads of each launch.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void EarlyReturnLoopWithBarrierKernel(
        ArrayView1D<int, Stride1D.Dense> output,
        int limit)
    {
        var shared = Group.GetSharedMemory<int>(64);
        int g = Group.Index;
        int t = (int)Grid.GlobalThreadIndex;
        int acc = t % 9;
        for (int i = 0; i < 6; i++)
        {
            shared[g] = acc;
            Group.Barrier();
            acc += shared[g ^ 1] % 7 + i;
            Group.Barrier();
            if (i == limit)
            {
                output[t] = acc * 10 + i;
                return;
            }
        }
        output[t] = -acc;
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

        // Groups of one warp cover at least twelve threads
        int groupSize = accelerator.WarpSize;
        int groups = (12 + groupSize - 1) / groupSize;
        using var output = stream.Allocate1D<int>(groups * groupSize);
        stream.Launch(new KernelConfig(groups, groupSize), index =>
            Kernels.EarlyReturnLoopWithBarrierKernel(output.View, 3));
        stream.Synchronize();
        var early = output.GetAsArray1D();
        for (int i = 0; i < 12; i++)
            Console.WriteLine(early[i]);

        stream.Launch(new KernelConfig(groups, groupSize), index =>
            Kernels.EarlyReturnLoopWithBarrierKernel(output.View, 10));
        stream.Synchronize();

        var values = output.GetAsArray1D();
        for (int i = 0; i < 12; i++)
            Console.WriteLine(values[i]);
    }
}
