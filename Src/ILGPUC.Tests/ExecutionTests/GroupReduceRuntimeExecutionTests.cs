// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupReduceRuntimeExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

/// <summary>
/// Group reduce execution tests with runtime-parameterized inputs.
/// Inputs are read from an <c>ArrayView</c> so the optimizer cannot fold
/// the reduction into a compile-time constant.
/// </summary>
public abstract class GroupReduceRuntimeExecutionTests : ExecutionTestBase
{
    protected GroupReduceRuntimeExecutionTests(
        ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task GroupAllReduceAdd_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GroupReduceRuntime/GroupAllReduceAddRuntime.cs",
            ["Kernels.GroupAllReduceAddRuntimeKernel"],
            ["10", "10", "10", "10"]);
    }

    [SkippableFact]
    public async Task GroupAllReduceMax_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GroupReduceRuntime/GroupAllReduceMaxRuntime.cs",
            ["Kernels.GroupAllReduceMaxRuntimeKernel"],
            ["4", "4", "4", "4"]);
    }
}
