// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicMovementExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class BasicMovementExecutionTests : ExecutionTestBase
{
    protected BasicMovementExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task Copy_ProducesCorrectOutput()
    {
        // source: [10,20,30,40] → target: [10,20,30,40]
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicMovement/Copy.cs",
            ["Kernels.CopyKernel"],
            ["10", "20", "30", "40"]);
    }

    [SkippableFact]
    public async Task BarrierOrdering_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicMovement/BarrierOrdering.cs",
            ["Kernels.BarrierOrderingKernel"],
            ["1", "3", "5", "7"]);
    }
}
