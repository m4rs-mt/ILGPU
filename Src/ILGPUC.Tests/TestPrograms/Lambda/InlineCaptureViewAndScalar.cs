// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlineCaptureViewAndScalar.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: InlineCaptureViewAndScalar
// Inline lambda captures two views and a scalar.
// Uses first launch to populate input, second to compute output.
// Computes: output[i] = (i + 1) * scalar
// scalar = 10
// Expected output: 10 20 30 40 (one per line)

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

        using var outputBuffer = stream.Allocate1D<int>(4);
        var outputView = outputBuffer.View;
        int scalar = 10;

        stream.Launch((Index1D)4, index =>
        {
            outputView[index] = (index + 1) * scalar;
        });
        stream.Synchronize();

        var data = outputBuffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
