// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicIfIRTests.cs
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

public sealed class BasicIfIRTests : CompilationTestBase
{
    public BasicIfIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void IfTrue_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.IfTrueKernel)));

    [Fact]
    public void IfFalse_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.IfFalseKernel)));

    [Fact]
    public void IfSideEffects_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.IfSideEffectsKernel)));

    [Fact]
    public void IfAndOr_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.IfAndOrKernel)));

    [Fact]
    public void NestedIf_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.NestedIfKernel)));

    [Fact]
    public void IfChain_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.IfChainKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IfTrue_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.IfTrueKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IfFalse_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.IfFalseKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IfSideEffects_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.IfSideEffectsKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IfAndOr_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.IfAndOrKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void NestedIf_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.NestedIfKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IfChain_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.IfChainKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IfTrue_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.IfTrueKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IfSideEffects_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(BasicIfKernels), nameof(BasicIfKernels.IfSideEffectsKernel)),
            backend, opt, mode);
}
