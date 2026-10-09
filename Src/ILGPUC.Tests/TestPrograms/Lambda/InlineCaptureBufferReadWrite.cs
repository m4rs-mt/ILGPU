// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlineCaptureBufferReadWrite.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: InlineCaptureBufferReadWrite
// Inline lambda captures two MemoryBuffers (both lowered to views).
// First kernel populates source, second reads and doubles.
// Expected output: 2 4 6 8 (one per line)

using System;
using ILGPU;
using ILGPU.Runtime;

static class Program
{
    static void Main()
    {
        using var context = Context.Create(b => b.Default());
        using var accelerator = context.GetPreferredDevice(preferCPU: true)
            .CreateAccelerator(context);
        var stream = accelerator.DefaultStream;

        using var inBuf = stream.Allocate1D<int>(4);
        using var outBuf = stream.Allocate1D<int>(4);

        // First kernel: populate source via buffer.View
        stream.Launch((Index1D)4, index =>
        {
            inBuf.View[index] = index + 1;
        });
        stream.Synchronize();

        // Second kernel: read from source, write doubled to output
        stream.Launch((Index1D)4, index =>
        {
            outBuf.View[index] = inBuf.View[index] * 2;
        });
        stream.Synchronize();

        var data = outBuf.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
