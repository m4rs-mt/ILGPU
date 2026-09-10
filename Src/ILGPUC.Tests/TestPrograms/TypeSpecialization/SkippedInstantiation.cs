// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SkippedInstantiation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: a generic wrapper launches Kernel2<T> with float and with int.
// GenericSin<int> throws, so the int instantiation found through the wrapper is
// skipped at build time, and launching it throws a KernelNotCompiledException (a
// NotSupportedException) at run time, as the same code would throw on the CPU. The
// float instantiation still works.
// Expected output: 0.4794 0.8415 KernelNotCompiledException

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Kernel2<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> x,
        ArrayView1D<T, Stride1D.Dense> ans)
        where T : unmanaged
    {
        ans[index] = GenericSin(x[index]);
    }

    static T GenericSin<T>(T x) where T : unmanaged
    {
        if (typeof(T) == typeof(float))
            return Unsafe.BitCast<float, T>(MathF.Sin(Unsafe.BitCast<T, float>(x)));
        throw new NotSupportedException();
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

        Run(stream, new float[] { 0.5f, 1.0f });
        try
        {
            Run(stream, new int[] { 1, 2 });
        }
        catch (NotSupportedException e)
        {
            Console.WriteLine(e.GetType().Name);
        }
    }

    static void Run<T>(AcceleratorStream stream, T[] values) where T : unmanaged
    {
        using var x = stream.Allocate1D(values);
        using var ans = stream.Allocate1D<T>(values.Length);
        var xView = x.View;
        var ansView = ans.View;
        stream.Launch((Index1D)values.Length, i => Kernels.Kernel2(i, xView, ansView));
        stream.Synchronize();
        foreach (var value in ans.GetAsArray1D())
        {
            Console.WriteLine(
                Convert.ToDouble(value, CultureInfo.InvariantCulture)
                    .ToString("F4", CultureInfo.InvariantCulture));
        }
    }
}
