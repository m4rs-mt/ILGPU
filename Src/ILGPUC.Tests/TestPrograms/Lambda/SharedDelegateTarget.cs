// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedDelegateTarget.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SharedDelegateTarget
// The same static method is passed to the launching method from two call
// sites, which share the kernel compiled for it, and a launching method takes
// two delegate parameters, which compiles one kernel for each combination of
// their targets.
// Expected output: 2 4 6 8 / 4 8 12 16 / 1 5 9 13 / 1 9 17 25 / 3 7 11 15

using System;
using ILGPU;
using ILGPU.Runtime;

static class Ops
{
    public static int Twice(int value) => value * 2;

    public static int Decrement(int value) => value - 1;
}

static class Host
{
    public static void UnaryOp(
        AcceleratorStream stream, ArrayView1D<int, Stride1D.Dense> view, Func<int, int> op)
    {
        stream.Launch(view.IntExtent, index => view[index] = op(view[index]));
    }

    public static void Compose(
        AcceleratorStream stream,
        ArrayView1D<int, Stride1D.Dense> view,
        Func<int, int> first,
        Func<int, int> second)
    {
        stream.Launch(view.IntExtent, index => view[index] = second(first(view[index])));
    }
}

static class Program
{
    static void Print(MemoryBuffer1D<int, Stride1D.Dense> buffer) =>
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var buffer = stream.Allocate1D<int>(new[] { 1, 2, 3, 4 });

        Host.UnaryOp(stream, buffer.View, Ops.Twice);
        stream.Synchronize();
        Print(buffer);

        Host.UnaryOp(stream, buffer.View, Ops.Twice);
        stream.Synchronize();
        Print(buffer);

        Host.Compose(stream, buffer.View, Ops.Decrement, v => v - 2);
        stream.Synchronize();
        Print(buffer);

        Host.Compose(stream, buffer.View, Ops.Twice, Ops.Decrement);
        stream.Synchronize();
        Print(buffer);

        Host.Compose(stream, buffer.View, v => v + 3, v => v / 2 + 1);
        stream.Synchronize();
        Print(buffer);
    }
}
