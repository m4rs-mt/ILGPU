// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Matrix4x4DecomposeIterative.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Aligned-TRS round-trip for the iterative Higham polar decomposition.
// On an already-orthogonal input the iteration converges in a few steps
// to identity, recovering the same scale/rotation/translation that the
// closed-form path produces.

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Numerics;
using ILGPU.Runtime;

static class Kernels
{
    public static void DecomposeIterativeKernel(
        Index1D index,
        ArrayView1D<float, Stride1D.Dense> output,
        Matrix4x4 a)
    {
        if (a.TryDecomposeIterative(out var scale, out var rot, out var trans))
        {
            output[index * 10 + 0] = scale.X;
            output[index * 10 + 1] = scale.Y;
            output[index * 10 + 2] = scale.Z;
            output[index * 10 + 3] = rot.X;
            output[index * 10 + 4] = rot.Y;
            output[index * 10 + 5] = rot.Z;
            output[index * 10 + 6] = rot.W;
            output[index * 10 + 7] = trans.X;
            output[index * 10 + 8] = trans.Y;
            output[index * 10 + 9] = trans.Z;
        }
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

        var matrix =
            Matrix4x4.CreateScale(2f, 3f, 4f)
            * Matrix4x4.CreateTranslation(10f, 20f, 30f);

        using var buffer = stream.Allocate1D<float>(10);

        stream.Launch(
            (Index1D)1,
            index => Kernels.DecomposeIterativeKernel(index, buffer.View, matrix));
        stream.Synchronize();

        foreach (var f in buffer.GetAsArray1D())
            Console.WriteLine(f.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
    }
}
