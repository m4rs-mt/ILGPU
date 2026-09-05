// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ForwardedDelegateParameter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: ForwardedDelegateParameter
// The delegate parameter captured by the launch is forwarded through a chain
// of host methods (Outer -> Middle -> UnaryOp), and every method of the chain
// is also called directly. Middle and Outer call each other recursively,
// which must not trace the chain forever.
// Expected output: 6 7 8 9 / 60 70 80 90 / 59 69 79 89

using System;
using ILGPU;
using ILGPU.Runtime;

static class Host
{
    public static void UnaryOp(
        AcceleratorStream stream, ArrayView1D<int, Stride1D.Dense> view, Func<int, int> op)
    {
        stream.Launch(view.IntExtent, index => view[index] = op(view[index]));
    }

    public static void Middle(
        AcceleratorStream stream, ArrayView1D<int, Stride1D.Dense> view, Func<int, int> op, int depth)
    {
        if (depth > 0)
            Outer(stream, view, op, depth - 1);
        else
            UnaryOp(stream, view, op);
    }

    public static void Outer(
        AcceleratorStream stream, ArrayView1D<int, Stride1D.Dense> view, Func<int, int> op, int depth) =>
        Middle(stream, view, op, depth);
}

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var buffer = stream.Allocate1D<int>(new[] { 1, 2, 3, 4 });

        Host.Outer(stream, buffer.View, v => v + 5, 1);
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));

        Host.Middle(stream, buffer.View, v => v * 10, 0);
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));

        Host.UnaryOp(stream, buffer.View, v => v - 1);
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
