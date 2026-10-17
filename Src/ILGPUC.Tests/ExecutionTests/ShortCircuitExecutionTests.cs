// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ShortCircuitExecutionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

/// <summary>
/// Truth-table sweeps for the short-circuit kernels folded by the
/// IfConditionConversion pass. Every program encodes the full truth table of
/// its condition terms in the thread index, so any partition violation in the
/// folded branch ladder (impossible-path terms, dropped disjuncts) shows up as
/// a wrong output line. The DivGuard programs include a ZERO divisor: if the
/// pass ever speculates the guarded division, the CPU backend throws and the
/// test fails loudly.
/// </summary>
public abstract class ShortCircuitExecutionTests : ExecutionTestBase
{
    protected ShortCircuitExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task AndChain_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/AndChain.cs",
            ["Kernels.AndChainKernel"],
            ["0", "0", "0", "0", "0", "0", "0", "1"]);
    }

    [SkippableFact]
    public async Task OrChain_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/OrChain.cs",
            ["Kernels.OrChainKernel"],
            ["0", "1", "1", "1", "1", "1", "1", "1"]);
    }

    [SkippableFact]
    public async Task MixedNesting1_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/MixedNesting1.cs",
            ["Kernels.MixedNesting1Kernel"],
            ["0", "0", "0", "0", "0", "1", "1", "1"]);
    }

    [SkippableFact]
    public async Task MixedNesting2_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/MixedNesting2.cs",
            ["Kernels.MixedNesting2Kernel"],
            ["0", "0", "0", "1", "0", "1", "0", "1"]);
    }

    [SkippableFact]
    public async Task NegatedChain_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/NegatedChain.cs",
            ["Kernels.NegatedChainKernel"],
            ["1", "1", "1", "0"]);
    }

    [SkippableFact]
    public async Task DeepMixed_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/DeepMixed.cs",
            ["Kernels.DeepMixedKernel"],
            [
                "0", "0", "0", "1", "0", "1", "0", "1",
                "0", "0", "0", "1", "0", "1", "0", "1",
                "0", "0", "0", "1", "0", "1", "0", "1",
                "1", "1", "1", "1", "1", "1", "1", "1",
            ]);
    }

    [SkippableFact]
    public async Task ThreeWayChain_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/ThreeWayChain.cs",
            ["Kernels.ThreeWayChainKernel"],
            ["30", "10", "10", "10", "20", "10", "10", "10"]);
    }

    [SkippableFact]
    public async Task DiamondFeedChain_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/DiamondFeedChain.cs",
            ["Kernels.DiamondFeedChainKernel"],
            [
                "0", "0", "0", "0", "0", "0", "0", "0",
                "0", "0", "0", "1", "1", "0", "1", "1",
            ]);
    }

    [SkippableFact]
    public async Task SelectMerge_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/SelectMerge.cs",
            ["Kernels.SelectMergeKernel"],
            ["9", "9", "9", "7"]);
    }

    [SkippableFact]
    public async Task MultiExitPhi_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/MultiExitPhi.cs",
            ["Kernels.MultiExitPhiKernel"],
            ["7", "5", "6", "5"]);
    }

    [SkippableFact]
    public async Task DeepChain_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/DeepChain.cs",
            ["Kernels.DeepChainKernel"],
            ["0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "1"]);
    }

    [SkippableFact]
    public async Task DivGuard_ProducesCorrectOutput()
    {
        Skip.If(
            Backend != BackendType.CPU,
            "Not yet supported by the structured control flow reconstruction");
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/DivGuard.cs",
            ["Kernels.DivGuardKernel"],
            ["0", "0", "1", "1"]);
    }

    [SkippableFact]
    public async Task DivGuardPhi_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/DivGuardPhi.cs",
            ["Kernels.DivGuardPhiKernel"],
            ["-10", "0", "10", "5"]);
    }

    [SkippableFact]
    public async Task GuardedLoad_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/GuardedLoad.cs",
            ["Kernels.GuardedLoadKernel"],
            ["7", "0", "0", "0"]);
    }

    [SkippableFact]
    public async Task GuardedAtomic_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ShortCircuit/GuardedAtomic.cs",
            ["Kernels.GuardedAtomicKernel"],
            ["0", "0", "1", "1", "2"]);
    }
}
