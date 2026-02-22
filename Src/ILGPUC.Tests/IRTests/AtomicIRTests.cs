// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicIRTests.cs
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

public sealed class AtomicIRTests : CompilationTestBase
{
    public AtomicIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void AtomicAdd_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(AtomicKernels), nameof(AtomicKernels.AtomicAddKernel)));

    [Fact]
    public void AtomicMax_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(AtomicKernels), nameof(AtomicKernels.AtomicMaxKernel)));

    [Fact]
    public void AtomicMin_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(AtomicKernels), nameof(AtomicKernels.AtomicMinKernel)));

    [Fact]
    public void AtomicExchange_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(AtomicKernels), nameof(AtomicKernels.AtomicExchangeKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void AtomicAdd_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(AtomicKernels), nameof(AtomicKernels.AtomicAddKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void AtomicMax_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(AtomicKernels), nameof(AtomicKernels.AtomicMaxKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void AtomicMin_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(AtomicKernels), nameof(AtomicKernels.AtomicMinKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void AtomicExchange_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(AtomicKernels), nameof(AtomicKernels.AtomicExchangeKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void AtomicAdd_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(AtomicKernels), nameof(AtomicKernels.AtomicAddKernel)),
            backend, opt, mode);
}
