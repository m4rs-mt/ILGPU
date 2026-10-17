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

    /// <summary>
    /// A warp barrier that only the first warp of each group reaches, while the
    /// other warps wait for it to pass.
    /// </summary>
    [SkippableFact]
    public async Task WarpBarrierDivergent_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Warp/WarpBarrierDivergent.cs",
            ["Kernels.WarpBarrierDivergentKernel"],
            ["17", "16", "15", "14", "13", "12", "11", "10", "0"]);
    }

    /// <summary>
    /// The warps of a group take a WarpSpinLock in turn without blocking the
    /// warp that holds it.
    /// </summary>
    [SkippableFact]
    public async Task WarpSpinLockGroups_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Warp/WarpSpinLockGroups.cs",
            ["Kernels.WarpSpinLockGroupsKernel"],
            ["lanes 128", "threads 128"]);
    }

    /// <summary>
    /// Shuffles, broadcasts, reductions and scans of values taken directly from
    /// the thread indices, which differ per thread.
    /// </summary>
    [SkippableFact]
    public async Task WarpShuffleKinds_ProducesCorrectOutput()
    {
        // Groups wider than a warp cannot synchronize on the CPU backend
        var broadcast = Backend == BackendType.CPU
            ? "warp index broadcast skipped"
            : "warp index broadcast mismatches 0";
        await VerifyProgramOutputAsync(
            "TestPrograms/Warp/WarpShuffleKinds.cs",
            [
                "Kernels.WarpShuffleKindsKernel",
                "Kernels.IndexShuffleKernel",
                "Kernels.WarpIndexBroadcastKernel",
            ],
            ["warp mismatches 0", "index mismatches 0", broadcast]);
    }

    /// <summary>
    /// Warp shuffles of 64-bit integers.
    /// </summary>
    [SkippableFact]
    public async Task WarpShuffleLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Warp/WarpShuffleLong.cs",
            ["Kernels.WarpShuffleLongKernel"],
            ["8589934603", "12884901900", "17179869197", "4294967306"]);
    }
}
