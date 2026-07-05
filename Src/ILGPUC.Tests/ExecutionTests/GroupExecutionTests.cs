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

    /// <summary>
    /// Group and warp predicate barriers, warp barriers and scoped memory
    /// fences.
    /// </summary>
    [SkippableFact]
    public async Task PredicateBarriers_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.PredicateBarriers);
        await VerifyProgramOutputAsync(
            "TestPrograms/Group/PredicateBarriers.cs",
            ["Kernels.PredicateBarriersKernel"],
            ["301301", "301301", "301301", "301301"]);
    }

    /// <summary>
    /// Loops bounded by the group index, which GPU backends read from
    /// unsigned built-ins: one with two exit conditions, one starting at a
    /// negative value.
    /// </summary>
    [SkippableFact]
    public async Task GroupIdxLoopBound_ProducesCorrectOutput()
    {
        // first[t] = sum of (i + 1) for i in 0..t,
        // second[t] = sum of (i + 3) for i in -2..t
        await VerifyProgramOutputAsync(
            "TestPrograms/Group/GroupIdxLoopBound.cs",
            [
                "Kernels.GroupIdxLoopBoundKernel",
                "Kernels.GroupIdxNegativeStartKernel"
            ],
            ["1", "3", "6", "10", "6", "10", "15", "21"]);
    }

    /// <summary>
    /// Group and warp predicate barriers over many groups with divergent and
    /// uniform predicates, including a partially filled last group.
    /// </summary>
    [SkippableFact]
    public async Task PredicateBarrierGroups_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.PredicateBarriers);
        await VerifyProgramOutputAsync(
            "TestPrograms/Group/PredicateBarrierGroups.cs",
            ["Kernels.PredicateBarrierGroupsKernel"],
            [
                "group mismatches 0", "warp mismatches 0", "uniform mismatches 0",
                "partial True", "groups True",
            ]);
    }

    /// <summary>
    /// Group and warp predicate barriers in helper methods that are not inlined,
    /// with per-thread and uniform predicates.
    /// </summary>
    [SkippableFact]
    public async Task PredicateBarrierHelper_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.PredicateBarriers);
        await VerifyProgramOutputAsync(
            "TestPrograms/Group/PredicateBarrierHelper.cs",
            ["Kernels.PredicateBarrierHelperKernel"],
            [
                "group mismatches 0", "warp mismatches 0", "uniform mismatches 0",
                "partial True", "groups True",
            ]);
    }

    /// <summary>
    /// Group predicate barriers, shared memory and launch geometry in
    /// explicitly grouped launches with and without a KernelIndex, including
    /// a group smaller than a warp and a group larger than the device allows.
    /// </summary>
    [SkippableFact]
    public async Task GroupedLaunchBarriers_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.PredicateBarriers);
        await VerifyProgramOutputAsync(
            "TestPrograms/Group/GroupedLaunchBarriers.cs",
            ["Kernels.GroupedBarriersKernel", "Kernels.KernelIndexBarriersKernel"],
            [
                "grouped mismatches 0", "kernel index mismatches 0",
                "grouped mismatches 0", "kernel index mismatches 0",
                "oversized rejected True",
            ]);
    }

    /// <summary>
    /// Predicate barriers that only some threads of a group reach. Barriers in
    /// divergent control flow are undefined on the GPU backends; the CPU
    /// backend combines the predicate across the lanes that reach them.
    /// </summary>
    [SkippableFact]
    public async Task PredicateBarrierInactiveLanes_ProducesCorrectOutput()
    {
        Skip.If(
            Backend != BackendType.CPU,
            "Barriers in divergent control flow are undefined on GPUs");
        await VerifyProgramOutputAsync(
            "TestPrograms/Group/PredicateBarrierInactiveLanes.cs",
            ["Kernels.PredicateBarrierInactiveLanesKernel"],
            ["mismatches 0"]);
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
