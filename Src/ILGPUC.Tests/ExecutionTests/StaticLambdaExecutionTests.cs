// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StaticLambdaExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class StaticLambdaExecutionTests : ExecutionTestBase
{
    protected StaticLambdaExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task StaticLambdaAdd_ProducesCorrectOutput()
    {
        // add(index, 1) = index + 1 → [1, 2, 3, 4]
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/StaticLambdaAdd.cs",
            ["Kernels.StaticLambdaAddKernel"],
            ["1", "2", "3", "4"]);
    }
}
