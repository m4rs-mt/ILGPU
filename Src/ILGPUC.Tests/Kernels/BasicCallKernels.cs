// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicCallKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for method call tests.
/// </summary>
static class BasicCallKernels
{
    static int Add(int a, int b) => a + b;

    static int Multiply(int a, int b) => a * b;

    static int AddThenMultiply(int a, int b, int c) => Multiply(Add(a, b), c);

    static void ComputeOut(int a, int b, out int result)
    {
        result = a + b;
    }

    static void DoubleRef(ref int value)
    {
        value = value * 2;
    }

    /// <summary>
    /// Simple call: call Add and store result.
    /// </summary>
    public static void SimpleCallKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int a, int b)
    {
        data[index] = Add(a, b);
    }

    /// <summary>
    /// Nested call: call a method that calls another method.
    /// </summary>
    public static void NestedCallKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data,
        int a, int b, int c)
    {
        data[index] = AddThenMultiply(a, b, c);
    }

    /// <summary>
    /// Call with out parameter.
    /// </summary>
    public static void CallWithOutKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int a, int b)
    {
        ComputeOut(a, b, out int result);
        data[index] = result;
    }

    /// <summary>
    /// Call with ref parameter.
    /// </summary>
    public static void CallWithRefKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data, int value)
    {
        int v = value;
        DoubleRef(ref v);
        data[index] = v;
    }

    /// <summary>
    /// Chain of method calls.
    /// </summary>
    public static void ChainCallKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data,
        int a, int b, int c)
    {
        int step1 = Add(a, b);
        int step2 = Multiply(step1, c);
        int step3 = Add(step2, a);
        data[index] = step3;
    }
}
