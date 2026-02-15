// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StaticLambdaIRTests.cs
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

public sealed class StaticLambdaIRTests : CompilationTestBase
{
    public StaticLambdaIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void StaticLambdaAdd_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(LambdaKernels),
            nameof(LambdaKernels.StaticLambdaAddKernel)));

    [Fact]
    public void StaticLambdaInline_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(LambdaKernels),
            nameof(LambdaKernels.StaticLambdaInlineKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void StaticLambdaAdd_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(LambdaKernels),
            nameof(LambdaKernels.StaticLambdaAddKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void StaticLambdaInline_AfterGlobalOpt(
        OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(LambdaKernels),
            nameof(LambdaKernels.StaticLambdaInlineKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void StaticLambdaAdd_AfterBackendTransforms(
        BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(LambdaKernels),
            nameof(LambdaKernels.StaticLambdaAddKernel)),
            backend, opt, mode);
}
