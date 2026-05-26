// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: UnsignedMinMax.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: UnsignedMinMax
// Math.Min/Math.Max on uint and ulong compare unsigned: values with the top
// bit set are large, not negative.
// Expected output: 10 4294967280 10 18446744073709551600

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void UnsignedMinMaxKernel(
        Index1D index,
        ArrayView1D<uint, Stride1D.Dense> u32,
        ArrayView1D<ulong, Stride1D.Dense> u64,
        ArrayView1D<ulong, Stride1D.Dense> output)
    {
        output[0] = Math.Min(u32[0], u32[1]);
        output[1] = Math.Max(u32[0], u32[1]);
        output[2] = Math.Min(u64[0], u64[1]);
        output[3] = Math.Max(u64[0], u64[1]);
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

        using var u32 = stream.Allocate1D<uint>(2);
        u32.View.CopyFromCPU(stream, new uint[] { 0xFFFFFFF0u, 10u });
        using var u64 = stream.Allocate1D<ulong>(2);
        u64.View.CopyFromCPU(stream, new ulong[] { 0xFFFFFFFFFFFFFFF0ul, 10ul });
        using var output = stream.Allocate1D<ulong>(4);
        stream.Launch((Index1D)1, index => Kernels.UnsignedMinMaxKernel(index, u32.View, u64.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
