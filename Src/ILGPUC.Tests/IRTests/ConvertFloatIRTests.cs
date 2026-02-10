// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConvertFloatIRTests.cs
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

public sealed class ConvertFloatIRTests : CompilationTestBase
{
    public ConvertFloatIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void FloatToInt_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ConvertFloatKernels), nameof(ConvertFloatKernels.FloatToIntKernel)));

    [Fact]
    public void IntToFloat_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ConvertFloatKernels), nameof(ConvertFloatKernels.IntToFloatKernel)));

    [Fact]
    public void FloatToDouble_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ConvertFloatKernels), nameof(ConvertFloatKernels.FloatToDoubleKernel)));

    [Fact]
    public void DoubleToFloat_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ConvertFloatKernels), nameof(ConvertFloatKernels.DoubleToFloatKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void FloatToInt_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ConvertFloatKernels), nameof(ConvertFloatKernels.FloatToIntKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IntToFloat_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ConvertFloatKernels), nameof(ConvertFloatKernels.IntToFloatKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void FloatToDouble_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ConvertFloatKernels), nameof(ConvertFloatKernels.FloatToDoubleKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void DoubleToFloat_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ConvertFloatKernels), nameof(ConvertFloatKernels.DoubleToFloatKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void FloatToInt_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(ConvertFloatKernels), nameof(ConvertFloatKernels.FloatToIntKernel)),
            backend, opt, mode);
}
