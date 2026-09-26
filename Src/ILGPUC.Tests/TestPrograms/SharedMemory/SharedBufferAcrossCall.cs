// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedBufferAcrossCall.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: SharedBufferAcrossCall
// A helper that is inlined returns a shared memory buffer. The kernel fills the
// buffer through it, and a helper that is not inlined reads the neighbour's value
// through it, so both must see the same buffer. Runs groups of one warp.
// Expected output: neighbour mismatches 0

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    const int MaxGroupSize = 64;

    static ArrayView<int> Buffer() => Group.GetSharedMemory<int>(MaxGroupSize);

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int ReadNeighbour()
    {
        var shared = Buffer();
        return shared[(Group.Index + 1) % Group.Dimension];
    }

    public static void SharedBufferAcrossCallKernel(
        ArrayView1D<int, Stride1D.Dense> output)
    {
        var shared = Buffer();
        shared[Group.Index] = (int)Grid.GlobalThreadIndex;
        Group.Barrier();
        output[(int)Grid.GlobalThreadIndex] = ReadNeighbour();
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
        using var output = stream.Allocate1D<int>(GridSize * groupSize);
        stream.Launch(new KernelConfig(GridSize, groupSize), index =>
            Kernels.SharedBufferAcrossCallKernel(output.View));
        stream.Synchronize();

        var values = output.GetAsArray1D();
        int neighbours = 0;
        for (int i = 0; i < GridSize * groupSize; ++i)
        {
            int group = i % groupSize;
            int next = i - group + (group + 1) % groupSize;
            if (values[i] != next)
                ++neighbours;
        }
        Console.WriteLine($"neighbour mismatches {neighbours}");
    }
}
