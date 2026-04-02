// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SwitchMap.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: SwitchMap
// Kernel maps switch value 2 → 30.
// Expected output: 30 30 30 30

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void SwitchMapKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        int result;
        switch (value)
        {
            case 0: result = 10; break;
            case 1: result = 20; break;
            case 2: result = 30; break;
            case 3: result = 40; break;
            default: result = -1; break;
        }
        data[index] = result;
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

        using var buffer = stream.Allocate1D<int>(4);
        stream.Launch((Index1D)4, index => Kernels.SwitchMapKernel(index, buffer.View, 2));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
