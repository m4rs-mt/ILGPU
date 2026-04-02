// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalArrayKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for local array allocation tests (new T[N] inside kernels).
/// Exercises LowerArrays: ArrayValue, LoadArrayElementAddress, GetArrayLength.
/// </summary>
static class LocalArrayKernels
{
    /// <summary>
    /// Allocate local array, fill with constants, copy to output.
    /// </summary>
    public static void WriteReadKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] local = new int[4];
        local[0] = 10; local[1] = 20; local[2] = 30; local[3] = 40;
        output[index] = local[index];
    }

    /// <summary>
    /// Query local array Length property.
    /// </summary>
    public static void LengthKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] local = new int[4];
        output[index] = local.Length;
    }

    /// <summary>
    /// Loop-based scratch buffer: write then sum.
    /// </summary>
    public static void AccumulateKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] scratch = new int[3];
        scratch[0] = 1; scratch[1] = 2; scratch[2] = 3;
        int sum = 0;
        for (int i = 0; i < 3; ++i)
            sum += scratch[i];
        output[index] = sum;
    }

    /// <summary>
    /// Per-thread varying index into local lookup table.
    /// </summary>
    public static void IndexComputeKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> output)
    {
        int[] table = new int[4];
        table[0] = 100; table[1] = 200; table[2] = 300; table[3] = 400;
        output[index] = table[index.X];
    }

    /// <summary>
    /// Float element type local array.
    /// </summary>
    public static void FloatArrayKernel(
        Index1D index, ArrayView1D<float, Stride1D.Dense> output)
    {
        float[] local = new float[4];
        local[0] = 1.5f; local[1] = 2.5f; local[2] = 3.5f; local[3] = 4.5f;
        output[index] = local[index];
    }
}
