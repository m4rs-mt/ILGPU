// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericSinDouble.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: the type-specialized helper pattern from issue #1427 with double.
// Kernel2<double> is only launched through a generic wrapper, which also launches
// Kernel2<float>.
// Expected output: sin(0.5) and sin(1), then cos(0.5) and cos(1), with four decimals.

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

    // The type-specialized helper from issue #1427: typeof(T) selects the
    // arithmetic, and the throw is removed for float and double at compile time
    static T GenericSin<T>(T x) where T : unmanaged
    {
        if (typeof(T) == typeof(float))
            return Unsafe.BitCast<float, T>(MathF.Sin(Unsafe.BitCast<T, float>(x)));
        else if (typeof(T) == typeof(double))
            return Unsafe.BitCast<double, T>(Math.Cos(Unsafe.BitCast<T, double>(x)));
        else
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
        Run(stream, new double[] { 0.5, 1.0 });
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
