// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using ILGPU;
using ILGPU.Initialization;
using ILGPU.Runtime;
using ILGPU.ScanReduce;

namespace Reduce;

static class Program
{
    static void Main()
    {
        using var context = Context.CreateDefault();

        var device = context.GetPreferredDevice(preferCPU: false);

        using var accelerator = device.CreateAccelerator(context);
        Console.WriteLine($"Using {accelerator}");
        var stream = accelerator.DefaultStream;

        using var buffer = stream.Allocate1D<int>(64);

        // Fill the buffer with a sequence 0..63
        stream.Sequence(buffer.View, i => (int)i);

        // Reduce: sum all elements -> returns the scalar result directly
        int result = stream.Reduce(
            buffer.View,
            identity: 0,
            apply: (a, b) => a + b,
            atomicApply: (ref int t, int v) => Atomic.Add(ref t, v));
        stream.Synchronize();

        Console.WriteLine($"Reduced = {result}");
    }
}
