// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: UnaryIntOpKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Unary operations on integer types.
/// </summary>
static class UnaryIntOpKernels
{
    public static void NegKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> input,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, IUnaryNegationOperators<T, T>
    {
        result[index] = -input[index];
    }

    public static void NotKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> input,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, IBitwiseOperators<T, T, T>
    {
        result[index] = ~input[index];
    }

    public static void AbsKernel<T>(
        Index1D index,
        ArrayView1D<T, Stride1D.Dense> input,
        ArrayView1D<T, Stride1D.Dense> result)
        where T : unmanaged, INumber<T>
    {
        result[index] = T.Abs(input[index]);
    }
}
