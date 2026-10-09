// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructPassByValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: StructPassByValue
// Kernel passes struct to helper, modifies fields, sums result.
// Input: X=10, Y=100, Z=2, W=30 → modified: X=11, Y=110, Z=102, W=1030 → sum=1253
// Expected output: 1253 1253 1253 1253

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
    static TestStruct ModifyStruct(TestStruct s)
    {
        s.X = s.X + 1;
        s.Y = s.Y + 10L;
        s.Z = (short)(s.Z + 100);
        s.W = s.W + 1000;
        return s;
    }

    public static void StructPassByValueKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, TestStruct input)
    {
        TestStruct modified = ModifyStruct(input);
        data[index] = modified.X + (int)modified.Y + (int)modified.Z + modified.W;
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
        stream.Launch((Index1D)4, index => Kernels.StructPassByValueKernel(index, buffer.View, s));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
