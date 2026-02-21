// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: FixedBufferIRTests.cs
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

public sealed class FixedBufferIRTests : CompilationTestBase
{
    public FixedBufferIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void FixedRead_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(FixedBufferKernels), nameof(FixedBufferKernels.FixedReadKernel)));

    [Fact]
    public void FixedWrite_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(FixedBufferKernels), nameof(FixedBufferKernels.FixedWriteKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void FixedRead_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(FixedBufferKernels), nameof(FixedBufferKernels.FixedReadKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void FixedWrite_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(FixedBufferKernels), nameof(FixedBufferKernels.FixedWriteKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void FixedRead_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(FixedBufferKernels), nameof(FixedBufferKernels.FixedReadKernel)),
            backend, opt, mode);
}
