// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DelegateParameter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: DelegateParameter
// A host method launches a kernel method taking a Func with the delegate it
// receives (issue #463). Its callers pass two lambdas and a static method
// group, also from other classes in DelegateParameterCallers.cs, and one
// kernel is compiled for each of them. A delegate passed by a caller ILGPUC
// cannot see (here through reflection) is rejected at the launch.
// Expected output: -1 -2 -3 -4 / 0 1 2 3 / 0 2 4 6 / NotSupportedException

using System;
using System.Reflection;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void UnaryOpKernel(Index1D index, ArrayView<int> data, Func<int, int> op) =>
        data[index] = op(data[index]);
}

static class Host
{
    public static void UnaryOp(AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
    {
        stream.Launch(view.IntExtent, index => Kernels.UnaryOpKernel(index, view, op));
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

        using var buffer = stream.Allocate1D<int>(new[] { 1, 2, 3, 4 });

        Host.UnaryOp(stream, buffer.View, v => -v);
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));

        BitOps.Invert(stream, buffer.View);
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));

        MathOps.Double(stream, buffer.View);
        stream.Synchronize();
        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));

        try
        {
            Func<int, int> unknown = v => v + 1;
            typeof(Host).GetMethod("UnaryOp")!.Invoke(
                null, [stream, (ArrayView<int>)buffer.View, unknown]);
        }
        catch (TargetInvocationException e)
        {
            Console.WriteLine(e.InnerException!.GetType().Name);
        }
    }
}
