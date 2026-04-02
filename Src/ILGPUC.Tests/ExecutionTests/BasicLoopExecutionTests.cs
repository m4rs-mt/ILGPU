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
}
