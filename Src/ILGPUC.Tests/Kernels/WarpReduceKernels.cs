// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpReduceKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;

namespace ILGPUC.Tests.Kernels;

/// <summary>
/// Kernel definitions for warp-level reduce and scan operations
/// using the lambda-based API.
/// </summary>
static class WarpReduceKernels
{
    /// <summary>
    /// Warp AllReduce with addition: all lanes get the sum.
    /// </summary>
    public static void WarpAllReduceAddKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X + 1;
        int result = Warp.AllReduce(value, (a, b) => a + b);
        data[index] = result;
    }

    /// <summary>
    /// Warp AllReduce with max: all lanes get the maximum.
    /// Uses XMath.Max wrapped in a lambda. XMath.Max is a MathIntrinsic that
    /// lowers to BinaryArithmeticValue(Max); after Inliner folds the lambda
    /// body, TryRecognizeOperation picks it up as an intrinsic Max reduce.
    /// </summary>
    public static void WarpAllReduceMaxKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X;
        int result = Warp.AllReduce(value, (a, b) => XMath.Max(a, b));
        data[index] = result;
    }

    /// <summary>
    /// Warp AllReduce using a user-defined static method group directly
    /// (no wrapping lambda). Exercises the frontend <c>ldnull</c> +
    /// <c>ldftn</c> + <c>newobj Func&lt;&gt;</c> path for static targets —
    /// the <c>LoadNull</c> helper in
    /// <c>Frontend/CodeGenerator/Constants.cs</c> makes this compile.
    /// </summary>
    public static void WarpAllReduceAddMethodGroupKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X + 1;
        int result = Warp.AllReduce(value, AddInts);
        data[index] = result;
    }

    /// <summary>
    /// Plain static binary op used as a method group target.
    /// </summary>
    private static int AddInts(int a, int b) => a + b;

    /// <summary>
    /// Warp AllReduce using the <c>XMath.Max</c> intrinsic as a
    /// method group (no wrapping lambda). Exercises the frontend
    /// intrinsic-body synthesis path: ldftn on an intrinsic method
    /// with a registered generator triggers
    /// <c>TrySynthesizeIntrinsicBody</c>, which reifies <c>XMath.Max</c>
    /// into a one-block IR method containing a
    /// <c>BinaryArithmeticValue(Max)</c>. <c>TryRecognizeOperation</c>
    /// then classifies the WarpReduce's operation as <c>Max</c>.
    /// </summary>
    public static void WarpAllReduceMaxMethodGroupKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = index.X;
        int result = Warp.AllReduce(value, XMath.Max);
        data[index] = result;
    }

    /// <summary>
    /// Warp Reduce with addition: only lane 0 gets the result.
    /// </summary>
    public static void WarpReduceAddKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = 1;
        int result = Warp.Reduce(value, (a, b) => a + b);
        data[index] = result;
    }

    /// <summary>
    /// Warp InclusiveScan with addition.
    /// </summary>
    public static void WarpInclusiveScanAddKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = 1;
        int result = Warp.InclusiveScan(value, (a, b) => a + b);
        data[index] = result;
    }

    /// <summary>
    /// Warp ExclusiveScan with addition and identity 0.
    /// </summary>
    public static void WarpExclusiveScanAddKernel(
        Index1D index, ArrayView1D<int, Stride1D.Dense> data)
    {
        int value = 1;
        int result = Warp.ExclusiveScan(value, 0, (a, b) => a + b);
        data[index] = result;
    }
}
