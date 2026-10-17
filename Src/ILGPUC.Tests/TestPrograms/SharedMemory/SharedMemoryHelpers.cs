// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemoryHelpers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: SharedMemoryHelpers
// Helper methods that are not inlined allocate shared memory, synchronize the
// group and read the warp indices. The kernel calls an outer helper that uses
// none of them itself but calls the inner helper, which exchanges each
// thread's value with its neighbour through shared memory. A second helper
// returns the warp lane and warp index. Runs groups of one warp.
// Expected output: neighbour mismatches 0, warp mismatches 0

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    const int MaxGroupSize = 64;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Exchange(int value)
    {
        var shared = Group.GetSharedMemory<int>(MaxGroupSize);
        shared[Group.Index] = value;
        Group.Barrier();
        int neighbour = shared[(Group.Index + 1) % Group.Dimension];
        Group.Barrier();
        return neighbour;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Outer(int value) => Exchange(value * 2) + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int WarpPosition() => Warp.Index * 1000 + Warp.LaneIndex;

    public static void SharedMemoryHelpersKernel(
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int global = (int)Grid.GlobalThreadIndex;
        output[global * 2] = Outer(global);
        output[global * 2 + 1] = WarpPosition();
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

        int groupSize = Math.Min(accelerator.WarpSize, 64);
        const int GridSize = 3;
        using var output = stream.Allocate1D<int>(GridSize * groupSize * 2);
        stream.Launch(new KernelConfig(GridSize, groupSize), index =>
            Kernels.SharedMemoryHelpersKernel(output.View));
        stream.Synchronize();

        var values = output.GetAsArray1D();
        int neighbours = 0;
        int warps = 0;
        for (int i = 0; i < GridSize * groupSize; ++i)
        {
            int group = i % groupSize;
            int next = i - group + (group + 1) % groupSize;
            if (values[i * 2] != next * 2 + 1)
                ++neighbours;
            if (values[i * 2 + 1] != group)
                ++warps;
        }
        Console.WriteLine($"neighbour mismatches {neighbours}");
        Console.WriteLine($"warp mismatches {warps}");
    }
}
