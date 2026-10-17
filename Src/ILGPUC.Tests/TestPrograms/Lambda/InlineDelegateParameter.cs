// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlineDelegateParameter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: InlineDelegateParameter
// An inline kernel invokes a delegate parameter of the launching method
// together with a captured scalar. One kernel is compiled for each lambda
// passed by the callers, an anonymous method and a static lambda.
// Expected output: 13 16 19 22 / 11 14 17 20

using System;
using ILGPU;
using ILGPU.Runtime;

static class Program
{
    static void Apply(
        AcceleratorStream stream,
        ArrayView1D<int, Stride1D.Dense> view,
        int offset,
        Func<int, int> op)
    {
        stream.Launch(view.IntExtent, i => view[i] = op(view[i]) + offset);
    }

    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var buffer = stream.Allocate1D<int>(new[] { 1, 2, 3, 4 });

        Apply(stream, buffer.View, 10, delegate (int v) { return v * 3; });
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));

        Apply(stream, buffer.View, 1, static v => v - 3);
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
