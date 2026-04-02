// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlineMultiStatement.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: InlineMultiStatement
// Inline lambda with multiple statements and local variables.
// Computes: temp = (index + 1) * 2; output[index] = temp + offset
// offset=5
// Expected output: 7 9 11 13 (one per line)

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
        var output = outputBuffer.View;
        int offset = 5;

        stream.Launch((Index1D)4, index =>
        {
            int temp = (index + 1) * 2;
            output[index] = temp + offset;
        });
        stream.Synchronize();

        var data = outputBuffer.GetAsArray1D();
        foreach (var v in data)
            Console.WriteLine(v);
    }
}
