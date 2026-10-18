// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NestedConditionalBoolArm.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NestedConditionalBoolArm
// Nested conditionals whose inner arm is a 0/1 selection. In Release IL, Roslyn emits
// the 0/1 selection as a comparison, so one predecessor of the integer join pushes a
// bool. dir = [-1, -1, 1, 1], flag = [0, 1, 0, 1], n = 10.
// Expected output (one per line):
//   Select:      2 2 1 0
//   NestedArms:  9 8 0 1
//   ConstArms:   42 17 0 1
//   FloatTarget: 2 2 1 0
//   BoolToFloat: 0 1 0 1

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SelectKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> dir,
        ArrayView1D<int, Stride1D.Dense> flag,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        output[index] = dir[index] < 0 ? 2 : flag[index] != 0 ? 0 : 1;
    }

    public static void NestedArmsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> dir,
        ArrayView1D<int, Stride1D.Dense> flag,
        ArrayView1D<int, Stride1D.Dense> output,
        int n)
    {
        bool s = flag[index] != 0;
        output[index] = dir[index] < 0 ? (s ? n - 2 : n - 1) : (s ? 1 : 0);
    }

    public static void ConstArmsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> dir,
        ArrayView1D<int, Stride1D.Dense> flag,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        bool s = flag[index] != 0;
        output[index] = dir[index] < 0 ? (s ? 17 : 42) : (s ? 1 : 0);
    }

    public static void FloatTargetKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> dir,
        ArrayView1D<int, Stride1D.Dense> flag,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        float r = dir[index] < 0 ? 2 : flag[index] != 0 ? 0 : 1;
        output[index] = r;
    }

    public static void BoolToFloatKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> flag,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        float r = flag[index] != 0 ? 1 : 0;
        output[index] = r;
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

        using var dir = stream.Allocate1D(new[] { -1, -1, 1, 1 });
        using var flag = stream.Allocate1D(new[] { 0, 1, 0, 1 });
        using var select = stream.Allocate1D<int>(4);
        using var nestedArms = stream.Allocate1D<int>(4);
        using var constArms = stream.Allocate1D<int>(4);
        using var floatTarget = stream.Allocate1D<float>(4);
        using var boolToFloat = stream.Allocate1D<float>(4);

        stream.Launch((Index1D)4, index =>
            Kernels.SelectKernel(index, dir.View, flag.View, select.View));
        stream.Launch((Index1D)4, index =>
            Kernels.NestedArmsKernel(index, dir.View, flag.View, nestedArms.View, 10));
        stream.Launch((Index1D)4, index =>
            Kernels.ConstArmsKernel(index, dir.View, flag.View, constArms.View));
        stream.Launch((Index1D)4, index =>
            Kernels.FloatTargetKernel(index, dir.View, flag.View, floatTarget.View));
        stream.Launch((Index1D)4, index =>
            Kernels.BoolToFloatKernel(index, flag.View, boolToFloat.View));
        stream.Synchronize();

        foreach (var v in select.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in nestedArms.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in constArms.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in floatTarget.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in boolToFloat.GetAsArray1D())
            Console.WriteLine(v);
    }
}
