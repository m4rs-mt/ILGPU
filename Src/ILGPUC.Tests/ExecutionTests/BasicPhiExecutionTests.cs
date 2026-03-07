// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicPhiExecutionTests.cs
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
}
