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

using ILGPU;
using ILGPU.Runtime;
using System;
using System.Linq;

namespace Consumer;

/// <summary>
/// Companion to the <c>MyKernelLib</c> project in this sample. MyKernelLib
/// demonstrates the structure of a kernel-callable library tagged with
/// <c>[assembly: KernelLibrary]</c> — see MyKernelLib/AssemblyInfo.cs and
/// MyKernelLib/MathHelpers.cs for the form your own libraries should take.
///
/// The kernel in this consumer uses <see cref="XMath"/> directly rather than
/// MyKernelLib helpers, because cross-project kernel-callable methods in the
/// current Roslyn-driven <c>ilgpuc build</c> pipeline have a known issue
/// (the kernel's IR comes back empty when it transitively reaches a method
/// in a referenced project at build time). This is orthogonal to the
/// walkability classifier and the [KernelLibrary] attribute itself: when
/// invoked through <c>ilgpuc compile</c> on a pre-loaded MethodInfo, the
/// frontend correctly walks MyKernelLib because of rule (c) — the attribute.
/// MyKernelLib remains the reference for how to mark a library so the
/// frontend will walk it eagerly when consumers reach it transitively.
/// </summary>
static class Kernels
{
    public static void AbsDiffKernel(
        Index1D index,
        ArrayView<int> result,
        ArrayView<int> a,
        ArrayView<int> b,
        ArrayView<int> c)
    {
        // Equivalent to MyKernelLib.MathHelpers.SumAbsDiff(a[i], b[i], c[i]):
        result[index] =
            XMath.Abs(a[index] - b[index]) +
            XMath.Abs(b[index] - c[index]);
    }
}

static class Program
{
    static void Main()
    {
        using var context = Context.CreateDefault();

        var device = context.Devices
            .OrderByDescending(d => d.AcceleratorType switch
            {
                AcceleratorType.Metal  => 5,
                AcceleratorType.Cuda   => 4,
                AcceleratorType.ROCm   => 3,
                AcceleratorType.OpenCL => 2,
                AcceleratorType.CPU    => 1,
                _                      => 0,
            })
            .First();

        using var accelerator = device.CreateAccelerator(context);
        Console.WriteLine($"Using {accelerator}");
        var stream = accelerator.DefaultStream;

        const int Length = 8;
        using var a = stream.Allocate1D<int>(new[] { 10, -3,  4,  -5, 8, -2, 7, 0 });
        using var b = stream.Allocate1D<int>(new[] {  2,  9, -1,   3, 6,  4, 0, 5 });
        using var c = stream.Allocate1D<int>(new[] {  5, -7,  2,  -8, 1, -6, 3, 9 });
        using var result = stream.Allocate1D<int>(Length);

        stream.Launch(
            (Index1D)Length,
            index => Kernels.AbsDiffKernel(
                index, result.View, a.View, b.View, c.View));
        stream.Synchronize();

        var data = result.GetAsArray1D();
        for (int i = 0; i < data.Length; i++)
            Console.WriteLine($"|a-b|+|b-c| at {i} = {data[i]}");
    }
}
