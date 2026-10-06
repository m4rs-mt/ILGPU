// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypedPointers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: PointerArithmetic TypedPointers
// Kernels read bytes, elements of other types and padded structures through pointers,
// and move pointers by large and structure-sized byte offsets.
// Expected output: 40240 33250 32260 31270, 52 96 248 172, 15 94 35 134, 23 43 63 83, 10 40 3 40,
// 4294967296 17179869200 4294967304 4294967270, 2147483648 2147483648 2147483648 2147483647,
// 21474836481 21474836482 21474836473 21474836474,
// 2110 13331 4330 15551

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

struct Triple
{
    public int A;
    public int B;
    public int C;
}

struct Mixed
{
    public int X;
    public long Y;
}

struct TriplePair
{
    public Triple First;
    public Triple Second;
}

struct MixedPair
{
    public Mixed First;
    public Mixed Second;
}

struct Triple<T> where T : unmanaged
{
    public T A;
    public T B;
    public T C;
}

static unsafe class Kernels
{
    private static int Read(int* p, int k) => p[k];

    private static int* Next(int* p) => p + 1;

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

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.BytesKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.ElementTypesKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.StructElementsKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.HelperKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.NativeIntOffsetKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<long>(4))
        {
            stream.Launch((Index1D)4, index => Kernels.LargeOffsetsKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<long>(4))
        {
            stream.Launch((Index1D)4, index => Kernels.NegativeOffsetsKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<long>(4))
        {
            stream.Launch((Index1D)4, index => Kernels.NativeBytesKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
        using (var buffer = stream.Allocate1D<int>(4))
        {
            buffer.View.CopyFromCPU(stream, new int[] { 10, 20, 30, 40 });
            stream.Launch((Index1D)4, index => Kernels.StructOffsetsKernel(index, buffer.View));
            stream.Synchronize();
            foreach (var v in buffer.GetAsArray1D())
                Console.WriteLine(v);
        }
    }
}
