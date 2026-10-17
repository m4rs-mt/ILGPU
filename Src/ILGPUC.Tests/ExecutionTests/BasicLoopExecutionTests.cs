// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicLoopExecutionTests.cs
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

public abstract class BasicLoopExecutionTests : ExecutionTestBase
{
    protected BasicLoopExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task WhileLoop_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/WhileLoop.cs",
            ["Kernels.WhileLoopKernel"],
            ["45", "45", "45", "45"]);
    }

    [SkippableFact]
    public async Task ForLoop_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/ForLoop.cs",
            ["Kernels.ForLoopKernel"],
            ["15", "15", "15", "15"]);
    }

    [SkippableFact]
    public async Task DoWhileLoop_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/DoWhileLoop.cs",
            ["Kernels.DoWhileLoopKernel"],
            ["5", "5", "5", "5"]);
    }

    [SkippableFact]
    public async Task NestedLoop_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/NestedLoop.cs",
            ["Kernels.NestedLoopKernel"],
            ["120", "120", "120", "120"]);
    }

    [SkippableFact]
    public async Task DivergentLoop_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/DivergentLoop.cs",
            ["Kernels.DivergentLoopKernel"],
            ["0", "1", "3", "6"]);
    }

    [SkippableFact]
    public async Task LoopWithBreak_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/LoopWithBreak.cs",
            ["Kernels.LoopWithBreakKernel"],
            ["15", "15", "15", "15"]);
    }

    [SkippableFact]
    public async Task LoopWithContinue_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/LoopWithContinue.cs",
            ["Kernels.LoopWithContinueKernel"],
            ["25", "25", "25", "25"]);
    }

    [SkippableFact]
    public async Task DivergentBreak_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/DivergentBreak.cs",
            ["Kernels.DivergentBreakKernel"],
            ["1", "6", "10", "10", "15", "21", "21", "28"]);
    }

    [SkippableFact]
    public async Task CountedReduction_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/CountedReduction.cs",
            ["Kernels.CountedReductionKernel"],
            [
                "74973", "75091", "74973", "75023", "74973", "75091", "74973", "75023",
                "74973", "75091", "74973", "75023"
            ]);
    }

    [SkippableFact]
    public async Task ConstantNest_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/ConstantNest.cs",
            ["Kernels.ConstantNestKernel"],
            [
                "224", "2240", "4256", "6272", "8288", "10304", "12320", "14336",
                "16352", "224", "2240", "4256"
            ]);
    }

    [SkippableFact]
    public async Task DoWhileDigits_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/DoWhileDigits.cs",
            ["Kernels.DoWhileDigitsKernel"],
            [
                "701", "1305", "2805", "1605", "3105", "1905", "2505", "2205", "3705",
                "701", "1305", "2805"
            ]);
    }

    [SkippableFact]
    public async Task WhileLoadSearch_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/WhileLoadSearch.cs",
            ["Kernels.WhileLoadSearchKernel"],
            ["0", "14", "28", "42", "56", "70", "84", "98", "11", "25", "39", "53"]);
    }

    [SkippableFact]
    public async Task WhileExitRecompute_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/WhileExitRecompute.cs",
            ["Kernels.WhileExitRecomputeKernel"],
            [
                "11003", "8012", "5026", "2045", "30069", "15013", "12027", "9046",
                "6070", "3099", "19023", "16042"
            ]);
    }

    [SkippableFact]
    public async Task WhileExitReload_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicLoop/WhileExitReload.cs",
            ["Kernels.WhileExitReloadKernel"],
            [
                "21130", "29301", "37542", "4853", "13234", "21135", "29306", "37547",
                "4858", "13239", "21140", "29311"
            ]);
    }
}
