// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicCallIRTests.cs
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

public sealed class BasicCallIRTests : CompilationTestBase
{
    public BasicCallIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void SimpleCall_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicCallKernels), nameof(BasicCallKernels.SimpleCallKernel)));

    [Fact]
    public void NestedCall_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicCallKernels), nameof(BasicCallKernels.NestedCallKernel)));

    [Fact]
    public void CallWithOut_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicCallKernels), nameof(BasicCallKernels.CallWithOutKernel)));

    [Fact]
    public void CallWithRef_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicCallKernels), nameof(BasicCallKernels.CallWithRefKernel)));

    [Fact]
    public void ChainCall_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicCallKernels), nameof(BasicCallKernels.ChainCallKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SimpleCall_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicCallKernels), nameof(BasicCallKernels.SimpleCallKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void NestedCall_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicCallKernels), nameof(BasicCallKernels.NestedCallKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void CallWithOut_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicCallKernels), nameof(BasicCallKernels.CallWithOutKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void CallWithRef_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicCallKernels), nameof(BasicCallKernels.CallWithRefKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void ChainCall_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicCallKernels), nameof(BasicCallKernels.ChainCallKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SimpleCall_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(BasicCallKernels), nameof(BasicCallKernels.SimpleCallKernel)),
            backend, opt, mode);
}
