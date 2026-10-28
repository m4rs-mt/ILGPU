// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TailRecursionAddresses.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: TailRecursionAddresses
// Helpers pass the address of a local variable or of a parameter as an nint to the
// call of themselves in tail position, and every call reads the variable of its
// caller. They stay recursive, as a loop would use one variable for all calls.
// Expected output: 0 0 2 2 5 5 9 9 14 14 20 20 27 27 35 35

using System;
using ILGPU;
using ILGPU.Runtime;

static unsafe class Kernels
{
    static int SumCallerLocals(int n, nint caller, int sum)
    {
        int x = n;
        if (n == 0)
            return sum;
        return SumCallerLocals(n - 1, (nint)(&x), sum + *(int*)caller);
    }

    static int SumCallerArguments(int n, nint caller, int sum)
    {
        if (n == 0)
            return sum;
        return SumCallerArguments(n - 1, (nint)(&n), sum + *(int*)caller);
    }

    public static void TailRecursionAddressesKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int start = 0;
        data[index * 2] = SumCallerLocals(index + 1, (nint)(&start), 0);
        data[index * 2 + 1] = SumCallerArguments(index + 1, (nint)(&start), 0);
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

        using var buffer = stream.Allocate1D<int>(16);
        buffer.View.MemSetToZero(stream);
        stream.Launch(
            (Index1D)8,
            index => Kernels.TailRecursionAddressesKernel(index, buffer.View));
        stream.Synchronize();

        Console.WriteLine(string.Join(" ", buffer.GetAsArray1D()));
    }
}
