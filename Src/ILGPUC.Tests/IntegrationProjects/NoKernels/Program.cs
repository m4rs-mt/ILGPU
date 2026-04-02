// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// NoKernels: a console app that imports ILGPU types but never calls
// stream.Launch(). The .targets file should produce an empty manifest and
// leave @(Compile) untouched. The build must still succeed.
// Expected output: hello no kernels

using System;
using ILGPU;
using ILGPU.Runtime;

namespace NoKernels;

static class Program
{
    static void Main()
    {
        // Touch ILGPU types so the reference isn't elided, but never launch.
        using var context = Context.Create(b => b.Default());
        var device = context.GetPreferredDevice(preferCPU: true);
        Console.WriteLine("hello no kernels");
        GC.KeepAlive(device);
    }
}
