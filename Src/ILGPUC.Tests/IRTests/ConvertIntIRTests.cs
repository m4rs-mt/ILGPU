// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConvertIntIRTests.cs
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

public sealed class ConvertIntIRTests : CompilationTestBase
{
    public ConvertIntIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void Truncate_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ConvertIntKernels), nameof(ConvertIntKernels.TruncateKernel)));

    [Fact]
    public void Promote_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ConvertIntKernels), nameof(ConvertIntKernels.PromoteKernel)));

    [Fact]
    public void ByteToInt_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ConvertIntKernels), nameof(ConvertIntKernels.ByteToIntKernel)));

    [Fact]
    public void IntToByte_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ConvertIntKernels), nameof(ConvertIntKernels.IntToByteKernel)));

    [Fact]
    public void SignExtend_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ConvertIntKernels), nameof(ConvertIntKernels.SignExtendKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Truncate_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ConvertIntKernels), nameof(ConvertIntKernels.TruncateKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Promote_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ConvertIntKernels), nameof(ConvertIntKernels.PromoteKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void ByteToInt_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ConvertIntKernels), nameof(ConvertIntKernels.ByteToIntKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IntToByte_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ConvertIntKernels), nameof(ConvertIntKernels.IntToByteKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SignExtend_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ConvertIntKernels), nameof(ConvertIntKernels.SignExtendKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Truncate_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(ConvertIntKernels), nameof(ConvertIntKernels.TruncateKernel)),
            backend, opt, mode);
}
