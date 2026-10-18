// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicUnsignedFloat.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: AtomicUnsignedFloat
// Atomics on uint (values with the top bit set) and float, from 4 threads:
// uint Add, Max, Min, And, Or, Xor, Exchange, CompareExchange and float Add,
// Max, Min, Exchange, CompareExchange, and the previous value of a float Max.
// Expected output: 10 2147483651 2147483648 4294967280 4026531840 15 3735928559 2415919104 5 2.5 -2 7.25 3.5 4 4.5

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void AtomicUnsignedFloatKernel(
        Index1D index,
        ArrayView1D<uint, Stride1D.Dense> u,
        ArrayView1D<float, Stride1D.Dense> f)
    {
        uint i = (uint)index.X;
        Atomic.Add(ref u[0], i + 1u);
        Atomic.Max(ref u[1], 0x8000_0000u + i);
        Atomic.Min(ref u[2], 0x8000_0000u + i);
        Atomic.And(ref u[3], ~(1u << index.X));
        Atomic.Or(ref u[4], 1u << (index.X + 28));
        Atomic.Xor(ref u[5], 1u << index.X);

        float value = index.X * 1.5f - 2.0f;
        Atomic.Add(ref f[0], 0.5f * (index.X + 1));
        Atomic.Max(ref f[1], value);
        Atomic.Min(ref f[2], value);

        if (index.X == 0)
        {
            Atomic.Exchange(ref u[6], 0xDEAD_BEEFu);
            uint old = Atomic.CompareExchange(ref u[7], 0u, 0x9000_0000u);
            Atomic.CompareExchange(ref u[7], old + 5u, 1u);
            Atomic.Exchange(ref f[3], 7.25f);
            float oldF = Atomic.CompareExchange(ref f[4], 0.0f, 3.5f);
            Atomic.CompareExchange(ref f[4], oldF + 1.0f, 9.0f);
            float previous = Atomic.Max(ref f[5], 1.0f);
            f[6] = previous + 0.5f;
        }
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

        using var u = stream.Allocate1D(new uint[] { 0, 0, 0xFFFF_FFFFu, 0xFFFF_FFFFu, 0, 0, 0, 0 });
        using var f = stream.Allocate1D(new float[] { 0.0f, -100.0f, 100.0f, 0.0f, 0.0f, 4.0f, 0.0f });
        stream.Launch((Index1D)4, index => Kernels.AtomicUnsignedFloatKernel(index, u.View, f.View));
        stream.Synchronize();

        foreach (var v in u.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in f.GetAsArray1D())
            Console.WriteLine(v);
    }
}
