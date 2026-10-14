// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ShortCircuitIRTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.Backends;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.IRTests;

public sealed class ShortCircuitIRTests : CompilationTestBase
{
    public ShortCircuitIRTests(ITestOutputHelper output) : base(output) { }

    // --- AfterFrontend ---

    [Fact]
    public void AndChain_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.AndChainKernel)));

    [Fact]
    public void OrChain_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.OrChainKernel)));

    [Fact]
    public void MixedNesting1_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.MixedNesting1Kernel)));

    [Fact]
    public void MixedNesting2_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.MixedNesting2Kernel)));

    [Fact]
    public void NegatedChain_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.NegatedChainKernel)));

    [Fact]
    public void DeepMixed_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.DeepMixedKernel)));

    [Fact]
    public void ThreeWayChain_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.ThreeWayChainKernel)));

    [Fact]
    public void DiamondFeedChain_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.DiamondFeedChainKernel)));

    [Fact]
    public void SelectMerge_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.SelectMergeKernel)));

    [Fact]
    public void MultiExitPhi_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.MultiExitPhiKernel)));

    [Fact]
    public void DeepChain_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.DeepChainKernel)));

    [Fact]
    public void DivGuard_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.DivGuardKernel)));

    [Fact]
    public void DivGuardPhi_AfterFrontend() =>
        VerifyAfterFrontend(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.DivGuardPhiKernel)));

    // --- AfterGlobalOpt ---

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void AndChain_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.AndChainKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void OrChain_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.OrChainKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void MixedNesting1_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.MixedNesting1Kernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void MixedNesting2_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.MixedNesting2Kernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void NegatedChain_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.NegatedChainKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void DeepMixed_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.DeepMixedKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void ThreeWayChain_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.ThreeWayChainKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void DiamondFeedChain_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.DiamondFeedChainKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void SelectMerge_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.SelectMergeKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void MultiExitPhi_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.MultiExitPhiKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void DeepChain_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.DeepChainKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void DivGuard_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.DivGuardKernel)), opt, mode);

    [Theory]
    [MemberData(nameof(TestTypes.OptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void DivGuardPhi_AfterGlobalOpt(OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterGlobalOpt(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.DivGuardPhiKernel)), opt, mode);

    // --- AfterBackendTransforms ---

    [Theory]
    [MemberData(nameof(TestTypes.BackendTypesAndOptLevelsAndModes), MemberType = typeof(TestTypes))]
    public void AndChain_AfterBackendTransforms(BackendType backend, OptimizationLevel opt, CompilationMode mode) =>
        VerifyAfterBackendTransforms(GetKernel(
            typeof(ShortCircuitKernels), nameof(ShortCircuitKernels.AndChainKernel)),
            backend, opt, mode);

    // --- Predicate growth ---

    /// <summary>
    /// The if-converted predicates of eight chained short-circuit helpers share
    /// their operations: negations of shared predicates are created once and
    /// the predicates of rejoining branches fold, so the IR grows linearly with
    /// the chain length instead of exponentially (millions of comparisons).
    /// </summary>
    [Theory]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    public void PredicateChain_AfterGlobalOpt_GrowsLinearly(OptimizationLevel opt)
    {
        var ir = DumpIRString(
            GetKernel(
                typeof(ShortCircuitKernels),
                nameof(ShortCircuitKernels.PredicateChainKernel)),
            IRDumpPoint.AfterGlobalOpt,
            new CompilationProperties(OptimizationLevel: opt));

        int numCompares = ir.Split(" icmp ").Length - 1;
        Assert.InRange(numCompares, 1, 120);
    }
}
