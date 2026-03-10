// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpReduceRuntimeExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

/// <summary>
/// Warp reduce/scan execution tests with runtime-parameterized inputs.
/// Inputs are read from an <c>ArrayView</c> so the optimizer cannot fold
/// the reduction into a compile-time constant.
/// </summary>
public abstract class WarpReduceRuntimeExecutionTests : ExecutionTestBase
{
    protected WarpReduceRuntimeExecutionTests(
        ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task WarpAllReduceAdd_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/WarpReduceRuntime/WarpAllReduceAddRuntime.cs",
            ["Kernels.WarpAllReduceAddRuntimeKernel"],
            ["10", "10", "10", "10"]);
    }

    [SkippableFact]
    public async Task WarpAllReduceMax_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/WarpReduceRuntime/WarpAllReduceMaxRuntime.cs",
            ["Kernels.WarpAllReduceMaxRuntimeKernel"],
            ["4", "4", "4", "4"]);
    }

    [SkippableFact]
    public async Task WarpReduceAdd_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/WarpReduceRuntime/WarpReduceAddRuntime.cs",
            ["Kernels.WarpReduceAddRuntimeKernel"],
            ["10", "-1", "-1", "-1"]);
    }

    [SkippableFact]
    public async Task WarpInclusiveScanAdd_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/WarpReduceRuntime/WarpInclusiveScanAddRuntime.cs",
            ["Kernels.WarpInclusiveScanAddRuntimeKernel"],
            ["1", "3", "6", "10"]);
    }

    [SkippableFact]
    public async Task WarpExclusiveScanAdd_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/WarpReduceRuntime/WarpExclusiveScanAddRuntime.cs",
            ["Kernels.WarpExclusiveScanAddRuntimeKernel"],
            ["0", "1", "3", "6"]);
    }
}
