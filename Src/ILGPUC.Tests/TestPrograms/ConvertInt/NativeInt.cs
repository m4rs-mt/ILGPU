// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NativeInt.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: NativeInt
// Kernel computes with nint and nuint locals and helper parameters: arithmetic,
// shifts, signed and unsigned comparisons, comparisons with 0, negation,
// complement and the zero extension of uint to nuint.
// Expected output per lane: the seven values stored by the kernel (see the
// comments at the stores).

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Half(nint value) => value == 0 ? -1 : (int)(value >> 1);

    static int Sign(nint value) => value < 0 ? -1 : value > 0 ? 1 : 0;

    static int Low(nuint value) => value >= 8 ? (int)(value & 7) : (int)value;

    public static void NativeIntKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> output)
    {
        int i = index;
        int o = i * 7;
        nint v = i;
        // -1 0 1 1
        output[o] = Half(v);
        v = v * 3 + 1;
        // 0 2 3 5
        output[o + 1] = Half(v);
        nuint u = (nuint)v << 2;
        // 2 8 14 20
        output[o + 2] = (long)(u >> 1);
        // -1 -1 0 1
        output[o + 3] = Sign(i - 2);
        // 4 0 4 0
        output[o + 4] = Low(u);
        // 100 * (0 -2 0 -2) + (-1 -5 -7 -11)
        nint w = -v / 2 % 3;
        output[o + 5] = w * 100 + (~v | 1);
        // 4294967295 - (0 1 2 3)
        output[o + 6] = (long)(nuint)(uint.MaxValue - (uint)i);
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

        using var output = stream.Allocate1D<long>(28);
        stream.Launch(
            (Index1D)4,
            index => Kernels.NativeIntKernel(index, output.View));
        stream.Synchronize();

        var data = output.GetAsArray1D();
        for (int i = 0; i < 4; ++i)
            Console.WriteLine(string.Join(" ", data[(i * 7)..(i * 7 + 7)]));
    }
}
