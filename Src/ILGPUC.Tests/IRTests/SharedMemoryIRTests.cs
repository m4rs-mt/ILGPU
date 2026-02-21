// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemoryIRTests.cs
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

public sealed class SharedMemoryIRTests : CompilationTestBase
{
    public SharedMemoryIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void SharedMemoryVariable_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(SharedMemoryKernels), nameof(SharedMemoryKernels.SharedMemoryVariableKernel)));

    [Fact]
    public void SharedMemoryArray_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(SharedMemoryKernels), nameof(SharedMemoryKernels.SharedMemoryArrayKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SharedMemoryVariable_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(SharedMemoryKernels), nameof(SharedMemoryKernels.SharedMemoryVariableKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SharedMemoryArray_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(SharedMemoryKernels), nameof(SharedMemoryKernels.SharedMemoryArrayKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SharedMemoryVariable_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(SharedMemoryKernels), nameof(SharedMemoryKernels.SharedMemoryVariableKernel)),
            backend, opt, mode);
}
