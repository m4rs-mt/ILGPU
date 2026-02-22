// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupIRTests.cs
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

public sealed class GroupIRTests : CompilationTestBase
{
    public GroupIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void GroupDimension_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(GroupKernels), nameof(GroupKernels.GroupDimensionKernel)));

    [Fact]
    public void GroupBarrier_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(GroupKernels), nameof(GroupKernels.GroupBarrierKernel)));

    [Fact]
    public void GroupIdx_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(GroupKernels), nameof(GroupKernels.GroupIdxKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupDimension_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(GroupKernels), nameof(GroupKernels.GroupDimensionKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupBarrier_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(GroupKernels), nameof(GroupKernels.GroupBarrierKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupIdx_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(GroupKernels), nameof(GroupKernels.GroupIdxKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupDimension_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(GroupKernels), nameof(GroupKernels.GroupDimensionKernel)),
            backend, opt, mode);
}
