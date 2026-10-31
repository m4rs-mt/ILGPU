// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupedScalarFirstParameter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: GroupedScalarFirstParameter
// Explicitly grouped launches of kernels without an index parameter whose first
// parameter looks like an index: an int, as Index1D is passed, structs of two
// or three ints or longs, as the 2D and 3D indices are passed, and a struct of
// a long and an int, as KernelIndex is passed. They are kernel arguments,
// uniform for all threads, which must not be replaced by the thread index.

using System;
using ILGPU;
using ILGPU.Runtime;

struct Offsets
{
    public int Bias;
    public int Scale;
}

struct WideOffsets
{
    public long Bias;
    public int Scale;
}

struct TripleOffsets
{
    public int Bias;
    public int Scale;
    public int Step;
}

struct LongOffsets
{
    public long Bias;
    public long Scale;
}

struct LongTripleOffsets
{
    public long Bias;
    public long Scale;
    public long Step;
}

static class Kernels
{
    public static void IntFirstKernel(
        int bias,
        ArrayView1D<int, Stride1D.Dense> data,
        int scale)
    {
        int globalIndex = Group.Dimension * (int)Grid.Index + Group.Index;
        data[globalIndex] = bias + (int)Grid.Index * 100 + Group.Index * scale;
    }

    public static void StructFirstKernel(
        Offsets offsets,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int globalIndex = Group.Dimension * (int)Grid.Index + Group.Index;
        data[globalIndex] =
            offsets.Bias + (int)Grid.Index * 100 + Group.Index * offsets.Scale;
    }

    public static void WideStructFirstKernel(
        WideOffsets offsets,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int globalIndex = Group.Dimension * (int)Grid.Index + Group.Index;
        data[globalIndex] =
            (int)offsets.Bias + (int)Grid.Index * 100 + Group.Index * offsets.Scale;
    }

    public static void TripleStructFirstKernel(
        TripleOffsets offsets,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int globalIndex = Group.Dimension * (int)Grid.Index + Group.Index;
        data[globalIndex] = offsets.Bias + (int)Grid.Index * offsets.Step +
            Group.Index * offsets.Scale;
    }

    public static void LongStructFirstKernel(
        LongOffsets offsets,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int globalIndex = Group.Dimension * (int)Grid.Index + Group.Index;
        data[globalIndex] = (int)offsets.Bias + (int)Grid.Index * 100 +
            Group.Index * (int)offsets.Scale;
    }

    public static void LongTripleStructFirstKernel(
        LongTripleOffsets offsets,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int globalIndex = Group.Dimension * (int)Grid.Index + Group.Index;
        data[globalIndex] = (int)offsets.Bias + (int)Grid.Index * (int)offsets.Step +
            Group.Index * (int)offsets.Scale;
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

        var config = new KernelConfig(2, 4);
        using var buffer = stream.Allocate1D<int>(8);
        var view = buffer.View;

        int bias = 1000;
        int scale = 3;
        stream.Launch(in config, _ => Kernels.IntFirstKernel(bias, view, scale));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));

        var offsets = new Offsets { Bias = 2000, Scale = 5 };
        stream.Launch(in config, _ => Kernels.StructFirstKernel(offsets, view));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));

        var wideOffsets = new WideOffsets { Bias = 3000, Scale = 7 };
        stream.Launch(in config, _ => Kernels.WideStructFirstKernel(wideOffsets, view));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));

        var tripleOffsets = new TripleOffsets { Bias = 4000, Scale = 2, Step = 300 };
        stream.Launch(
            in config,
            _ => Kernels.TripleStructFirstKernel(tripleOffsets, view));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));

        var longOffsets = new LongOffsets { Bias = 5000, Scale = 11 };
        stream.Launch(in config, _ => Kernels.LongStructFirstKernel(longOffsets, view));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));

        var longTripleOffsets =
            new LongTripleOffsets { Bias = 6000, Scale = 13, Step = 400 };
        stream.Launch(
            in config,
            _ => Kernels.LongTripleStructFirstKernel(longTripleOffsets, view));
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
