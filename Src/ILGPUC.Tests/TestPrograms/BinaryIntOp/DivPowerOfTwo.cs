// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DivPowerOfTwo.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DivPowerOfTwo
// Divisions by powers of two round towards zero for negative signed values
// and treat the top bit of unsigned values as part of the magnitude.
// Expected output: 0 -1 -2 1 0 -1 2147483647 536870912 9223372036854775807

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void DivPowerOfTwoKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> i32,
        ArrayView1D<long, Stride1D.Dense> i64,
        ArrayView1D<uint, Stride1D.Dense> u32,
        ArrayView1D<ulong, Stride1D.Dense> u64,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        output[0] = i32[0] / 2;
        output[1] = i32[1] / 4;
        output[2] = i32[2] / 4;
        output[3] = i32[3] / 4;
        output[4] = i64[0] / 2;
        output[5] = i64[1] / 8;
        output[6] = u32[0] / 2;
        output[7] = u32[1] / 4;
        output[8] = (long)(u64[0] / 2);
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

        using var i32 = stream.Allocate1D<int>(new int[] { -1, -7, -8, 7 });
        using var i64 = stream.Allocate1D<long>(new long[] { -1L, -9L });
        using var u32 = stream.Allocate1D<uint>(new uint[] { 0xFFFFFFFEu, 0x80000000u });
        using var u64 = stream.Allocate1D<ulong>(new ulong[] { 0xFFFFFFFFFFFFFFFEul });
        using var output = stream.Allocate1D<long>(9);
        stream.Launch((Index1D)1, index => Kernels.DivPowerOfTwoKernel(index, i32.View, i64.View, u32.View, u64.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
