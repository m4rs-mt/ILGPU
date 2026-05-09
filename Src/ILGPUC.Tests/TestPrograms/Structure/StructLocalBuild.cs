// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructLocalBuild.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: StructLocalBuild
// Kernel builds a struct locally (alloca) with per-field writes, then
// outputs a summary. Exercises LoadFieldAddress on local struct allocas
// — the vectorized LFA path on CPU.
// Expected output: 10 10 10 10
// (value=1: 1 + 2 + 3 + 4 = 10)

using System;
using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Runtime;

[StructLayout(LayoutKind.Sequential)]
struct TestStruct
{
    public int X;
    public long Y;
    public short Z;
    public int W;
}

static class Kernels
{
    public static void StructLocalBuildKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        TestStruct local;
        local.X = value;
        local.Y = value * 2L;
        local.Z = (short)(value * 3);
        local.W = value * 4;
        data[index] = local.X + (int)local.Y + (int)local.Z + local.W;
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

        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index =>
            Kernels.StructLocalBuildKernel(index, buffer.View, 1));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
