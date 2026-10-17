// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicPhiExecutionTests.cs
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

public abstract class BasicPhiExecutionTests : ExecutionTestBase
{
    protected BasicPhiExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task PhiInlining_ProducesCorrectOutput()
    {
        // a=10, b=3: a > b → result = 10-3 = 7
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicPhi/PhiInlining.cs",
            ["Kernels.PhiInliningKernel"],
            ["7", "7", "7", "7"]);
    }

    [SkippableFact]
    public async Task DeepPhi_ProducesCorrectOutput()
    {
        // value=250: >100, >200, !>300 → result = 3
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicPhi/DeepPhi.cs",
            ["Kernels.DeepPhiKernel"],
            ["3", "3", "3", "3"]);
    }

    [SkippableFact]
    public async Task DeepPhi_O0_ProducesCorrectOutput()
    {
        // At O0, the view is accessed through a local copy whose address
        // flows through a phi of all four branches
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicPhi/DeepPhi.cs",
            ["Kernels.DeepPhiKernel"],
            ["3", "3", "3", "3"],
            new CompilationProperties { OptimizationLevel = OptimizationLevel.O0 });
    }

    [SkippableFact]
    public async Task IntSwapLoop_ProducesCorrectOutput()
    {
        // input = [1, 2, 3, 4]; a and b swap each iteration, thread t sums a
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicPhi/IntSwapLoop.cs",
            ["Kernels.IntSwapLoopKernel"],
            ["1", "3", "4", "6"]);
    }

    [SkippableFact]
    public async Task IntRotateLoop_ProducesCorrectOutput()
    {
        // input = [1, 2, 3, 4]; a, b and c rotate each iteration, thread t sums a
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicPhi/IntRotateLoop.cs",
            ["Kernels.IntRotateLoopKernel"],
            ["1", "3", "6", "7"]);
    }

    [SkippableFact]
    public async Task LoopCarriedLoadInHeader_ProducesCorrectOutput()
    {
        // input = [1, 2, 3, 4, 5, 6]; thread t sums (input[i] + 10)^2 for i <= t + 1
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicPhi/LoopCarriedLoadInHeader.cs",
            ["Kernels.LoopCarriedLoadInHeaderKernel"],
            ["265", "434", "630", "855"]);
    }

    [SkippableFact]
    public async Task DoWhileRotateLoad_ProducesCorrectOutput()
    {
        // input = [1, 2, 3, 4, 5, 6]; thread t rotates a, b, c for t + 3 iterations
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicPhi/DoWhileRotateLoad.cs",
            ["Kernels.DoWhileRotateLoadKernel"],
            ["20406", "40606", "60609", "60912"]);
    }

    [SkippableFact]
    public async Task ViewSwapLoop_ProducesCorrectOutput()
    {
        // data[i] = (7 * i) % 11 - 5; thread t swaps the two halves of data
        // in t + 2 iterations, once with ArrayView1D and once with ArrayView
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicPhi/ViewSwapLoop.cs",
            ["Kernels.ViewSwapLoopKernel", "Kernels.ContiguousViewSwapLoopKernel"],
            ["-17", "16", "-103", "443", "-17", "16", "-103", "443"]);
    }

    [SkippableFact]
    public async Task ViewShrinkLoop_ProducesCorrectOutput()
    {
        // data[i] = 3 * i + 1; thread t sums data[0..t + 2) by dropping the
        // first element of a view in every iteration
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicPhi/ViewShrinkLoop.cs",
            ["Kernels.ViewShrinkLoopKernel"],
            ["5", "12", "22", "35"]);
    }
}
