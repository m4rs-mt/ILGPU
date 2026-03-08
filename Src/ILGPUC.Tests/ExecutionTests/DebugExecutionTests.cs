// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: DebugExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class DebugExecutionTests : ExecutionTestBase
{
    protected DebugExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task AssertTrue_ProducesCorrectOutput()
    {
        // Assert(true) should not crash; writes 42 to all elements
        await VerifyProgramOutputAsync(
            "TestPrograms/Debug/AssertTrue.cs",
            ["Kernels.AssertTrueKernel"],
            ["42", "42", "42", "42"]);
    }

    [SkippableFact]
    public async Task AssertTrue_Release_ProducesCorrectOutput()
    {
        // In Release mode assertions are stripped; same correct output
        var props = new CompilationProperties().WithMode(CompilationMode.Release);
        await VerifyProgramOutputAsync(
            "TestPrograms/Debug/AssertTrue.cs",
            ["Kernels.AssertTrueKernel"],
            ["42", "42", "42", "42"],
            props);
    }

    [SkippableFact]
    public async Task AssertCondition_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Debug/AssertCondition.cs",
            ["Kernels.AssertConditionKernel"],
            ["1", "2", "3", "4"]);
    }
}
