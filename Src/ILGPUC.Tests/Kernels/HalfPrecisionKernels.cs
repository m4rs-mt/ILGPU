// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfPrecisionKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// System.Half constants, comparisons, conversions, bit casts and math functions.
/// </summary>
static class HalfPrecisionKernels
{
    public static void HalfConstantsKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> data,
        ArrayView1D<ushort, Stride1D.Dense> result)
    {
        var x = data[index];
        result[index] = BitConverter.HalfToUInt16Bits(
            x * Half.Pi + Half.One - Half.Epsilon + XMath.Rcp(x));
        data[index] = Half.Clamp(x, Half.MinValue, Half.MaxValue);
    }

    public static void HalfCompareKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> a,
        ArrayView1D<Half, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> result)
    {
        var x = a[index];
        var y = b[index];
        result[index] =
            (x < y ? 1 : 0) | (x <= y ? 2 : 0) | (x > y ? 4 : 0) |
            (x >= y ? 8 : 0) | (x == y ? 16 : 0) | (x != y ? 32 : 0) |
            (Half.IsInfinity(x) ? 64 : 0) | (Half.IsNegative(x) ? 128 : 0) |
            (Half.IsSubnormal(x) ? 256 : 0) | (Half.IsInteger(x) ? 512 : 0);
    }

    public static void HalfConvertKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> data,
        ArrayView1D<float, Stride1D.Dense> floats,
        ArrayView1D<int, Stride1D.Dense> ints)
    {
        var x = data[index];
        floats[index] = (float)x;
        ints[index] = (int)x + (short)x + (byte)x + (sbyte)x;
        data[index] = (Half)floats[index] + (Half)ints[index] + (Half)(long)index.X;
    }

    public static void HalfBitCastKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> data,
        ArrayView1D<short, Stride1D.Dense> bits)
    {
        bits[index] = BitConverter.HalfToInt16Bits(data[index]);
        data[index] = BitConverter.Int16BitsToHalf((short)(bits[index] ^ 0x8000));
    }

    public static void HalfMathKernel(
        Index1D index,
        ArrayView1D<Half, Stride1D.Dense> a,
        ArrayView1D<Half, Stride1D.Dense> b,
        ArrayView1D<Half, Stride1D.Dense> result)
    {
        var x = a[index];
        var y = b[index];
        result[index] =
            Half.Sin(x) + Half.Cos(y) + Half.Tan(x) + Half.Atan2(x, y) +
            Half.Exp(x) + Half.Log(y) + Half.Pow(x, y) + Half.Sqrt(y) +
            Half.Floor(x) + Half.Ceiling(y) + Half.Round(x) + Half.Truncate(y) +
            Half.Abs(x) + Half.Min(x, y) + Half.Max(x, y) + Half.CopySign(x, y) +
            Half.FusedMultiplyAdd(x, y, x) + Half.Ieee754Remainder(x, y) + x % y +
            Half.Tanh(x) + Half.Cbrt(y) + Half.Log2(y) + Half.Exp2(x) +
            XMath.Rsqrt(y) + XMath.Rcp(x);
    }
}
