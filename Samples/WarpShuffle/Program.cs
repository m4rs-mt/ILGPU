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

namespace WarpShuffle;

public readonly struct ComplexStruct
{
    public ComplexStruct(int x, float y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public int X { get; }
    public float Y { get; }
    public double Z { get; }

    public override string ToString() =>
        $"X: {X}, Y: {Y}, Z: {Z}";
}

static class Kernels
{
    /// <summary>
    /// ShuffleDown: shift values down by a given delta.
    /// </summary>
    public static void ShuffleDownKernel(
        ArrayView1D<int, Stride1D.Dense> dataTarget,
        int delta)
    {
        int value = Group.Index;
        value = Warp.ShuffleDown(value, delta);

        dataTarget[Grid.GlobalThreadIndex] = value;
    }

    /// <summary>
    /// Generic shuffle: broadcast a complex struct from a given lane.
    /// </summary>
    public static void GenericShuffleKernel(
        ArrayView1D<ComplexStruct, Stride1D.Dense> dataTarget,
        ComplexStruct inputValue,
        int sourceLane)
    {
        var value = Warp.Shuffle(inputValue, sourceLane);
        dataTarget[Grid.GlobalThreadIndex] = value;
    }

    /// <summary>
    /// Warp collectives: AllReduce, ExclusiveScan, InclusiveScan.
    /// </summary>
    public static void WarpCollectivesKernel(
        ArrayView1D<int, Stride1D.Dense> dataTarget,
        int warpSize)
    {
        var laneIndex = Warp.LaneIndex;

        // AllReduce: sum 1 across all lanes (result = warpSize)
        int reduced = Warp.AllReduce(1, (a, b) => a + b);
        dataTarget[laneIndex] = reduced;

        // ExclusiveScan: running sum excluding current lane
        int exclusive = Warp.ExclusiveScan(1, 0, (a, b) => a + b);
        dataTarget[warpSize + laneIndex] = exclusive;

        // InclusiveScan: running sum including current lane
        int inclusive = Warp.InclusiveScan(1, (a, b) => a + b);
        dataTarget[warpSize * 2 + laneIndex] = inclusive;

        // AllReduce with Min: find the minimum of (laneIndex + 1)
        int minReduced = Warp.AllReduce(laneIndex + 1, (a, b) => XMath.Min(a, b));
        dataTarget[warpSize * 3 + laneIndex] = minReduced;
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

        int warpSize = accelerator.WarpSize;
        var config = new KernelConfig(gridDim: 1, groupDim: warpSize);

        // 1) ShuffleDown: shift values down by 2 lanes
        {
            using var dataTarget = stream.Allocate1D<int>(warpSize);
            dataTarget.MemSetToZero();

            stream.Launch(in config, index =>
                Kernels.ShuffleDownKernel(dataTarget.View, 2));
            stream.Synchronize();

            Console.WriteLine("Shuffle-down kernel");
            var target = dataTarget.GetAsArray1D();
            for (int i = 0, e = target.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {target[i]}");
        }

        // 2) Generic shuffle: broadcast a complex struct from lane 0
        {
            using var dataTarget = stream.Allocate1D<ComplexStruct>(warpSize);
            dataTarget.MemSetToZero();

            var inputValue = new ComplexStruct(2, 40.0f, 16.0);

            stream.Launch(in config, index =>
                Kernels.GenericShuffleKernel(dataTarget.View, inputValue, 0));
            stream.Synchronize();

            Console.WriteLine("Generic shuffle kernel");
            var target = dataTarget.GetAsArray1D();
            for (int i = 0, e = target.Length; i < e; ++i)
                Console.WriteLine($"Data[{i}] = {target[i]}");
        }

        // 3) Warp collectives: AllReduce, InclusiveScan, ExclusiveScan
        {
            using var dataTarget = stream.Allocate1D<int>(warpSize * 4);
            dataTarget.MemSetToZero();

            stream.Launch(in config, index =>
                Kernels.WarpCollectivesKernel(dataTarget.View, warpSize));
            stream.Synchronize();

            Console.WriteLine("Warp AllReduce (sum)");
            var target = dataTarget.GetAsArray1D();
            for (int i = 0; i < warpSize; ++i)
                Console.WriteLine($"  Lane[{i}] = {target[i]}");

            Console.WriteLine("Warp ExclusiveScan (sum)");
            for (int i = 0; i < warpSize; ++i)
                Console.WriteLine($"  Lane[{i}] = {target[warpSize + i]}");

            Console.WriteLine("Warp InclusiveScan (sum)");
            for (int i = 0; i < warpSize; ++i)
                Console.WriteLine($"  Lane[{i}] = {target[warpSize * 2 + i]}");

            Console.WriteLine("Warp AllReduce (min)");
            for (int i = 0; i < warpSize; ++i)
                Console.WriteLine($"  Lane[{i}] = {target[warpSize * 3 + i]}");
        }

        Console.WriteLine("Done.");
    }
}
