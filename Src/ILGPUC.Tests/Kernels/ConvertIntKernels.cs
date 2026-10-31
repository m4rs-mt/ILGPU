// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConvertIntKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System.Runtime.CompilerServices;

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

    /// <summary>
    /// Two adjacent fields whose addresses are 4 bytes apart.
    /// </summary>
    public struct FieldPair
    {
        public int A;
        public int B;
    }

    /// <summary>
    /// Unary operations on native integers, an address aligned with a negated
    /// mask and an untyped address moved by bytes.
    /// </summary>
    public static unsafe void NativeIntegerUnaryKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        FieldPair pair;
        pair.A = 0;
        pair.B = 0;
        nint n = data[index];
        nint negated = -n;
        nint inverted = ~n;
        int* element = (int*)Unsafe.AsPointer(ref data[index]);
        int* aligned = (int*)((nint)element & ~(nint)3);
        nint address = (nint)(&pair.A);
        address += 4;
        *(int*)address = 1000;
        *aligned = (int)(negated * 100 + inverted) + pair.B;
    }

    /// <summary>
    /// Differences of typed pointers in elements and bytes.
    /// </summary>
    public static unsafe void PointerDifferencesKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int* first = (int*)Unsafe.AsPointer(ref data[0]);
        int* last = (int*)Unsafe.AsPointer(ref data[3]);
        int* element = (int*)Unsafe.AsPointer(ref data[index]);
        long forward = element - first;
        long backward = element - last;
        long bytes = (byte*)element - (byte*)first;
        data[index] += (int)(forward * 1000 + backward * 100 + bytes);
    }
}
