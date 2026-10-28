// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedBufferInlinedTwice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: SharedBufferInlinedTwice
// A GetSharedMemory call site yields one buffer per kernel, also in a helper
// inlined twice: the kernel writes through the buffer of the first inlined call
// and reads through the buffer of the second. A second kernel of the same
// program inlines the same helper. Runs groups of several warps where the
// device allows.
// Expected output: shared twice mismatches 0, other kernel mismatches 0

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public const int MaxGroupSize = 256;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ArrayView<int> Buffer() => Group.GetSharedMemory<int>(MaxGroupSize);

    public static void SharedTwiceKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        var first = Buffer();
        var second = Buffer();
        int global = (int)Grid.GlobalThreadIndex;
        first[Group.Index] = global;
        Group.Barrier();
        output[global] = second[(Group.Index + 1) % Group.Dimension];
    }

    public static void SharedOtherKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        var shared = Buffer();
        int global = (int)Grid.GlobalThreadIndex;
        shared[Group.Index] = -global - 1;
        Group.Barrier();
        output[global] = shared[(Group.Index + 1) % Group.Dimension];
    }
}

static class Program
{
    static int Neighbour(int index, int groupSize)
    {
        int lane = index % groupSize;
        return index - lane + (lane + 1) % groupSize;
    }

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        int groupSize = Math.Min(accelerator.MaxNumThreadsPerGroup, Kernels.MaxGroupSize);
        const int GridSize = 3;
        int total = GridSize * groupSize;
        var config = new KernelConfig(GridSize, groupSize);
        using var output = stream.Allocate1D<int>(total);

        stream.Launch(in config, index => Kernels.SharedTwiceKernel(output.View));
        stream.Synchronize();
        var values = output.GetAsArray1D();
        int shared = 0;
        for (int i = 0; i < total; ++i)
        {
            if (values[i] != Neighbour(i, groupSize))
                ++shared;
        }

        stream.Launch(in config, index => Kernels.SharedOtherKernel(output.View));
        stream.Synchronize();
        values = output.GetAsArray1D();
        int other = 0;
        for (int i = 0; i < total; ++i)
        {
            if (values[i] != -Neighbour(i, groupSize) - 1)
                ++other;
        }

        Console.WriteLine($"shared twice mismatches {shared}");
        Console.WriteLine($"other kernel mismatches {other}");
    }
}
