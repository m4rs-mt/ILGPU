// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodGroupIntrinsicIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System;
using Xunit;

namespace ILGPUC.Tests.IRTests;

/// <summary>
/// Kernels for <see cref="MethodGroupIntrinsicIRTests"/>. Declared outside the
/// <c>ILGPUC.Tests.Kernels</c> namespace so that <see cref="KernelRegistry"/> does
/// not add them to the shared backend corpus.
/// </summary>
static class MathMethodGroupKernels
{
    public static void MaxKernel(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
        d[i] = Warp.AllReduce(d[i], Math.Max);

    public static void FloatMinKernel(
        Index1D i,
        ArrayView1D<float, Stride1D.Dense> d) =>
        d[i] = Group.Reduce(d[i], float.MaxValue, Math.Min);

    public static void LongMaxScanKernel(
        Index1D i,
        ArrayView1D<long, Stride1D.Dense> d) =>
        d[i] = Warp.InclusiveScan(d[i], Math.Max);
}

/// <summary>
/// An intrinsic used as a method group gets a body synthesised from its generator when
/// the delegate is created. The frontend must not generate code for it a second time
/// when the method is called through the delegate.
/// </summary>
public sealed class MethodGroupIntrinsicIRTests
{
    [Fact]
    public void MethodGroupIntrinsic_IsGeneratedOnce()
    {
        var kernel = typeof(WarpReduceKernels).GetMethod(
            nameof(WarpReduceKernels.WarpAllReduceMaxMethodGroupKernel))!;

        using var recorder = AssertionRecorder.Start();
        using var helper = new CompilationHelper();
        var module = helper.CompileToModule(kernel, new CompilationProperties());

        Assert.Empty(recorder.Failures);
        Assert.True(module.NumMethods > 0);
    }

    /// <summary>
    /// A method group of a method that the frontend remaps to an intrinsic, such
    /// as <c>Math.Max</c> to <c>XMath.Max</c>, targets the intrinsic, so that a
    /// collective recognizes it as its operation.
    /// </summary>
    [Theory]
    [InlineData(
        nameof(MathMethodGroupKernels.MaxKernel),
        "@ilgpu.warp.reduce.AllReduce.Max(")]
    [InlineData(
        nameof(MathMethodGroupKernels.FloatMinKernel),
        "@ilgpu.group.reduce.Reduce.Min(")]
    [InlineData(
        nameof(MathMethodGroupKernels.LongMaxScanKernel),
        "@ilgpu.warp.scan.Inclusive.Max(")]
    public void RemappedMethodGroup_IsRecognizedAsOperation(
        string kernelName,
        string collective)
    {
        using var helper = new CompilationHelper();
        var ir = helper.GetNormalizedIR(
            typeof(MathMethodGroupKernels).GetMethod(kernelName)!,
            IRDumpPoint.AfterGlobalOpt);
        Assert.Contains(collective, ir, StringComparison.Ordinal);
    }
}
