// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: IEEERemainder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: IEEERemainder
// MathF.IEEERemainder matches .NET bit for bit on large quotients, on quotients
// close to halfway between two integers, on halfway quotients, which round to
// the even integer, and on signed zeros, infinities, NaNs and divisors whose
// double overflows. Each lane divides by its own and by a uniform divisor.
// NaN payloads differ between devices and are compared as NaN; no remainder is
// subnormal, as devices may flush subnormal results to zero. Mismatches are
// printed before the number of mismatches of each group.
// Expected output: large quotients: mismatches 0, near halfway: mismatches 0,
// halfway: mismatches 0, special values: mismatches 0

using System;
using System.Collections.Generic;
using System.Globalization;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void IEEERemainderKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> x,
        ArrayView1D<float, Stride1D.Dense> y,
        ArrayView1D<float, Stride1D.Dense> output,
        float uniformDivisor)
    {
        output[index * 2] = MathF.IEEERemainder(x[index], y[index]);
        output[index * 2 + 1] = MathF.IEEERemainder(x[index], uniformDivisor);
    }
}

static class Program
{
    const float UniformDivisor = 0.1f;

    static readonly Random Random = new(1234);

    static float Bits(int bits) => BitConverter.Int32BitsToSingle(bits);

    static string Text(float value) =>
        BitConverter.SingleToInt32Bits(value).ToString("X8", CultureInfo.InvariantCulture);

    // Returns a random float with a random sign and an exponent in the given range
    static float NextFloat(int minExponent, int maxExponent)
    {
        int exponent = Random.Next(minExponent, maxExponent + 1) + 127;
        int mantissa = Random.Next(0, 1 << 23);
        int sign = Random.Next(2) << 31;
        return Bits(sign | exponent << 23 | mantissa);
    }

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        var groups = new List<(string Name, float[] X, float[] Y)>();

        // Large quotients, whose products with the divisor and differences to
        // the dividend round
        var x = new List<float> { 5.149796E+34f, -126836420f, 1.7803285E+14f, 6.1454934E+30f, -4.088099E+35f, float.MaxValue, 1e30f };
        var y = new List<float> { 0.0014541488f, 5.8988157E-05f, -1.5067422f, 0.99280083f, 0.000158357f, 3f, 3f };
        for (int i = 0; i < 1000; ++i)
        {
            x.Add(NextFloat(24, 127));
            y.Add(NextFloat(-20, 5));
        }
        groups.Add(("large quotients", x.ToArray(), y.ToArray()));

        // Quotients a few units in the last place away from halfway cases
        x = [456.8978f, -337.61914f, 7401965.5f, -262813740f, -10728477f];
        y = [0.0014488202f, -0.0010238283f, 10.597452f, -334.04626f, -20.920816f];
        for (int i = 0; i < 1000; ++i)
        {
            float divisor = NextFloat(-8, 8);
            float halfway = (float)((Random.Next(0, 1 << 20) + 0.5) * divisor);
            x.Add(Bits(BitConverter.SingleToInt32Bits(halfway) + Random.Next(-2, 3)));
            y.Add(divisor);
        }
        groups.Add(("near halfway", x.ToArray(), y.ToArray()));

        // Exact halfway cases with even and odd truncated quotients: divisors
        // with eight significant bits keep the dividends exact
        x = [2.5f, 3.5f, -2.5f, -3.5f, 5f, 7f, -7f, 0.75f, 6f, 16777215f, 8388609f];
        y = [1f, 1f, 1f, 1f, 2f, 2f, -2f, 0.5f, 4f, 2f, -2f];
        for (int i = 0; i < 1000; ++i)
        {
            float divisor = Bits(BitConverter.SingleToInt32Bits(NextFloat(-30, 30)) & ~0xFFFF);
            x.Add((float)((Random.Next(0, 1 << 15) + 0.5) * divisor));
            y.Add(divisor);
        }
        groups.Add(("halfway", x.ToArray(), y.ToArray()));

        // Signed zeros, infinities, NaNs and divisors whose double overflows
        float inf = float.PositiveInfinity;
        float max = float.MaxValue;
        groups.Add((
            "special values",
            [0f, -0f, 4f, -4f, 1f, inf, -inf, 1f, -1f, float.NaN, 1f, max, -max, max],
            [1f, 1f, 2f, 2f, 0f, 1f, 2f, inf, -inf, 1f, float.NaN, max * 0.75f, max * 0.6f, -max]));

        foreach (var (name, xs, ys) in groups)
        {
            using var xBuffer = stream.Allocate1D(xs);
            using var yBuffer = stream.Allocate1D(ys);
            using var output = stream.Allocate1D<float>(xs.Length * 2);
            stream.Launch(
                (Index1D)xs.Length,
                index => Kernels.IEEERemainderKernel(
                    index, xBuffer.View, yBuffer.View, output.View, UniformDivisor));
            stream.Synchronize();

            var values = output.GetAsArray1D();
            int mismatches = 0;
            for (int i = 0; i < xs.Length; ++i)
            {
                float[] divisors = [ys[i], UniformDivisor];
                for (int j = 0; j < divisors.Length; ++j)
                {
                    float expected = MathF.IEEERemainder(xs[i], divisors[j]);
                    float actual = values[i * 2 + j];
                    bool equal = float.IsNaN(expected)
                        ? float.IsNaN(actual)
                        : BitConverter.SingleToInt32Bits(expected) ==
                            BitConverter.SingleToInt32Bits(actual);
                    if (equal)
                        continue;
                    Console.WriteLine(
                        $"{Text(xs[i])} {Text(divisors[j])}: expected {Text(expected)} " +
                        $"but got {Text(actual)}");
                    ++mismatches;
                }
            }
            Console.WriteLine($"{name}: mismatches {mismatches}");
        }
    }
}
