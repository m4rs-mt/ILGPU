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
using System.Runtime.CompilerServices;

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
    /// An integer padded to the alignment of the long that follows it.
    /// </summary>
    public struct Mixed
    {
        public int X;
        public long Y;
    }

    /// <summary>
    /// Two padded structures.
    /// </summary>
    public struct MixedPair
    {
        public Mixed First;
        public Mixed Second;
    }

    /// <summary>
    /// Two structures of 12 bytes.
    /// </summary>
    public struct TriplePair
    {
        public Triple First;
        public Triple Second;
    }

    /// <summary>
    /// Three adjacent elements of the given type.
    /// </summary>
    public struct Triple<T> where T : unmanaged
    {
        public T A;
        public T B;
        public T C;
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
    /// Elements at int, long, small uint and negative indices.
    /// </summary>
    public static void IndexKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = data[index];
        t.B = 2 * data[index];
        t.C = 3;
        int* p = &t.A;
        int* q = &t.C;
        int k = index.X % 3;
        long l = 2 - k;
        uint u = (uint)k;
        data[index] = p[k] + p[l] + p[u] + q[-k];
    }

    /// <summary>
    /// Pointers moved by increments, decrements and compound assignments.
    /// </summary>
    public static void MoveKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = data[index];
        t.B = 2 * data[index];
        t.C = 3;
        int* p = &t.A;
        int sum = 0;
        for (int k = 0; k < 3; ++k)
            sum += *p++;
        p -= 2;
        sum = 10 * sum + *p--;
        p += index.X & 1;
        data[index] = 10 * sum + *p;
    }

    /// <summary>
    /// An int pointer moved backward by native integers and by a long, such as
    /// p - (nint)2.
    /// </summary>
    public static void IntBackKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = 1;
        t.B = 2;
        t.C = 3;
        int* p = &t.C;
        nint k = index.X % 3;
        long l = (index.X + 1) % 3;
        data[index] = *(p - k) + 10 * *(p - l) + 100 * *(p - (nint)2);
    }

    /// <summary>
    /// Differences of int, byte and padded structure pointers.
    /// </summary>
    public static void DifferenceKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t = default;
        MixedPair pair = default;
        long elements = &t.C - &t.A;
        long bytes = (byte*)&t.C - (byte*)&t.A;
        long structures = &pair.Second - &pair.First;
        data[index] += (int)elements + 10 * (int)bytes + 100 * (int)structures;
    }

    /// <summary>
    /// Addresses moved by bytes and selected by conditions with other
    /// addresses: untyped addresses, the address of a structure cast to an
    /// int or byte pointer and moved past its first field, an int pointer
    /// moved by one byte, and an untyped address moved backward by bytes.
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
        nint s = even ? (nint)(&t.B) - 4 : (nint)(&t.C);
        data[index] = *(int*)a + 10 * *q + 100 * *(int*)b + 1000 * *c +
            10000 * *(int*)s;
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

    /// <summary>
    /// Elements of byte, short, long and float pointers.
    /// </summary>
    public static void ElementTypesKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        int d = data[index];
        int k = index.X % 3;
        Triple<byte> b;
        b.A = 1;
        b.B = 2;
        b.C = (byte)d;
        Triple<short> s;
        s.A = 1;
        s.B = 2;
        s.C = (short)d;
        Triple<long> l;
        l.A = 1;
        l.B = 2;
        l.C = d;
        Triple<float> f;
        f.A = 1;
        f.B = 2;
        f.C = d;
        byte* pb = &b.A;
        short* ps = &s.A;
        long* pl = &l.A;
        float* pf = &f.A;
        data[index] =
            pb[2] + *(pb + 1) + pb[k] +
            ps[2] + *(ps + 1) + ps[k] +
            (int)(pl[2] + *(pl + 1) + pl[k]) +
            (int)(pf[2] + *(pf + 1) + pf[k]);
    }

    /// <summary>
    /// Elements of pointers to padded structures and to structures of 12 bytes.
    /// </summary>
    public static void StructElementsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        MixedPair pair;
        pair.First.X = 1;
        pair.First.Y = 2;
        pair.Second.X = data[index];
        pair.Second.Y = 4;
        Mixed* p = &pair.First;
        TriplePair triples = default;
        triples.Second.B = 5;
        Triple* q = &triples.First;
        data[index] = p[1].X + (int)(p + 1)->Y + p[index.X & 1].X +
            10 * q[index.X & 1].B;
    }

    private static int Read(int* p, int k) => p[k];

    private static int* Next(int* p) => p + 1;

    /// <summary>
    /// Pointers indexed and moved by helpers.
    /// </summary>
    public static void HelperKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = data[index];
        t.B = 2 * data[index];
        t.C = 3;
        int* p = &t.A;
        data[index] = Read(p, 2) + *Next(p);
    }

    /// <summary>
    /// A byte offset held in a native integer.
    /// </summary>
    public static void NativeIntOffsetKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        Triple t;
        t.A = data[index];
        t.B = 2 * data[index];
        t.C = 3;
        nint offset = (nint)(index.X % 3) * sizeof(int);
        data[index] = *(int*)((byte*)&t + offset);
    }

    /// <summary>
    /// Pointers to view elements moved by byte offsets beyond 4 GB that native
    /// integers scaled by constants compute, which overflow on 32 bits. The
    /// pointers are not read, and pointers to locals may have 32 bits.
    /// </summary>
    public static void LargeOffsetsKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> data)
    {
        byte* b = (byte*)Unsafe.AsPointer(ref data[index]);
        long* l = (long*)Unsafe.AsPointer(ref data[index]);
        int k = index.X;
        int x = 0x40000000 + k;
        int y = 0x7FFFFFF0 + k;
        data[index] = k switch
        {
            0 => b + (nint)x * 4 - b,
            1 => (byte*)((nint)l + (nint)x * 16) - b,
            2 => b + ((nint)x << 2) - b,
            _ => b + ((nint)y + (nint)y) - b,
        };
    }

    /// <summary>
    /// Pointers to view elements moved back by the byte offset int.MinValue,
    /// whose negation overflows on 32 bits. The pointers are not read.
    /// </summary>
    public static void NegativeOffsetsKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> data)
    {
        byte* b = (byte*)Unsafe.AsPointer(ref data[index]);
        int k = index.X;
        int m = int.MinValue + (k >> 1);
        data[index] = (k & 1) == 0 ? b - int.MinValue - b : b - m - b;
    }

    /// <summary>
    /// Addresses moved by native integers made from integers: an int pointer
    /// moved by a byte offset that is no multiple of the size of int, and a
    /// byte pointer moved back by an offset of up to int.MinValue, whose
    /// negation overflows on 32 bits. The moved byte pointer is not read.
    /// </summary>
    public static void NativeBytesKernel(
        Index1D index,
        ArrayView1D<long, Stride1D.Dense> data)
    {
        int x = 0x04030201;
        int* p = &x;
        nint k = index.X & 3;
        byte* b = (byte*)Unsafe.AsPointer(ref data[index]);
        int m = int.MinValue + (index.X >> 1);
        data[index] = *(byte*)((nint)p + k) +
            10 * ((nint)b - (nint)m - (nint)b);
    }

    /// <summary>
    /// Differences of pointers to structures of 12 bytes, and pointers to such
    /// structures moved by byte offsets that are multiples of 12.
    /// </summary>
    public static void StructOffsetsKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data)
    {
        TriplePair triples = default;
        triples.First.B = data[index];
        triples.Second.B = data[index] + 1;
        Triple* q = &triples.First;
        int k = index.X & 1;
        Triple* r = (Triple*)((byte*)q + (nint)k * 12);
        Triple* s = (Triple*)((nint)q + k * 12);
        data[index] = r->B + 10 * s->B + 100 * ((Triple*)&triples)[k].B +
            1000 * (int)(&triples.Second - q) + 10000 * (int)(r - q);
    }
}
