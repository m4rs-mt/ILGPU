// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ReinterpretCastKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Reinterpret cast kernels using Unsafe.As.
/// </summary>
static class ReinterpretCastKernels
{
    public static void FloatToUInt32Kernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> input,
        ArrayView1D<uint, Stride1D.Dense> output)
    {
        float val = input[index];
        output[index] = Unsafe.As<float, uint>(ref val);
    }

    public static void UInt32ToFloatKernel(
        Index1D index,
        ArrayView1D<uint, Stride1D.Dense> input,
        ArrayView1D<float, Stride1D.Dense> output)
    {
        uint val = input[index];
        output[index] = Unsafe.As<uint, float>(ref val);
    }

    public static void DoubleToUInt64Kernel(
        Index1D index,
        ArrayView1D<double, Stride1D.Dense> input,
        ArrayView1D<ulong, Stride1D.Dense> output)
    {
        double val = input[index];
        output[index] = Unsafe.As<double, ulong>(ref val);
    }

    public static void UInt64ToDoubleKernel(
        Index1D index,
        ArrayView1D<ulong, Stride1D.Dense> input,
        ArrayView1D<double, Stride1D.Dense> output)
    {
        ulong val = input[index];
        output[index] = Unsafe.As<ulong, double>(ref val);
    }
}
