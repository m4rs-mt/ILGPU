// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpReduceIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

public sealed class WarpReduceIRTests : CompilationTestBase
{
    public WarpReduceIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void WarpAllReduceAdd_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(WarpReduceKernels),
            nameof(WarpReduceKernels.WarpAllReduceAddKernel)));

    [Fact]
    public void WarpReduceAdd_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(WarpReduceKernels),
            nameof(WarpReduceKernels.WarpReduceAddKernel)));

    [Fact]
    public void WarpAllReduceMax_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(WarpReduceKernels),
            nameof(WarpReduceKernels.WarpAllReduceMaxKernel)));

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WarpAllReduceMax_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(WarpReduceKernels),
            nameof(WarpReduceKernels.WarpAllReduceMaxKernel)), opt, mode);

    [Fact]
    public void WarpInclusiveScanAdd_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(WarpReduceKernels),
            nameof(WarpReduceKernels.WarpInclusiveScanAddKernel)));

    [Fact]
    public void WarpExclusiveScanAdd_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(WarpReduceKernels),
            nameof(WarpReduceKernels.WarpExclusiveScanAddKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WarpAllReduceAdd_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(WarpReduceKernels),
            nameof(WarpReduceKernels.WarpAllReduceAddKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WarpReduceAdd_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(WarpReduceKernels),
            nameof(WarpReduceKernels.WarpReduceAddKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WarpAllReduceAdd_AfterBackendTransforms(
        BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(WarpReduceKernels),
            nameof(WarpReduceKernels.WarpAllReduceAddKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WarpReduceAdd_AfterBackendTransforms(
        BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(WarpReduceKernels),
            nameof(WarpReduceKernels.WarpReduceAddKernel)),
            backend, opt, mode);
}
