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
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;

namespace ILGPUC.Tests.IRTests;

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
}
