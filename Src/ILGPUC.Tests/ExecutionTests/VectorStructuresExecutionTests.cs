// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: VectorStructuresExecutionTests.cs
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

public abstract class VectorStructuresExecutionTests : ExecutionTestBase
{
    protected VectorStructuresExecutionTests(
        ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    /// <summary>
    /// Skips tests that the GPU backends do not translate correctly yet.
    /// </summary>
    private void SkipOnGpuBackends(string reason) =>
        Skip.If(Backend != BackendType.CPU, reason);

    [SkippableFact]
    public async Task StructDivergentFieldUpdate_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorStructures/StructDivergentFieldUpdate.cs",
            ["Kernels.StructDivergentFieldUpdateKernel"],
            [
                "-10", "16", "42", "68", "109", "135", "161", "187", "213", "-10", "16",
                "42"
            ]);
    }

    [SkippableFact]
    public async Task TupleSelect_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorStructures/TupleSelect.cs",
            ["Kernels.TupleSelectKernel"],
            ["20", "23", "26", "31", "41", "51", "61", "71", "81", "20", "23", "26"]);
    }

    [SkippableFact]
    public async Task StructFromBranchingHelper_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorStructures/StructFromBranchingHelper.cs",
            ["Kernels.StructFromBranchingHelperKernel"],
            ["5", "4", "3", "5", "6", "7", "8", "9", "10", "5", "4", "3"]);
    }

    [SkippableFact]
    public async Task NestedStructInLoop_ProducesCorrectOutput()
    {
        SkipOnGpuBackends("Struct updates in loops with breaks are not yet supported");
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorStructures/NestedStructInLoop.cs",
            ["Kernels.NestedStructInLoopKernel"],
            [
                "0", "101", "503", "1306", "2610", "3011", "2209", "2510", "2811", "0",
                "101", "503"
            ]);
    }
}
