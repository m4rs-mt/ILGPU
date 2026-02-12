// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ReinterpretCastIRTests.cs
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

public sealed class ReinterpretCastIRTests : CompilationTestBase
{
    public ReinterpretCastIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void FloatToUInt32_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ReinterpretCastKernels), nameof(ReinterpretCastKernels.FloatToUInt32Kernel)));

    [Fact]
    public void UInt32ToFloat_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ReinterpretCastKernels), nameof(ReinterpretCastKernels.UInt32ToFloatKernel)));

    [Fact]
    public void DoubleToUInt64_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ReinterpretCastKernels), nameof(ReinterpretCastKernels.DoubleToUInt64Kernel)));

    [Fact]
    public void UInt64ToDouble_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ReinterpretCastKernels), nameof(ReinterpretCastKernels.UInt64ToDoubleKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void FloatToUInt32_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ReinterpretCastKernels), nameof(ReinterpretCastKernels.FloatToUInt32Kernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void UInt32ToFloat_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ReinterpretCastKernels), nameof(ReinterpretCastKernels.UInt32ToFloatKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void DoubleToUInt64_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ReinterpretCastKernels), nameof(ReinterpretCastKernels.DoubleToUInt64Kernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void UInt64ToDouble_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ReinterpretCastKernels), nameof(ReinterpretCastKernels.UInt64ToDoubleKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void FloatToUInt32_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(ReinterpretCastKernels), nameof(ReinterpretCastKernels.FloatToUInt32Kernel)),
            backend, opt, mode);
}
