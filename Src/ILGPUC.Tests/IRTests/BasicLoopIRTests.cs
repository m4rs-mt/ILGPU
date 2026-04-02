// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicLoopIRTests.cs
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

public sealed class BasicLoopIRTests : CompilationTestBase
{
    public BasicLoopIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void WhileLoop_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.WhileLoopKernel)));

    [Fact]
    public void ForLoop_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.ForLoopKernel)));

    [Fact]
    public void DoWhileLoop_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.DoWhileLoopKernel)));

    [Fact]
    public void NestedLoop_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.NestedLoopKernel)));

    [Fact]
    public void DivergentLoop_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.DivergentLoopKernel)));

    [Fact]
    public void LoopWithBreak_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.LoopWithBreakKernel)));

    [Fact]
    public void LoopWithContinue_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.LoopWithContinueKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WhileLoop_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.WhileLoopKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void ForLoop_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.ForLoopKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void DoWhileLoop_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.DoWhileLoopKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void NestedLoop_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.NestedLoopKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void DivergentLoop_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.DivergentLoopKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LoopWithBreak_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.LoopWithBreakKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void LoopWithContinue_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.LoopWithContinueKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void WhileLoop_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(BasicLoopKernels), nameof(BasicLoopKernels.WhileLoopKernel)),
            backend, opt, mode);
}
