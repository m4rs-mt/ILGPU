// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedStoreTail.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: SharedStoreTail
// Two pairs of switch cases compute different values through goto case before one
// store, and a store selects a value behind a condition, an atomic and a load. The
// small blocks in front of the stores are duplicated, the stores are emitted once.
// Each lane uses x = index % 9; twelve lanes span two SIMD groups.
// Expected output: the switch results, the guarded results and the atomic counter.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SharedStoreTailSwitchKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int x = index.X % 9;
        int r = x;
        switch (x % 4)
        {
            case 0:
                r = x + 5;
                goto case 1;
            case 1:
                r += 7;
                break;
            case 2:
                r = x * 3;
                goto default;
            default:
                r += 100;
                break;
        }
        output[index] = r;
    }

    public static void SharedStoreTailGuardKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output,
        ArrayView1D<int, Stride1D.Dense> counter)
    {
        int x = index.X % 9;
        output[index] = x > 1 && Atomic.Add(ref counter[0], 1) >= 0 && data[x] > 3 ? x * 2 : -x;
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

        var values = new int[9];
        for (int i = 0; i < values.Length; i++)
            values[i] = i * 7 % 10;

        using var switchOutput = stream.Allocate1D<int>(12);
        using var data = stream.Allocate1D(values);
        using var guardOutput = stream.Allocate1D<int>(12);
        using var counter = stream.Allocate1D<int>(1);
        counter.MemSetToZero();

        stream.Launch((Index1D)12, index => Kernels.SharedStoreTailSwitchKernel(index, switchOutput.View));
        stream.Launch((Index1D)12, index => Kernels.SharedStoreTailGuardKernel(
            index, data.View, guardOutput.View, counter.View));
        stream.Synchronize();

        foreach (var v in switchOutput.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in guardOutput.GetAsArray1D())
            Console.WriteLine(v);
        Console.WriteLine(counter.GetAsArray1D()[0]);
    }
}
