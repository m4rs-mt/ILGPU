// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NullArrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NullArrays
// Every thread selects null or allocated arrays depending on its index and
// tests them for null: conditional null arrays, ??, ?.Length, null arguments of
// inlined and of NoInlining helpers, the unassigned array field of a default
// structure, empty arrays (which are not null) and reference equality. Every
// thread writes eight results.
// Expected output (per thread):
//   0: 1 3 3 -1 -1 1 0 -2
//   1: -1 5 -2 -1 -1 0 0 -2
//   2: 3 3 3 0 -1 11 11 -2
//   3: -1 5 -2 0 4 10 11 3
// A second kernel tests ReferenceEquals, null arrays returned from helpers,
// ?[], property patterns, arrays compared as objects, ??= on a field and on a
// ref parameter, and writes six results per thread:
//   0: 1 -1 1 3 31 0
//   1: 1 -1 1 3 31 0
//   2: 0 4 0 2 41 1
//   3: 0 4 0 2 41 1
// A third kernel tests Array.Empty, whose arrays are not null and are the same
// array for the same element type, also in a NoInlining helper, but not a new
// empty array or an empty array of another type, also not of uint (also in a
// NoInlining helper), an int enum or a structure of one int, whose IR type is
// the same as int. It writes 1111 for even threads, which select Array.Empty,
// and 1011 for odd threads, which select a new empty array.

using System;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    struct Holder
    {
        public int[] Values;
    }

    enum IntEnum
    {
        Zero,
    }

    struct IntBox
    {
        public int Value;
    }

    static int LengthOrMinusOne(int[] values) =>
        values == null ? -1 : values.Length;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int CountNotNull(int[] first, int[] second) =>
        (first != null ? 1 : 0) + (second is not null ? 10 : 0);

    public static void NullArrayKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int i = index.X;
        int[] a = (i & 1) == 0 ? new int[3] : null;
        if (a != null)
            a[i % 3] = i + 1;
        int[] b = a ?? new int[5];
        int[] empty = i > 1 ? new int[0] : null;
        Holder holder = default;
        if (i == 3)
            holder.Values = new int[4];
        int[] x = new int[2];
        int[] y = new int[2];
        int[] z = i > 1 ? x : y;

        int o = i * 8;
        output[o + 0] = a == null ? -1 : a[i % 3];
        output[o + 1] = b.Length;
        output[o + 2] = a?.Length ?? -2;
        output[o + 3] = LengthOrMinusOne(empty);
        output[o + 4] = holder.Values is null ? -1 : holder.Values.Length;
        output[o + 5] = CountNotNull(a, empty);
        output[o + 6] = (z == x ? 1 : 0) + (z != y ? 10 : 0);
        output[o + 7] = LengthOrMinusOne(null) + LengthOrMinusOne(holder.Values);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool IsEmptyArray(int[] values) => values == Array.Empty<int>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool IsEmptyUIntArray(int[] values) =>
        (object)values == Array.Empty<uint>();

    public static void EmptyArrayKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] a = Array.Empty<int>();
        int[] b = Array.Empty<int>();
        float[] f = Array.Empty<float>();
        int[] c = (index.X & 1) == 0 ? Array.Empty<int>() : new int[0];
        output[index] = a.Length * 100000 + (a is not null ? 1 : 0) +
            (a == b ? 10 : 0) + (c == a ? 100 : 0) +
            (IsEmptyArray(b) ? 1000 : 0) + (new int[0] == a ? 10000 : 0) +
            ((object)a == f ? 1000000 : 0) +
            ((object)a == Array.Empty<uint>() ? 10000000 : 0) +
            ((object)a == Array.Empty<IntEnum>() ? 10000000 : 0) +
            ((object)a == Array.Empty<IntBox>() ? 10000000 : 0) +
            (IsEmptyUIntArray(b) ? 10000000 : 0);
    }

    static int[] Make(int x) => x > 1 ? new int[4] : null;

    static void Ensure(ref int[] values)
    {
        values ??= new int[3];
        values[0] += 1;
    }

    public static void NullReferenceKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int i = index.X;
        int[] a = Make(i);
        float[] f = i > 2 ? new float[2] : null;
        Holder holder = default;
        if (i > 1)
            holder.Values = new int[2];
        holder.Values ??= new int[3];
        int[] r = i == 1 ? null : a;
        Ensure(ref r);

        int o = i * 6;
        output[o + 0] = ReferenceEquals(a, null) ? 1 : 0;
        output[o + 1] = a is { Length: > 2 } ? a.Length : a?[0] ?? -1;
        output[o + 2] = (object)a == f ? 1 : 0;
        output[o + 3] = holder.Values.Length;
        output[o + 4] = r.Length * 10 + r[0];
        output[o + 5] = r == a ? 1 : 0;
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

        using var output = stream.Allocate1D<int>(32);
        stream.Launch((Index1D)4, index => Kernels.NullArrayKernel(index, output.View));
        using var references = stream.Allocate1D<int>(24);
        stream.Launch(
            (Index1D)4,
            index => Kernels.NullReferenceKernel(index, references.View));
        using var empty = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.EmptyArrayKernel(index, empty.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in references.GetAsArray1D())
            Console.WriteLine(v);
        foreach (var v in empty.GetAsArray1D())
            Console.WriteLine(v);
    }
}
