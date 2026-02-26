// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupReduceIRTests.cs
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

public sealed class GroupReduceIRTests : CompilationTestBase
{
    public GroupReduceIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void GroupAllReduceAdd_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(GroupReduceKernels),
            nameof(GroupReduceKernels.GroupAllReduceAddKernel)));

    [Fact]
    public void GroupReduceAdd_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(GroupReduceKernels),
            nameof(GroupReduceKernels.GroupReduceAddKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupAllReduceAdd_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(GroupReduceKernels),
            nameof(GroupReduceKernels.GroupAllReduceAddKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupReduceAdd_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(GroupReduceKernels),
            nameof(GroupReduceKernels.GroupReduceAddKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupAllReduceAdd_AfterBackendTransforms(
        BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(GroupReduceKernels),
            nameof(GroupReduceKernels.GroupAllReduceAddKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void GroupReduceAdd_AfterBackendTransforms(
        BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(GroupReduceKernels),
            nameof(GroupReduceKernels.GroupReduceAddKernel)),
            backend, opt, mode);
}
