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

// LocalNuGetConsumer/Consumer: a real consumer project that pulls
// ILGPU + ILGPUC from a *local* NuGet feed. Useful when you're working
// on the ILGPUC compiler itself and want to verify what your downstream
// users will see when they install your in-progress nupkg.
//
// See ../README.md for the full walkthrough. Short version:
//   1. ./pack-local.sh           (in the parent directory)
//   2. cd Consumer && dotnet build
//   3. dotnet run

using System;
using ILGPU;
using ILGPU.Runtime;

namespace Consumer;

static class Kernels
{
    // Trivial kernel — the point of this sample is the *workflow*
    // (consuming a local nupkg), not the kernel pattern. Other samples
    // (KernelLibraryAttribute, AlgorithmsReduce, MatrixMultiply, ...)
    // demonstrate richer kernels.
    public static void DoubleKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        data[index] = index.X * 2;
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

        using var buffer = stream.Allocate1D<int>(8);
        stream.Launch(
            (Index1D)8,
            index => Kernels.DoubleKernel(index, buffer.View));
        stream.Synchronize();

        var data = buffer.GetAsArray1D();
        for (int i = 0; i < data.Length; i++)
            Console.WriteLine($"{i} = {data[i]}");
    }
}
