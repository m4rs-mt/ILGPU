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
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorStructures/NestedStructInLoop.cs",
            ["Kernels.NestedStructInLoopKernel"],
            [
                "0", "101", "503", "1306", "2610", "3011", "2209", "2510", "2811", "0",
                "101", "503"
            ]);
    }

    /// <summary>
    /// A vector passed to a helper inlined in a loop keeps its fields for a helper
    /// after the loop, and IsZero compares every element with zero.
    /// </summary>
    [SkippableFact]
    public async Task VectorFieldsAfterInlinedCalls_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/VectorStructures/VectorFieldsAfterInlinedCalls.cs",
            ["Kernels.VectorFieldsAfterInlinedCallsKernel"],
            [
                "0", "40", "127", "5", "80", "45", "9", "120", "45", "13", "160", "45",
                "0", "0", "0", "0", "-0", "-0", "-0", "-0", "10", "12", "14", "16",
                "-5", "-6", "-7", "-8", "18", "20", "22", "24", "-9", "-10", "-11",
                "-12", "26", "28", "30", "32", "-13", "-14", "-15", "-16"
            ]);
    }
}
