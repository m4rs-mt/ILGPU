// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NullArrayStackValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: NullArrayStackValues
// Null arrays that stay on the IL stack across branches, as in Release builds:
// ?? of two conditional null arrays (the first one stays on the stack while the
// second one is selected), null locals compared with each other, conditional
// null arrays in loop bodies, the length of null arrays, which is 0 in
// kernels, also of a null local (ldnull; ldlen), and a null local that a loop
// replaces with a new array. Every thread writes ten results.
// Expected output (per thread):
//   0: 1 1 1 1 575 333 0 1 0 902
//   1: -1 -1 -1 11 757 133 0 0 0 903
//   2: 1 2 1 11 575 113 0 2 0 904
//   3: 1 2 1 11 757 111 0 2 0 905

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    static int Coalesce(int i)
    {
        int[] a = i > 1 ? new int[2] : null;
        int[] b = i < 1 ? new int[1] : null;
        return (a ?? b) == null ? -1 : 1;
    }

    static int CoalesceLength(int i)
    {
        int[] a = i > 1 ? new int[2] : null;
        int[] b = i < 1 ? new int[1] : null;
        int[] c = a ?? b;
        return c == null ? -1 : c.Length;
    }

    static int CoalesceSelected(int i)
    {
        int[] x = new int[3];
        int[] a = i > 1 ? x : null;
        int[] b = i < 1 ? new int[1] : null;
        return (a ?? b) == null ? -1 : 1;
    }

    static int CompareNullLocals(int i)
    {
        int[] a = null;
        int[] b = null;
        int[] c = new int[1];
        int[] d = i > 0 ? c : null;
        return (a == b ? 1 : 0) + (d == c ? 10 : 0);
    }

    static int SelectInLoop(int i)
    {
        int r = 0;
        int[] x = new int[5];
        for (int k = 0; k < 3; ++k)
        {
            int[] a = ((k + i) & 1) == 0 ? x : null;
            r = r * 10 + (a?.Length ?? 7);
        }
        return r;
    }

    static int CoalesceInLoop(int i)
    {
        int r = 0;
        for (int k = 0; k < 3; ++k)
        {
            int[] a = k == i ? new int[4] : null;
            int[] b = k > i ? new int[2] : null;
            int t = (a ?? b) == null ? 1 : 3;
            r = r * 10 + t;
        }
        return r;
    }

    // The length of a null array is 0 in kernels
    static int NullLength(int i)
    {
        int[] a = i > 9 ? new int[3] : null;
        return a.Length;
    }

    // A null local of a Release build is ldnull; ldlen
    static int NullLocalLength()
    {
        int[] a = null;
        return a.Length;
    }

    static int CoalesceLengthOfNull(int i)
    {
        int[] a = i > 1 ? new int[2] : null;
        int[] b = i < 1 ? new int[1] : null;
        int[] c = a ?? b;
        return c.Length;
    }

    // A null local that the first loop iteration replaces with a new array
    static int AssignInLoop(int i)
    {
        int[] a = null;
        int s = 0;
        for (int k = 0; k < 3; ++k)
        {
            if (a == null)
                a = new int[3];
            a[k] = k + i;
            s += a.Length;
        }
        return s * 100 + a[2];
    }

    public static void NullArrayStackKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int i = index.X;
        int o = i * 10;
        output[o + 0] = Coalesce(i);
        output[o + 1] = CoalesceLength(i);
        output[o + 2] = CoalesceSelected(i);
        output[o + 3] = CompareNullLocals(i);
        output[o + 4] = SelectInLoop(i);
        output[o + 5] = CoalesceInLoop(i);
        output[o + 6] = NullLength(i);
        output[o + 7] = CoalesceLengthOfNull(i);
        output[o + 8] = NullLocalLength();
        output[o + 9] = AssignInLoop(i);
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

        using var output = stream.Allocate1D<int>(40);
        stream.Launch(
            (Index1D)4,
            index => Kernels.NullArrayStackKernel(index, output.View));
        stream.Synchronize();

        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
