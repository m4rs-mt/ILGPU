// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalMemoryPerThread.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: LocalMemoryPerThread
// Group.GetLocalMemoryPerThread allocates a buffer of local memory private to
// every thread, also in 2D and 3D and in a helper that is not inlined. Every
// thread fills its buffers, the group synchronizes so that the threads of the
// group interleave, and every thread reads its own values back. Runs several
// group sizes, of several warps where the device allows.
// Expected output: local 1D mismatches 0, local helper mismatches 0,
// local 2D mismatches 0, local 3D mismatches 0

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public const int Length = 5;
    public const int Outputs = 5;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static long HelperSum(int global)
    {
        var local = Group.GetLocalMemoryPerThread<long>(Length);
        for (int i = 0; i < Length; ++i)
            local[i] = global * 7L + i;
        Group.Barrier();
        long sum = 0;
        for (int i = 0; i < Length; ++i)
            sum += local[i] * (i + 1);
        return sum;
    }

    public static void LocalPerThreadKernel(ArrayView1D<int, Stride1D.Dense> output)
    {
        int global = (int)Grid.GlobalThreadIndex;
        var local = Group.GetLocalMemoryPerThread<int>(Length);
        var grid = Group.GetLocalMemoryPerThread2D<int, Stride2D.DenseX>(
            new Index2D(3, 2));
        var cube = Group.GetLocalMemoryPerThread3D<short, Stride3D.DenseXY>(
            new Index3D(2, 3, 2));

        for (int i = 0; i < Length; ++i)
            local[i] = global * Length + i;
        for (int x = 0; x < 3; ++x)
        {
            for (int y = 0; y < 2; ++y)
                grid[new Index2D(x, y)] = global * 100 + x * 10 + y;
        }
        for (int x = 0; x < 2; ++x)
        {
            for (int y = 0; y < 3; ++y)
            {
                for (int z = 0; z < 2; ++z)
                {
                    cube[new Index3D(x, y, z)] =
                        (short)(global % 1000 + x * 4 + y * 2 + z);
                }
            }
        }
        Group.Barrier();

        int sum = 0;
        for (int i = 0; i < Length; ++i)
            sum += local[i] * (i + 1);

        int gridSum = 0;
        for (int x = 0; x < 3; ++x)
        {
            for (int y = 0; y < 2; ++y)
                gridSum += grid[new Index2D(x, y)] * (x + 3 * y + 1);
        }
        int cubeSum = 0;
        for (int x = 0; x < 2; ++x)
        {
            for (int y = 0; y < 3; ++y)
            {
                for (int z = 0; z < 2; ++z)
                    cubeSum += cube[new Index3D(x, y, z)] * (x + 2 * y + 6 * z + 1);
            }
        }

        int offset = global * Outputs;
        output[offset] = sum;
        output[offset + 1] = local.IntLength;
        output[offset + 2] = (int)HelperSum(global);
        output[offset + 3] = gridSum;
        output[offset + 4] = cubeSum;
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
        int maxGroupSize = Math.Min(accelerator.MaxNumThreadsPerGroup, 1024);
        var groupSizes = new SortedSet<int>();
        foreach (int size in new[]
        {
            Math.Max(warpSize / 2, 1), warpSize, warpSize * 3, warpSize * 8,
            maxGroupSize,
        })
        {
            if (size <= maxGroupSize)
                groupSizes.Add(size);
        }

        const int GridSize = 3;
        int local = 0;
        int helper = 0;
        int grid = 0;
        int cube = 0;
        foreach (int groupSize in groupSizes)
        {
            int total = GridSize * groupSize;
            using var output = stream.Allocate1D<int>(total * Kernels.Outputs);
            stream.Launch(new KernelConfig(GridSize, groupSize), index =>
                Kernels.LocalPerThreadKernel(output.View));
            stream.Synchronize();

            var values = output.GetAsArray1D();
            for (int g = 0; g < total; ++g)
            {
                int offset = g * Kernels.Outputs;
                int sum = 0;
                for (int i = 0; i < Kernels.Length; ++i)
                    sum += (g * Kernels.Length + i) * (i + 1);
                if (values[offset] != sum || values[offset + 1] != Kernels.Length)
                    ++local;

                long helperSum = 0;
                for (int i = 0; i < Kernels.Length; ++i)
                    helperSum += (g * 7L + i) * (i + 1);
                if (values[offset + 2] != (int)helperSum)
                    ++helper;

                int gridSum = 0;
                for (int x = 0; x < 3; ++x)
                {
                    for (int y = 0; y < 2; ++y)
                        gridSum += (g * 100 + x * 10 + y) * (x + 3 * y + 1);
                }
                if (values[offset + 3] != gridSum)
                    ++grid;

                int cubeSum = 0;
                for (int x = 0; x < 2; ++x)
                {
                    for (int y = 0; y < 3; ++y)
                    {
                        for (int z = 0; z < 2; ++z)
                        {
                            cubeSum += (short)(g % 1000 + x * 4 + y * 2 + z) *
                                (x + 2 * y + 6 * z + 1);
                        }
                    }
                }
                if (values[offset + 4] != cubeSum)
                    ++cube;
            }
        }

        Console.WriteLine($"local 1D mismatches {local}");
        Console.WriteLine($"local helper mismatches {helper}");
        Console.WriteLine($"local 2D mismatches {grid}");
        Console.WriteLine($"local 3D mismatches {cube}");
    }
}
