// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Linq;
using ILGPU;
using ILGPU.Runtime;

namespace ExplicitlyGroupedKernels;

static class Kernels
{
    /// <summary>
    /// Default grouped kernel: writes globalIndex + constant.
    /// </summary>
    public static void DefaultGroupedKernel(
        ArrayView1D<int, Stride1D.Dense> dataTarget,
        int constant)
    {
        var globalIndex = Grid.GlobalThreadIndex;

        if (globalIndex < dataTarget.Length)
            dataTarget[globalIndex] = (int)globalIndex + constant;
    }

    /// <summary>
    /// Grouped kernel with barrier: threshold test on source data.
    /// </summary>
    public static void GroupedBarrierKernel(
        ArrayView1D<int, Stride1D.Dense> dataSource,
        ArrayView1D<int, Stride1D.Dense> dataTarget,
        int threshold)
    {
        var globalIndex = Grid.GlobalThreadIndex;

        // Wait until all threads in the group reach this point
        Group.Barrier();

        if (globalIndex < dataSource.Length)
            dataTarget[globalIndex] =
                dataSource[globalIndex] > threshold ? 1 : 0;
    }

    /// <summary>
    /// Grouped kernel with and-barrier.
    /// </summary>
    public static void GroupedAndBarrierKernel(
        ArrayView1D<int, Stride1D.Dense> dataSource,
        ArrayView1D<int, Stride1D.Dense> dataTarget)
    {
        var globalIndex = Grid.GlobalThreadIndex;

        var value = globalIndex < dataSource.Length
            ? dataSource[globalIndex]
            : 0 + 1; // constant + 1

        // BarrierAnd returns true if the predicate is true for ALL threads
        var found = Group.BarrierAnd(value > 0);

        if (globalIndex < dataTarget.Length)
            dataTarget[globalIndex] = found ? 1 : 0;
    }

    /// <summary>
    /// Grouped kernel with or-barrier.
    /// </summary>
    public static void GroupedOrBarrierKernel(
        ArrayView1D<int, Stride1D.Dense> dataSource,
        ArrayView1D<int, Stride1D.Dense> dataTarget,
        int threshold)
    {
        var globalIndex = Grid.GlobalThreadIndex;

        var value = globalIndex < dataSource.Length
            ? dataSource[globalIndex]
            : threshold;

        // BarrierOr returns true if the predicate is true for ANY thread
        var found = Group.BarrierOr(value > threshold);

        if (globalIndex < dataTarget.Length)
            dataTarget[globalIndex] = found ? 1 : 0;
    }

    /// <summary>
    /// Grouped kernel with popcount-barrier.
    /// </summary>
    public static void GroupedPopCountBarrierKernel(
        ArrayView1D<int, Stride1D.Dense> dataSource,
        ArrayView1D<int, Stride1D.Dense> dataTarget)
    {
        var globalIndex = Grid.GlobalThreadIndex;

        var value = globalIndex < dataSource.Length
            ? dataSource[globalIndex]
            : 0;

        // BarrierPopCount returns the number of threads for which the
        // predicate evaluates to true
        var count = Group.BarrierPopCount(value > 0);

        if (globalIndex < dataTarget.Length)
            dataTarget[globalIndex] = count;
    }

    /// <summary>
    /// Group AllReduce kernel: sums 1 across all threads in the group.
    /// </summary>
    public static void GroupAllReduceKernel(
        ArrayView1D<int, Stride1D.Dense> dataTarget)
    {
        var globalIndex = Grid.GlobalThreadIndex;

        // AllReduce sums 1 across all threads in the group
        // (result = groupSize for every thread)
        int reduced = Group.AllReduce(1, 0, (a, b) => a + b);

        if (globalIndex < dataTarget.Length)
            dataTarget[globalIndex] = reduced;
    }

