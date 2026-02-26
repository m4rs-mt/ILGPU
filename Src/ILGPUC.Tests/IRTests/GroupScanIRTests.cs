// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupScanIRTests.cs
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

public sealed class GroupScanIRTests : CompilationTestBase
{
    public GroupScanIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void GroupInclusiveScanAdd_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(GroupScanKernels),
            nameof(GroupScanKernels.GroupInclusiveScanAddKernel)));

    [Fact]
    public void GroupExclusiveScanAdd_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(GroupScanKernels),
            nameof(GroupScanKernels.GroupExclusiveScanAddKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupInclusiveScanAdd_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(GroupScanKernels),
            nameof(GroupScanKernels.GroupInclusiveScanAddKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupExclusiveScanAdd_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(GroupScanKernels),
            nameof(GroupScanKernels.GroupExclusiveScanAddKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupInclusiveScanAdd_AfterBackendTransforms(
        BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(GroupScanKernels),
            nameof(GroupScanKernels.GroupInclusiveScanAddKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupExclusiveScanAdd_AfterBackendTransforms(
        BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(GroupScanKernels),
            nameof(GroupScanKernels.GroupExclusiveScanAddKernel)),
            backend, opt, mode);
}
