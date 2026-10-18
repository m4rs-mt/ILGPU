// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vector3Transform.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
// Test program: Vector3Transform
// Kernel transforms a point cloud by a Matrix4x4 (pure translation by
// (10, 20, 30) so the math is integer-clean). Skips the origin through
// an early return.
//
// Input  cloud:  [(1,2,3), (0,0,0), (4,5,6)]
// Transform: Translation(10, 20, 30)
// Output cloud:  [(11,22,33), (0,0,0), (14,25,36)]
// Expected stdout (one line per component):
//   11 22 33 0 0 0 14 25 36

using System;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void Vector3TransformKernel(
        Index1D index,
        ArrayView1D<Vector3, Stride1D.Dense> cloud,
        Matrix4x4 transform)
    {
        if (cloud[index] == Vector3.Zero)
            return;
        cloud[index] = Vector3.Transform(cloud[index], transform);
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

        Vector3[] inputData =
        [
            new Vector3(1, 2, 3),
            new Vector3(0, 0, 0),
            new Vector3(4, 5, 6),
        ];
        using var buffer = stream.Allocate1D(inputData);
        var transform = Matrix4x4.CreateTranslation(10, 20, 30);

        stream.Launch(
            (Index1D)inputData.Length,
            index => Kernels.Vector3TransformKernel(index, buffer.View, transform));
        stream.Synchronize();

        var output = buffer.GetAsArray1D();
        foreach (var v in output)
        {
            Console.WriteLine((int)v.X);
            Console.WriteLine((int)v.Y);
            Console.WriteLine((int)v.Z);
        }
    }
}
