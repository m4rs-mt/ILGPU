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
}
