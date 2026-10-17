// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PowCopySignDouble.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PowCopySignDouble
// Math.Pow and Math.CopySign on doubles: negative bases, signed zeros,
// infinities and NaNs with either sign bit. Each lane computes pow(x, y),
// copysign(x, y), pow(b, y), copysign(x, e) and pow(x, p) for a uniform base
// b = -2, a uniform sign e = -3 and a uniform exponent p = 2; the last line
// holds pow(b, e) and copysign(b, e) of the uniform base and sign. Pow
// results are printed as values (NaN payloads differ between devices),
// copysign results as their bits. Pow results are rounded to 15 significant
// digits, since device pow functions may differ from .NET in the last bits.
// Expected output: see FloatSemanticsExecutionTests.PowCopySignDouble_ProducesCorrectOutput.

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PowCopySignDoubleKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> x,
        ArrayView1D<double, Stride1D.Dense> y,
        ArrayView1D<double, Stride1D.Dense> output,
        double uniformBase,
        double uniformSign,
        double uniformExponent)
    {
        double a = x[index];
        double b = y[index];
        output[index * 5 + 0] = Math.Pow(a, b);
        output[index * 5 + 1] = Math.CopySign(a, b);
        output[index * 5 + 2] = Math.Pow(uniformBase, b);
        output[index * 5 + 3] = Math.CopySign(a, uniformSign);
        output[index * 5 + 4] = Math.Pow(a, uniformExponent);
        if (index == 0)
        {
            int last = (int)x.Length * 5;
            output[last] = Math.Pow(uniformBase, uniformSign);
            output[last + 1] = Math.CopySign(uniformBase, uniformSign);
        }
    }
}

static class Program
{
    static string Value(double v) =>
        double.IsNaN(v) ? "nan"
        : double.IsPositiveInfinity(v) ? "inf"
        : double.IsNegativeInfinity(v) ? "-inf"
        : v.ToString("G15", CultureInfo.InvariantCulture);

    static string Bits(double v) =>
        BitConverter.DoubleToInt64Bits(v).ToString("X16", CultureInfo.InvariantCulture);

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        double inf = double.PositiveInfinity;
        double nan = BitConverter.Int64BitsToDouble(0x7FF8000000000000);
        double negNaN = BitConverter.Int64BitsToDouble(
            unchecked((long)0xFFF8000000000000));
        double[] xs =
        [
            2, -2, -2, -2, -8, -0.0, -0.0, -inf, -inf, -1,
            nan, 3, -3, 3, inf, 2, -5, negNaN, 1, -1,
        ];
        double[] ys =
        [
            10, 3, 2, 0.5, 1.0 / 3.0, -1, -2, 3, -3, inf,
            0, -0.0, 0, -inf, -1, negNaN, nan, 1, nan, -inf,
        ];

        using var x = stream.Allocate1D(xs);
        using var y = stream.Allocate1D(ys);
        using var output = stream.Allocate1D<double>(xs.Length * 5 + 2);
        stream.Launch(
            (Index1D)xs.Length,
            index => Kernels.PowCopySignDoubleKernel(
                index, x.View, y.View, output.View, -2.0, -3.0, 2.0));
        stream.Synchronize();

        var values = output.GetAsArray1D();
        for (int i = 0; i <= xs.Length; i++)
        {
            if (i == xs.Length)
            {
                Console.WriteLine($"{Value(values[i * 5])} {Bits(values[i * 5 + 1])}");
                break;
            }
            Console.WriteLine(
                $"{Value(values[i * 5])} {Bits(values[i * 5 + 1])} " +
                $"{Value(values[i * 5 + 2])} {Bits(values[i * 5 + 3])} " +
                $"{Value(values[i * 5 + 4])}");
        }
    }
}
