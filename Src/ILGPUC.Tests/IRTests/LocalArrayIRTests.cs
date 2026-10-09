// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalArrayIRTests.cs
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

public sealed class LocalArrayIRTests : CompilationTestBase
{
    public LocalArrayIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void WriteRead_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.WriteReadKernel)));

    [Fact]
    public void Length_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.LengthKernel)));

    [Fact]
    public void Accumulate_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.AccumulateKernel)));

    [Fact]
    public void IndexCompute_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.IndexComputeKernel)));

    [Fact]
    public void FloatArray_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.FloatArrayKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WriteRead_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.WriteReadKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Length_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.LengthKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Accumulate_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.AccumulateKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IndexCompute_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.IndexComputeKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void FloatArray_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.FloatArrayKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WriteRead_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.WriteReadKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Length_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.LengthKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Accumulate_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.AccumulateKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void IndexCompute_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.IndexComputeKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void FloatArray_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(LocalArrayKernels), nameof(LocalArrayKernels.FloatArrayKernel)),
            backend, opt, mode);
}
