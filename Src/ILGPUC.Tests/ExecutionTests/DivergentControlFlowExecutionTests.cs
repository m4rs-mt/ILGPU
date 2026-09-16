// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DivergentControlFlowExecutionTests.cs
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

public abstract class DivergentControlFlowExecutionTests : ExecutionTestBase
{
    protected DivergentControlFlowExecutionTests(
        ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    /// <summary>
    /// A view merged after divergent branches with side effects is written by every lane
    /// that reaches the merge.
    /// </summary>
    [SkippableFact]
    public async Task BranchViewMerge_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/BranchViewMerge.cs",
            ["Kernels.BranchViewMergeKernel"],
            ["0", "1013", "22", "0", "22", "1013", "0", "1013", "22", "0", "22", "1013"]);
    }

    [SkippableFact]
    public async Task LoopBreakFirstIteration_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/LoopBreakFirstIteration.cs",
            ["Kernels.LoopBreakFirstIterationKernel"],
            ["0", "0", "1", "3", "6", "10", "15", "21", "28", "0", "0", "1"]);
    }

    [SkippableFact]
    public async Task LoopDivergentContinue_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/LoopDivergentContinue.cs",
            ["Kernels.LoopDivergentContinueKernel"],
            ["27", "30", "33", "27", "30", "33", "27", "30", "33", "27", "30", "33"]);
    }

    [SkippableFact]
    public async Task LoopEarlyReturn_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/LoopEarlyReturn.cs",
            ["Kernels.LoopEarlyReturnKernel"],
            ["-1", "-1", "7", "5", "4", "3", "3", "2", "2", "-1", "-1", "7"]);
    }

    [SkippableFact]
    public async Task WhileUniformConditionBreak_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/WhileUniformConditionBreak.cs",
            ["Kernels.WhileUniformConditionBreakKernel"],
            [
                "0", "1", "202", "603", "1204", "2005", "3006", "4207", "5608", "0", "1",
                "202"
            ]);
    }

    [SkippableFact]
    public async Task NestedLoopInnerBreak_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/NestedLoopInnerBreak.cs",
            ["Kernels.NestedLoopInnerBreakKernel"],
            ["4", "14", "30", "52", "80", "80", "80", "80", "80", "4", "14", "30"]);
    }

    [SkippableFact]
    public async Task NestedLoopGotoOut_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/NestedLoopGotoOut.cs",
            ["Kernels.NestedLoopGotoOutKernel"],
            ["6", "7", "8", "9", "13", "13", "14", "14", "18", "6", "7", "8"]);
    }

    [SkippableFact]
    public async Task NestedLoopGotoTargets_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/NestedLoopGotoTargets.cs",
            ["Kernels.NestedLoopGotoTargetsKernel"],
            [
                "21236", "21457", "28423", "14473", "14133", "28231", "27898", "7216",
                "7170", "28377", "68", "-167"
            ]);
    }

    [SkippableFact]
    public async Task LoopSwitchSelector_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/LoopSwitchSelector.cs",
            ["Kernels.LoopSwitchSelectorKernel"],
            [
                "132", "141", "231", "222", "132", "141", "231", "222", "132", "132",
                "141", "231"
            ]);
    }

    [SkippableFact]
    public async Task SwitchGotoCase_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/SwitchGotoCase.cs",
            ["Kernels.SwitchGotoCaseKernel"],
            [
                "100", "12", "7", "101", "100", "100", "100", "100", "100", "100", "12",
                "7"
            ]);
    }

    [SkippableFact]
    public async Task DoWhileValueAfterLoop_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/DoWhileValueAfterLoop.cs",
            ["Kernels.DoWhileValueAfterLoopKernel"],
            [
                "1", "1002", "4003", "9004", "16005", "25006", "36007", "49008", "64009",
                "1", "1002", "4003"
            ]);
    }

    [SkippableFact]
    public async Task DoWhileContinue_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/DoWhileContinue.cs",
            ["Kernels.DoWhileContinueKernel"],
            ["1", "2", "4", "6", "9", "12", "16", "20", "25", "1", "2", "4"]);
    }

    [SkippableFact]
    public async Task DoWhileBreak_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/DoWhileBreak.cs",
            ["Kernels.DoWhileBreakKernel"],
            [
                "3608", "3608", "3608", "2107", "1506", "1005", "604", "303", "303",
                "3608", "3608", "3608"
            ]);
    }

    [SkippableFact]
    public async Task LoopConditionalAccumulate_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/LoopConditionalAccumulate.cs",
            ["Kernels.LoopConditionalAccumulateKernel"],
            ["12", "16", "12", "16", "12", "16", "12", "16", "12", "12", "16", "12"]);
    }

    [SkippableFact]
    public async Task WhileCollatz_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/WhileCollatz.cs",
            ["Kernels.WhileCollatzKernel"],
            ["0", "1", "7", "2", "5", "8", "16", "3", "19", "0", "1", "7"]);
    }

    [SkippableFact]
    public async Task NestedLoopVariableTrip_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/NestedLoopVariableTrip.cs",
            ["Kernels.NestedLoopVariableTripKernel"],
            ["0", "0", "1", "4", "10", "20", "35", "56", "84", "0", "0", "1"]);
    }

    [SkippableFact]
    public async Task HelperLoopEarlyReturn_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/HelperLoopEarlyReturn.cs",
            ["Kernels.HelperLoopEarlyReturnKernel"],
            ["-1", "-1", "21", "15", "12", "9", "9", "6", "6", "-1", "-1", "21"]);
    }

    [SkippableFact]
    public async Task LoopPhiSwap_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/LoopPhiSwap.cs",
            ["Kernels.LoopPhiSwapKernel"],
            [
                "1", "1001", "1002", "2003", "3005", "5008", "8013", "13021", "21034",
                "1", "1001", "1002"
            ]);
    }

    [SkippableFact]
    public async Task NestedLoopContinueOuter_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/NestedLoopContinueOuter.cs",
            ["Kernels.NestedLoopContinueOuterKernel"],
            [
                "10001", "10004", "10010", "10120", "10229", "10336", "10440", "10440",
                "10440", "10001", "10004", "10010"
            ]);
    }

    [SkippableFact]
    public async Task IrreducibleLoop_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/IrreducibleLoop.cs",
            ["Kernels.IrreducibleLoopKernel"],
            ["1", "3", "7", "8", "14", "30", "29", "43", "57", "1", "3", "7"]);
    }

    /// <summary>
    /// Irreducible loops whose branches are constant per instantiation: folding
    /// them must keep the remaining blocks and their values intact.
    /// </summary>
    [SkippableFact]
    public async Task IrreducibleConstantBranches_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/IrreducibleConstantBranches.cs",
            ["Kernels.IntKernel", "Kernels.LongKernel", "Kernels.ShortKernel"],
            [
                "2006", "4008", "6010",
                "126000", "126002", "190004",
                "90", "2058", "4074",
            ]);
    }

    [SkippableFact]
    public async Task IrreducibleLoopEntryValues_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/IrreducibleLoopEntryValues.cs",
            ["Kernels.IrreducibleLoopEntryValuesKernel"],
            [
                "1107", "561", "1227", "1401", "1367", "1401", "2167", "2601", "2167",
                "1107", "561", "1227"
            ]);
    }

    [SkippableFact]
    public async Task IrreducibleLoopInOuterLoop_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/IrreducibleLoopInOuterLoop.cs",
            ["Kernels.IrreducibleLoopInOuterLoopKernel"],
            [
                "86", "213", "422", "529", "934", "1725", "1788", "2600", "3481", "86",
                "213", "422"
            ]);
    }

    [SkippableFact]
    public async Task IrreducibleGotoInKernel_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/IrreducibleGotoInKernel.cs",
            ["Kernels.IrreducibleGotoInKernelKernel"],
            [
                "5114", "701", "5112", "703", "2804", "6412", "6614", "7512", "2801",
                "7009", "9112", "9612"
            ]);
    }

    [SkippableFact]
    public async Task LoopSwitchContinueView_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/LoopSwitchContinueView.cs",
            ["Kernels.LoopSwitchContinueViewKernel"],
            [
                "458", "634", "977", "980", "807", "957", "745", "400", "401", "402",
                "802", "36"
            ]);
    }

    [SkippableFact]
    public async Task FixedBufferAfterRegion_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/FixedBufferAfterRegion.cs",
            ["Kernels.FixedBufferAfterRegionKernel"],
            [
                "100", "302", "303", "104", "304", "310", "103", "304", "308", "118",
                "303", "304"
            ]);
    }

    [SkippableFact]
    public async Task IrreducibleLoopFixedBuffer_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/IrreducibleLoopFixedBuffer.cs",
            ["Kernels.IrreducibleLoopFixedBufferKernel"],
            [
                "11", "43", "125", "117", "239", "411", "493", "585", "837", "139",
                "31", "163"
            ]);
    }

    [SkippableFact]
    public async Task SwitchSharedCases_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/SwitchSharedCases.cs",
            ["Kernels.SwitchSharedCasesKernel"],
            [
                "11", "102", "115", "106", "129", "110", "112", "-26", "116", "11",
                "102", "115"
            ]);
    }

    [SkippableFact]
    public async Task LoopSearchBreakValue_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/LoopSearchBreakValue.cs",
            ["Kernels.LoopSearchBreakValueKernel"],
            [
                "60", "821", "112", "33", "164", "85", "216", "77", "268", "60", "821",
                "112"
            ]);
    }

    [SkippableFact]
    public async Task HelperLoopTwoReturns_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/HelperLoopTwoReturns.cs",
            ["Kernels.HelperLoopTwoReturnsKernel"],
            [
                "-3", "-5", "-7", "3", "1104", "405", "1004", "1007", "908", "-3", "-5",
                "-7"
            ]);
    }

    [SkippableFact]
    public async Task SwitchContinueLatchCopies_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/SwitchContinueLatchCopies.cs",
            ["Kernels.SwitchContinueLatchCopiesKernel"],
            [
                "45076", "87115", "1056", "-36992", "45012", "12059", "7029", "50031",
                "73033", "45076", "87115", "1056"
            ]);
    }

    [SkippableFact]
    public async Task TripleLoopGotoStore_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/TripleLoopGotoStore.cs",
            ["Kernels.TripleLoopGotoStoreKernel"],
            [
                "60", "201", "352", "493", "594", "705", "826", "957", "1098", "60",
                "201", "352"
            ]);
    }

    [SkippableFact]
    public async Task ShortCircuitTernaryChain_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/ShortCircuitTernaryChain.cs",
            ["Kernels.ShortCircuitTernaryChainKernel"],
            [
                "100", "101", "102", "103", "20", "105", "30", "107", "40", "100", "101",
                "102"
            ]);
    }

    [SkippableFact]
    public async Task MergeWithBarrier_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/MergeWithBarrier.cs",
            ["Kernels.MergeWithBarrierKernel"],
            [
                "6065", "65006", "11003", "3011", "16008", "8016", "21007", "7021",
                "26006", "6026", "65011", "11065"
            ]);
    }

    [SkippableFact]
    public async Task MergeWithShuffle_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/MergeWithShuffle.cs",
            ["Kernels.MergeWithShuffleKernel"],
            [
                "6065", "65006", "11003", "3011", "16008", "8016", "21007", "7021",
                "26006", "6026", "65011", "11065"
            ]);
    }

    [SkippableFact]
    public async Task EarlyReturnLoopWithBarrier_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/EarlyReturnLoopWithBarrier.cs",
            ["Kernels.EarlyReturnLoopWithBarrierKernel"],
            [
                "123", "123", "233", "233", "273", "273", "243", "313", "193", "123",
                "143", "143", "-26", "-26", "-35", "-35", "-44", "-44", "-39", "-46",
                "-33", "-26", "-27", "-27"
            ]);
    }

    [SkippableFact]
    public async Task GotoCaseNoCopies_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentControlFlow/GotoCaseNoCopies.cs",
            ["Kernels.GotoCaseNoCopiesKernel"],
            [
                "30", "3", "998", "1003", "42", "15", "1002", "1007", "54", "30", "3",
                "998"
            ]);
    }
}
