// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueTupleKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// ValueTuple creation and access kernels.
/// </summary>
static class ValueTupleKernels
{
    public static void TupleCreateKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> intOutput,
        ArrayView1D<float, Stride1D.Dense> floatOutput)
    {
        var tuple = (index.X * 2, index.X * 3.0f);
        intOutput[index] = tuple.Item1;
        floatOutput[index] = tuple.Item2;
    }

    static (int, float) CreateTuple(int a, float b)
    {
        return (a + 1, b + 1.0f);
    }

    public static void TuplePassKernel(
        Index1D index,
        ArrayView1D<int, Stride1D.Dense> intOutput,
        ArrayView1D<float, Stride1D.Dense> floatOutput)
    {
        var tuple = CreateTuple(index.X, (float)index.X);
        intOutput[index] = tuple.Item1;
        floatOutput[index] = tuple.Item2;
    }
}
