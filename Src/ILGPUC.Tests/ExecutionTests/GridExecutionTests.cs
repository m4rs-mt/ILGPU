// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GridExecutionTests.cs
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

public abstract class GridExecutionTests : ExecutionTestBase
{
    protected GridExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task GridDimension_ProducesOutput()
    {
        // Grid.Dimension value depends on CPU runtime grouping strategy.
        // With LoadAutoGroupedStreamKernel(4, ...) on CPU, typically 1 grid block.
        var result = await RunProgramAsync(
            "TestPrograms/Grid/GridDimension.cs",
            ["Kernels.GridDimensionKernel"]);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut, "Process timed out");
        Assert.Equal(4, result.StdOutLines.Length);
    }

    [SkippableFact]
    public async Task GridIndex_ProducesOutput()
    {
        var result = await RunProgramAsync(
            "TestPrograms/Grid/GridIndex.cs",
            ["Kernels.GridIndexKernel"]);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut, "Process timed out");
        Assert.Equal(4, result.StdOutLines.Length);
    }
}
