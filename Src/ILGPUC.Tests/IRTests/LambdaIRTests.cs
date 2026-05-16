// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LambdaIRTests.cs
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

public sealed class LambdaIRTests : CompilationTestBase
{
    public LambdaIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void SimpleCapture_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(LambdaKernels), nameof(LambdaKernels.SimpleCaptureKernel)));

    [Fact]
    public void MultiCapture_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(LambdaKernels), nameof(LambdaKernels.MultiCaptureKernel)));

    [Fact]
    public void LambdaConditional_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(LambdaKernels), nameof(LambdaKernels.LambdaConditionalKernel)));

    [Fact]
    public void LambdaAppliedTwice_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(LambdaKernels), nameof(LambdaKernels.LambdaAppliedTwiceKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SimpleCapture_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(LambdaKernels), nameof(LambdaKernels.SimpleCaptureKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void MultiCapture_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(LambdaKernels), nameof(LambdaKernels.MultiCaptureKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LambdaConditional_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(LambdaKernels), nameof(LambdaKernels.LambdaConditionalKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LambdaAppliedTwice_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(LambdaKernels), nameof(LambdaKernels.LambdaAppliedTwiceKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SimpleCapture_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(LambdaKernels), nameof(LambdaKernels.SimpleCaptureKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void MultiCapture_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(LambdaKernels), nameof(LambdaKernels.MultiCaptureKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LambdaConditional_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(LambdaKernels), nameof(LambdaKernels.LambdaConditionalKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LambdaAppliedTwice_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(LambdaKernels), nameof(LambdaKernels.LambdaAppliedTwiceKernel)),
            backend, opt, mode);
}