    /// <summary>
    /// Group InclusiveScan kernel: computes a running sum across threads.
    /// </summary>
    public static void GroupInclusiveScanKernel(
        ArrayView1D<int, Stride1D.Dense> dataTarget)
    {
        var globalIndex = Grid.GlobalThreadIndex;

        // InclusiveScan computes a running sum across threads in the group
        int scanned = Group.InclusiveScan(1, 0, (a, b) => a + b);

        if (globalIndex < dataTarget.Length)
            dataTarget[globalIndex] = scanned;
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.CreateDefault();

        var device = context.Devices
            .OrderByDescending(d => d.AcceleratorType switch
            {
                AcceleratorType.Metal  => 5,
                AcceleratorType.Cuda   => 4,
                AcceleratorType.ROCm   => 3,
                AcceleratorType.OpenCL => 2,
                AcceleratorType.CPU    => 1,
                _                      => 0,
            })
            .First();

        using var accelerator = device.CreateAccelerator(context);
        Console.WriteLine($"Using {accelerator}");
        var stream = accelerator.DefaultStream;

        var data = Enumerable.Range(1, 128).ToArray();

        int groupSize = accelerator.MaxNumThreadsPerGroup;
        int numGroups = (data.Length + groupSize - 1) / groupSize;
        var config = new KernelConfig(gridDim: numGroups, groupDim: groupSize);

        using var dataSource = stream.Allocate1D<int>(data.Length);
        dataSource.CopyFromCPU(data);

        using var dataTarget = stream.Allocate1D<int>(data.Length);

        // 1) Default grouped kernel
        {
            dataTarget.MemSetToZero();

            stream.Launch(in config, index =>
                Kernels.DefaultGroupedKernel(dataTarget.View, 64));
            stream.Synchronize();

            Console.WriteLine("Default grouped kernel");
            var target = dataTarget.GetAsArray1D();
            for (int i = 0, e = target.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {target[i]}");
        }

        // 2) Grouped kernel with barrier
        {
            dataTarget.MemSetToZero();

            stream.Launch(in config, index =>
                Kernels.GroupedBarrierKernel(dataSource.View, dataTarget.View, 64));
            stream.Synchronize();

            Console.WriteLine("Grouped-barrier kernel");
            var target = dataTarget.GetAsArray1D();
            for (int i = 0, e = target.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {target[i]}");
        }

        // 3) Grouped kernel with and-barrier
        {
            dataTarget.MemSetToZero();

            stream.Launch(in config, index =>
                Kernels.GroupedAndBarrierKernel(dataSource.View, dataTarget.View));
            stream.Synchronize();

            Console.WriteLine("Grouped-and-barrier kernel");
            var target = dataTarget.GetAsArray1D();
            for (int i = 0, e = target.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {target[i]}");
        }

        // 4) Grouped kernel with or-barrier
        {
            dataTarget.MemSetToZero();

            stream.Launch(in config, index =>
                Kernels.GroupedOrBarrierKernel(dataSource.View, dataTarget.View, 64));
            stream.Synchronize();

            Console.WriteLine("Grouped-or-barrier kernel");
            var target = dataTarget.GetAsArray1D();
            for (int i = 0, e = target.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {target[i]}");
        }

        // 5) Grouped kernel with popcount-barrier
        {
            dataTarget.MemSetToZero();

            stream.Launch(in config, index =>
                Kernels.GroupedPopCountBarrierKernel(dataSource.View, dataTarget.View));
            stream.Synchronize();

            Console.WriteLine("Grouped-popcount-barrier kernel");
            var target = dataTarget.GetAsArray1D();
            for (int i = 0, e = target.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {target[i]}");
        }

        // 6) Group collectives: AllReduce and InclusiveScan
        {
            dataTarget.MemSetToZero();

            stream.Launch(in config, index =>
                Kernels.GroupAllReduceKernel(dataTarget.View));
            stream.Synchronize();

            Console.WriteLine("Group AllReduce kernel");
            var target = dataTarget.GetAsArray1D();
            for (int i = 0, e = target.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {target[i]}");
        }

        {
            dataTarget.MemSetToZero();

            stream.Launch(in config, index =>
                Kernels.GroupInclusiveScanKernel(dataTarget.View));
            stream.Synchronize();

            Console.WriteLine("Group InclusiveScan kernel");
            var target = dataTarget.GetAsArray1D();
            for (int i = 0, e = target.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {target[i]}");
        }

        Console.WriteLine("Done.");
    }
}
