// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

public sealed class WarpIRTests : CompilationTestBase
{
    public WarpIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void WarpSize_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(WarpKernels), nameof(WarpKernels.WarpSizeKernel)));

    [Fact]
    public void WarpLaneIdx_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(WarpKernels), nameof(WarpKernels.WarpLaneIdxKernel)));

    [Fact]
    public void WarpBarrier_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(WarpKernels), nameof(WarpKernels.WarpBarrierKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WarpSize_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(WarpKernels), nameof(WarpKernels.WarpSizeKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WarpLaneIdx_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(WarpKernels), nameof(WarpKernels.WarpLaneIdxKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WarpBarrier_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(WarpKernels), nameof(WarpKernels.WarpBarrierKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WarpSize_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(WarpKernels), nameof(WarpKernels.WarpSizeKernel)),
            backend, opt, mode);
}
