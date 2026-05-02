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

// EndToEndTest/HelloKernel: end-to-end smoke check for the published
// ILGPUC NuGet package. EndToEndTest/run.sh packs ILGPU + ILGPUC into a
// local feed, this project pulls them via PackageReference, and the
// targets file inside the ILGPUC package auto-imports + compiles the
// kernel below at build time.

using System;
using ILGPU;
using ILGPU.Runtime;

namespace HelloKernel;

static class Kernels
{
    // Same shape as the CompileBenchFacts workload in ILGPUC.Tests
    // (Src/ILGPUC.Tests/PerfTests/CompileBenchFacts.cs) —
    // result[i] = b[i] * c[i]. The bench, the perf regression layer,
    // and this E2E share one mental model for "the trivial kernel."
    public static void MultiplyKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> result,
        ArrayView1D<int, Stride1D.Dense> b,
        ArrayView1D<int, Stride1D.Dense> c)
    {
        result[index] = b[index] * c[index];
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

        // Inputs hard-coded so EndToEndTest/run.sh asserts a deterministic
        // expected output. Picking values that span positive / negative /
        // zero and cover a few different magnitudes — enough to catch a
        // codegen mistake without making the assertion fragile.
        using var b = stream.Allocate1D(new[] { 5, 3, -2, -5, 8, -2, 0, 0 });
        using var c = stream.Allocate1D(new[] { 10, 9, 2, 3, 6, 4, 3, 9 });
        using var result = stream.Allocate1D<int>(b.Length);

        stream.Launch(
            (Index1D)(int)b.Length,
            index => Kernels.MultiplyKernel(index, result.View, b.View, c.View));
        stream.Synchronize();

        var data = result.GetAsArray1D();
        for (int i = 0; i < data.Length; i++)
            Console.WriteLine($"{i} = {data[i]}");
    }
}
