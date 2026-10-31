// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerArithmeticIRTests.cs
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

public sealed class PointerArithmeticIRTests : CompilationTestBase
{
    public PointerArithmeticIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void LocalOffsets_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(PointerArithmeticKernels), nameof(PointerArithmeticKernels.LocalOffsetsKernel)));

    [Fact]
    public void Move_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(PointerArithmeticKernels), nameof(PointerArithmeticKernels.MoveKernel)));

    [Fact]
    public void Bytes_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(PointerArithmeticKernels), nameof(PointerArithmeticKernels.BytesKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LocalOffsets_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(PointerArithmeticKernels), nameof(PointerArithmeticKernels.LocalOffsetsKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void Bytes_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(PointerArithmeticKernels), nameof(PointerArithmeticKernels.BytesKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LocalOffsets_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(PointerArithmeticKernels), nameof(PointerArithmeticKernels.LocalOffsetsKernel)),
            backend, opt, mode);
}
