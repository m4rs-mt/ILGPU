// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicJumpExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class BasicJumpExecutionTests : ExecutionTestBase
{
    protected BasicJumpExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task Goto_NoSkip_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicJump/Goto.cs",
            ["Kernels.GotoKernel"],
            ["6", "6", "6", "6"]);
    }

    [SkippableFact]
    public async Task Goto_Skip_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicJump/GotoSkip.cs",
            ["Kernels.GotoKernel"],
            ["0", "0", "0", "0"]);
    }

    [SkippableFact]
    public async Task NestedLabel_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicJump/NestedLabel.cs",
            ["Kernels.NestedLabelKernel"],
            ["100", "100", "100", "100"]);
    }
}
