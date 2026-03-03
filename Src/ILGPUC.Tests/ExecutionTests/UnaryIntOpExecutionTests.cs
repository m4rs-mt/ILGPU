// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: UnaryIntOpExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class UnaryIntOpExecutionTests : ExecutionTestBase
{
    protected UnaryIntOpExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task Neg_ProducesCorrectOutput()
    {
        // Input: [1,2,3,4] → [-1,-2,-3,-4]
        await VerifyProgramOutputAsync(
            "TestPrograms/UnaryIntOp/Neg.cs",
            ["Kernels.NegKernel"],
            ["-1", "-2", "-3", "-4"]);
    }

    [SkippableFact]
    public async Task Not_ProducesCorrectOutput()
    {
        // Input: [0,1,2,3] → ~[0,1,2,3] = [-1,-2,-3,-4]
        await VerifyProgramOutputAsync(
            "TestPrograms/UnaryIntOp/Not.cs",
            ["Kernels.NotKernel"],
            ["-1", "-2", "-3", "-4"]);
    }

    [SkippableFact]
    public async Task AbsInt_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/UnaryIntOp/AbsInt.cs",
            ["Kernels.AbsKernel"],
            ["3", "1", "0", "5"]);
    }

    [SkippableFact]
    public async Task AbsLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/UnaryIntOp/AbsLong.cs",
            ["Kernels.AbsKernel"],
            ["3", "1", "0", "5"]);
    }

    [SkippableFact]
    public async Task NegLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/UnaryIntOp/NegLong.cs",
            ["Kernels.NegKernel"],
            ["-1", "-2", "-3", "-4"]);
    }

    [SkippableFact]
    public async Task NotLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/UnaryIntOp/NotLong.cs",
            ["Kernels.NotKernel"],
            ["-1", "-2", "-3", "-4"]);
    }
}
