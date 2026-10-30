// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ReturnedReferences.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// disable: max_line_length
// Test program: ReturnedReferences
// Helpers return references and pointers to memory that outlives them: a parameter
// passed by reference (also from a helper that is not inlined), a field of a structure
// passed by reference, a field of the structure an instance method is called on, a
// pointer passed in and moved to the next field, an element of a view and an element of
// shared memory. The kernel writes through the returned references.
// Expected output: 1118 1121 1124 1127

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using ILGPU;
using ILGPU.Runtime;

struct Pair
{
    public int First;
    public int Second;

    [UnscopedRef]
    public ref int GetFirst() => ref First;
}

static class Kernels
{
    static ref int Same(ref int value) => ref value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static ref int SameNotInlined(ref int value) => ref value;

    static ref int GetSecond(ref Pair pair) => ref pair.Second;

    static ref int Element(ArrayView1D<int, Stride1D.Dense> view, Index1D index) =>
        ref view[index];

    static unsafe int* Next(int* pointer) => pointer + 1;

    static ref int SharedSlot() => ref Group.GetSharedMemory<int>(32)[Group.Index];

    public static unsafe void ReturnedReferencesKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> data,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        int local = data[index];
        Same(ref local) += 1;
        var pair = new Pair { First = local, Second = 2 * local };
        GetSecond(ref pair) += 10;
        pair.GetFirst() += 100;
        *Next(&pair.First) += 1000;
        SharedSlot() = pair.First + pair.Second;
        Element(output, index) = SharedSlot();
        SameNotInlined(ref Element(output, index)) += 2;
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

        using var data = stream.Allocate1D(new int[] { 1, 2, 3, 4 });
        using var output = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.ReturnedReferencesKernel(index, data.View, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
