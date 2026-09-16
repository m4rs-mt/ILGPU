// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MergeWithShuffle.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: MergeWithShuffle
// Threads leave a loop in different iterations, either through a goto to a
// merge block or through the end of the loop and the code after it. The
// merge block exchanges values between neighbouring lanes of a warp through
// a shuffle that every lane reaches.
// Each thread uses x = thread % 9. Expected output: one result per thread
// for the first twelve threads.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void MergeWithShuffleKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        int t = (int)Grid.GlobalThreadIndex;
        int x = t % 9;
        int r = 0;
        for (int i = 0; i < 8; i++)
        {
            int v = (i * 5 + x * 3) % 11;
            if (v > 8)
            {
                r = i * 3 + x;
                goto merge;
            }
            r += v;
        }
        r = r * 2 + 1;
    merge:
        int other = Warp.ShuffleXor(r, 1);
        output[t] = r * 1000 + other;
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
            Kernels.MergeWithShuffleKernel(output.View));
        stream.Synchronize();

        var values = output.GetAsArray1D();
        for (int i = 0; i < 12; i++)
            Console.WriteLine(values[i]);
    }
}
