// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LiteralKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Tests.Framework;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernels with scalar constants that only survive code generation if they are
/// emitted as exact, culture-invariant literals: negative integers and
/// integral doubles.
/// </summary>
static class LiteralKernels
{
    /// <summary>
    /// Writes negative integer constants and the minimum values, some of them
    /// combined with a parameter so they appear inside expressions. The CPU
    /// backend formats 8-bit and 16-bit constants separately (with a cast to
    /// byte or short).
    /// </summary>
    public static void NegativeIntegerConstantsKernel(
        Index1D index,
        ArrayView1D<sbyte, Stride1D.Dense> sbytes,
        ArrayView1D<short, Stride1D.Dense> shorts,
        ArrayView1D<int, Stride1D.Dense> ints,
        ArrayView1D<long, Stride1D.Dense> longs,
        int x)
    {
        sbytes[0] = -3;
        shorts[0] = -7;
        ints[0] = -5;
        ints[1] = x * -123;
        ints[2] = int.MinValue;
        longs[0] = -9876543210L;
        longs[1] = x * -9876543210L;
        longs[2] = long.MinValue;
    }

    /// <summary>
    /// Writes integral double constants, which C# reads as integers unless
    /// they keep a fraction: negative zero would become the integer zero.
    /// </summary>
    [RequiresCapability(BackendCapability.Float64)]
    public static void IntegralDoubleConstantsKernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> output,
        double x)
    {
        output[0] = -0.0;
        output[1] = 9007199254740992.0;
        output[2] = x / 3.0;
    }
}
