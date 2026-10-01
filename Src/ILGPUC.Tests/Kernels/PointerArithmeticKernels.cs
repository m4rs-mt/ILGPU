// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerArithmeticKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Pointer arithmetic on the addresses of locals and view elements.
/// </summary>
static unsafe class PointerArithmeticKernels
{
    /// <summary>
    /// Three adjacent integers.
    /// </summary>
    public struct Triple
    {
        public int A;
        public int B;
        public int C;
    }

    /// <summary>
    /// Offsets of pointers to a local structure, including pointers converted
    /// from the address of the structure, whose element type IL does not keep.
    /// </summary>
    public static void LocalOffsetsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = data[index];
        t.B = 2 * data[index];
        t.C = 3;
        int* p = &t.A;
        data[index] = ((int*)&t)[1] + *((int*)&t + 2) + p[1] + *(p + 2) +
            *(&t.A + 2);
    }

    /// <summary>
    /// Addresses moved by bytes and selected by conditions with other
    /// addresses: untyped addresses, the address of a structure cast to an
    /// int or byte pointer and moved past its first field, and an int pointer
    /// moved by one byte.
    /// </summary>
    public static void SelectOffsetKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = data[index];
        t.B = 2;
        t.C = 3;
        int x = 0x0400;
        int* p = &x;
        int* r = &t.C;
        bool even = (index.X & 1) == 0;
        nint a = even ? (nint)(&t.A) + 4 : (nint)(&t.C);
        int* q = even ? (int*)&t + 1 : &t.C;
        byte* b = even ? (byte*)&t + 4 : (byte*)&t.C;
        byte* c = even ? (byte*)p + 1 : (byte*)r;
        data[index] = *(int*)a + 10 * *q + 100 * *(int*)b + 1000 * *c;
    }

    /// <summary>
    /// Bytes of an integer and of a structure read through byte pointers,
    /// including byte offsets of an int pointer.
    /// </summary>
    public static void BytesKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int x = data[index] + 0x01020300;
        byte* b = (byte*)&x;
        Triple t;
        t.A = 0x0100;
        t.B = 2;
        t.C = 3;
        int* p = &t.A;
        data[index] = b[0] + 10 * b[1] + 100 * *(b + 2) + 1000 * b[index.X & 3] +
            10000 * (*((byte*)p + 1) + *(int*)((byte*)p + 4));
    }
}
