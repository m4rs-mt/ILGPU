// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructFieldAccess.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: StructFieldAccess
// Kernel reads fields from a struct parameter.
// Expected output: 142 142 142 142

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
    public static void StructFieldAccessKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, TestStruct s)
    {
        data[index] = s.X + (int)s.Y + (int)s.Z + s.W;
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
        var s = new TestStruct { X = 10, Y = 100L, Z = 2, W = 30 };
        stream.Launch((Index1D)4, index => Kernels.StructFieldAccessKernel(index, buffer.View, s));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
