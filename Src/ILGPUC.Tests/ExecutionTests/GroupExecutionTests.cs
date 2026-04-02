// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupExecutionTests.cs
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

public abstract class GroupExecutionTests : ExecutionTestBase
{
    protected GroupExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task GroupBarrier_ProducesCorrectOutput()
    {
        // data[i] = i, barrier, data[i] = data[i] + 1 → [1,2,3,4]
        await VerifyProgramOutputAsync(
            "TestPrograms/Group/GroupBarrier.cs",
            ["Kernels.GroupBarrierKernel"],
            ["1", "2", "3", "4"]);
    }

    [SkippableFact]
    public async Task GroupDimension_ProducesOutput()
    {
        var result = await RunProgramAsync(
            "TestPrograms/Group/GroupDimension.cs",
            ["Kernels.GroupDimensionKernel"]);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut, "Process timed out");
        Assert.Equal(4, result.StdOutLines.Length);
    }

    [SkippableFact]
    public async Task GroupIdx_ProducesOutput()
    {
        var result = await RunProgramAsync(
            "TestPrograms/Group/GroupIdx.cs",
            ["Kernels.GroupIdxKernel"]);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut, "Process timed out");
        Assert.Equal(4, result.StdOutLines.Length);
    }
}
