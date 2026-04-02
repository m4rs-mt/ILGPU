// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicMovementIRTests.cs
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

public sealed class BasicMovementIRTests : CompilationTestBase
{
    public BasicMovementIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void BarrierOrdering_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicMovementKernels), nameof(BasicMovementKernels.BarrierOrderingKernel)));

    [Fact]
    public void Copy_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicMovementKernels), nameof(BasicMovementKernels.CopyKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void BarrierOrdering_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicMovementKernels), nameof(BasicMovementKernels.BarrierOrderingKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Copy_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicMovementKernels), nameof(BasicMovementKernels.CopyKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void BarrierOrdering_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(BasicMovementKernels), nameof(BasicMovementKernels.BarrierOrderingKernel)),
            backend, opt, mode);
}
