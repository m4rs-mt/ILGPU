// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NullArrayIRTests.cs
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

public sealed class NullArrayIRTests : CompilationTestBase
{
    public NullArrayIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void ConditionalNull_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(NullArrayKernels), nameof(NullArrayKernels.ConditionalNullKernel)));

    [Fact]
    public void NullArgument_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(NullArrayKernels), nameof(NullArrayKernels.NullArgumentKernel)));

    [Fact]
    public void EmptyNotNull_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(NullArrayKernels), nameof(NullArrayKernels.EmptyNotNullKernel)));

    [Fact]
    public void EmptyArray_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(NullArrayKernels), nameof(NullArrayKernels.EmptyArrayKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void ConditionalNull_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(NullArrayKernels), nameof(NullArrayKernels.ConditionalNullKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void NullArgument_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(NullArrayKernels), nameof(NullArrayKernels.NullArgumentKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void EmptyNotNull_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(NullArrayKernels), nameof(NullArrayKernels.EmptyNotNullKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void EmptyArray_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(NullArrayKernels), nameof(NullArrayKernels.EmptyArrayKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void ConditionalNull_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(NullArrayKernels), nameof(NullArrayKernels.ConditionalNullKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void NullArgument_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(NullArrayKernels), nameof(NullArrayKernels.NullArgumentKernel)),
            backend, opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void EmptyNotNull_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(NullArrayKernels), nameof(NullArrayKernels.EmptyNotNullKernel)),
            backend, opt, mode);
}
