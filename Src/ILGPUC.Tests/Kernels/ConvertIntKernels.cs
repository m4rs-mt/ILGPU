// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConvertIntKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for integer conversion tests.
/// </summary>
static class ConvertIntKernels
{
    /// <summary>
    /// Truncate: long -> int.
    /// </summary>
    public static void TruncateKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        long value)
    {
        data[index] = (int)value;
    }

    /// <summary>
    /// Promote: int -> long, store low 32 bits back as int.
    /// </summary>
    public static void PromoteKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> data,
        int value)
    {
        long promoted = (long)value;
        data[index] = promoted;
    }

    /// <summary>
    /// Byte to int conversion.
    /// </summary>
    public static void ByteToIntKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        byte value)
    {
        data[index] = (int)value;
    }

    /// <summary>
    /// Int to byte truncation.
    /// </summary>
    public static void IntToByteKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        int value)
    {
        byte truncated = (byte)value;
        data[index] = (int)truncated;
    }

    /// <summary>
    /// Sign extension: sbyte -> int.
    /// </summary>
    public static void SignExtendKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        sbyte value)
    {
        data[index] = (int)value;
    }
}
