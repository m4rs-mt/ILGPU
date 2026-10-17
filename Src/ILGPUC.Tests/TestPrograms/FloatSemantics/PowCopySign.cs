// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PowCopySign.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PowCopySign
// MathF.Pow and MathF.CopySign on special values: negative bases, signed zeros,
// infinities and NaNs with either sign bit. Each lane computes pow(x, y),
// copysign(x, y), pow(b, y), copysign(x, e) and pow(x, p) for a uniform base
// b = -2, a uniform sign e = -3 and a uniform exponent p = 2; the last line
// holds pow(b, e) and copysign(b, e) of the uniform base and sign. Pow
// results are printed as values (NaN payloads differ between devices),
// copysign results as their bits. Pow results are rounded to six significant
// digits, since device pow functions may differ from .NET in the last bits.
// Expected output: see FloatSemanticsExecutionTests.PowCopySign_ProducesCorrectOutput.

using System;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void PowCopySignKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> x,
        ArrayView1D<float, Stride1D.Dense> y,
        ArrayView1D<float, Stride1D.Dense> output,
        float uniformBase,
        float uniformSign,
        float uniformExponent)
    {
        float a = x[index];
        float b = y[index];
        output[index * 5 + 0] = MathF.Pow(a, b);
        output[index * 5 + 1] = MathF.CopySign(a, b);
        output[index * 5 + 2] = MathF.Pow(uniformBase, b);
        output[index * 5 + 3] = MathF.CopySign(a, uniformSign);
        output[index * 5 + 4] = MathF.Pow(a, uniformExponent);
        if (index == 0)
        {
            int last = (int)x.Length * 5;
            output[last] = MathF.Pow(uniformBase, uniformSign);
            output[last + 1] = MathF.CopySign(uniformBase, uniformSign);
        }
    }
}

static class Program
{
    static string Value(float v) =>
        float.IsNaN(v) ? "nan"
        : float.IsPositiveInfinity(v) ? "inf"
        : float.IsNegativeInfinity(v) ? "-inf"
        : v.ToString("G6", CultureInfo.InvariantCulture);

    static string Bits(float v) =>
        BitConverter.SingleToInt32Bits(v).ToString("X8", CultureInfo.InvariantCulture);

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        float inf = float.PositiveInfinity;
        float nan = BitConverter.Int32BitsToSingle(0x7FC00000);
        float negNaN = BitConverter.Int32BitsToSingle(unchecked((int)0xFFC00000));
        float[] xs =
        [
            2, -2, -2, -2, -8, -0.0f, -0.0f, -inf, -inf, -1,
            nan, 3, -3, 3, inf, 2, -5, negNaN, 1, -1,
        ];
        float[] ys =
        [
            10, 3, 2, 0.5f, 1.0f / 3.0f, -1, -2, 3, -3, inf,
            0, -0.0f, 0, -inf, -1, negNaN, nan, 1, nan, -inf,
        ];

        using var x = stream.Allocate1D(xs);
        using var y = stream.Allocate1D(ys);
        using var output = stream.Allocate1D<float>(xs.Length * 5 + 2);
        stream.Launch(
            (Index1D)xs.Length,
            index => Kernels.PowCopySignKernel(
                index, x.View, y.View, output.View, -2.0f, -3.0f, 2.0f));
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
