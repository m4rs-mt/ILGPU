// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MultiDimArrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// Test program: MultiDimArrays
// Kernels create local 2D and 3D arrays with new T[,] and new T[,,], fill them in
// loops bounded by GetLength and read their lengths.
// Expected output: one line per lane and kernel.

using System;
using ILGPU;
using ILGPU.Runtime;

static class Kernels
{
    public static void GridKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        var grid = new int[2, 3];
        for (int row = 0; row < grid.GetLength(0); ++row)
        {
            for (int column = 0; column < grid.GetLength(1); ++column)
                grid[row, column] = row * 10 + column + index;
        }
        output[index] = grid[1, 2] * 100 + grid.Length + grid.GetLength(1);
    }

    public static void CubeKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> output)
    {
        var cube = new long[2, 2, 2];
        cube[1, 0, 1] = index + 5;
        output[index] = (int)(cube[1, 0, 1] + cube.GetLength(2) + cube[0, 0, 0]);
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

        using var output = stream.Allocate1D<int>(4);
        var view = output.View;

        stream.Launch((Index1D)4, i => Kernels.GridKernel(i, view));
        stream.Synchronize();
        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);

        stream.Launch((Index1D)4, i => Kernels.CubeKernel(i, view));
        stream.Synchronize();
        foreach (var v in output.GetAsArray1D())
            Console.WriteLine(v);
    }
}
