// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueTupleExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class ValueTupleExecutionTests : ExecutionTestBase
{
    protected ValueTupleExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task TupleCreate_ProducesCorrectOutput()
    {
        // index 0: int=0, float=0; index 1: int=2, float=3;
        // index 2: int=4, float=6; index 3: int=6, float=9
        await VerifyProgramOutputAsync(
            "TestPrograms/ValueTuple/TupleCreate.cs",
            ["Kernels.TupleCreateKernel"],
            ["0", "0", "2", "3", "4", "6", "6", "9"]);
    }

    [SkippableFact]
    public async Task TuplePass_ProducesCorrectOutput()
    {
        // CreateTuple(i, (float)i) = (i+1, i+1.0f), interleaved
        await VerifyProgramOutputAsync(
            "TestPrograms/ValueTuple/TuplePass.cs",
            ["Kernels.TuplePassKernel"],
            ["1", "1", "2", "2", "3", "3", "4", "4"]);
    }
}
