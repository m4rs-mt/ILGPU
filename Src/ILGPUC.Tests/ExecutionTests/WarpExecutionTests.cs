// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpExecutionTests.cs
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

public abstract class WarpExecutionTests : ExecutionTestBase
{
    protected WarpExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task WarpBarrier_ProducesCorrectOutput()
    {
        // data[i] = i, warp barrier, data[i] = data[i] + 1 → [1,2,3,4]
        await VerifyProgramOutputAsync(
            "TestPrograms/Warp/WarpBarrier.cs",
            ["Kernels.WarpBarrierKernel"],
            ["1", "2", "3", "4"]);
    }

    [SkippableFact]
    public async Task WarpSize_ProducesOutput()
    {
        var result = await RunProgramAsync(
            "TestPrograms/Warp/WarpSize.cs",
            ["Kernels.WarpSizeKernel"]);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut, "Process timed out");
        Assert.Equal(4, result.StdOutLines.Length);
    }

    [SkippableFact]
    public async Task WarpLaneIdx_ProducesOutput()
    {
        var result = await RunProgramAsync(
            "TestPrograms/Warp/WarpLaneIdx.cs",
            ["Kernels.WarpLaneIdxKernel"]);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut, "Process timed out");
        Assert.Equal(4, result.StdOutLines.Length);
    }
}
