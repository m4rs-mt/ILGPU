// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BinaryIntOpKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda.Libraries;
using System.Numerics;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Generic binary operations on integer types.
/// </summary>
static class BinaryIntOpKernels
{
    public static void AddKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, INumber<T>
    {
        result[index] = a[index] + b[index];
    }

    public static void SubKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, INumber<T>
    {
        result[index] = a[index] - b[index];
    }

    public static void MulKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, INumber<T>
    {
        result[index] = a[index] * b[index];
    }

    public static void DivKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, INumber<T>
    {
        result[index] = a[index] / b[index];
    }

    public static void AndKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, INumber<T>, IBitwiseOperators<T, T, T>
    {
        result[index] = a[index] & b[index];
    }

    public static void OrKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, INumber<T>, IBitwiseOperators<T, T, T>
    {
        result[index] = a[index] | b[index];
    }

    public static void XorKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> a,
        ArrayView1D<T, Stride1D.Dense> b,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, INumber<T>, IBitwiseOperators<T, T, T>
    {
        result[index] = a[index] ^ b[index];
    }

    public static void UnsignedMinMaxKernel(
        Index1D index,
        ArrayView1D<uint, Stride1D.Dense> u32,
        ArrayView1D<ulong, Stride1D.Dense> u64,
        ArrayView1D<ulong, Stride1D.Dense> result)
    {
        result[index * 4 + 0] = System.Math.Min(u32[index], u32[index + 1]);
        result[index * 4 + 1] = System.Math.Max(u32[index], u32[index + 1]);
        result[index * 4 + 2] = System.Math.Min(u64[index], u64[index + 1]);
        result[index * 4 + 3] = System.Math.Max(u64[index], u64[index + 1]);
    }
}
