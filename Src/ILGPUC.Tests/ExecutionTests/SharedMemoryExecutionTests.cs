// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemoryExecutionTests.cs
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

public abstract class SharedMemoryExecutionTests : ExecutionTestBase
{
    protected SharedMemoryExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task SharedVariable_ProducesCorrectOutput()
    {
        // shared[groupIdx] = groupIdx * 2; data[index] = shared[groupIdx]
        // With 4 threads in one group: [0, 2, 4, 6]
        await VerifyProgramOutputAsync(
            "TestPrograms/SharedMemory/SharedVariable.cs",
            ["Kernels.SharedMemoryVariableKernel"],
            ["0", "2", "4", "6"]);
    }

    /// <summary>
    /// Shared memory, group barriers and warp indices in helper methods that
    /// are not inlined, including a helper that only calls such a helper.
    /// </summary>
    [SkippableFact]
    public async Task SharedMemoryHelpers_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/SharedMemory/SharedMemoryHelpers.cs",
            ["Kernels.SharedMemoryHelpersKernel"],
            ["neighbour mismatches 0", "warp mismatches 0"]);
    }

    /// <summary>
    /// A shared memory buffer returned by an inlined helper is the same buffer in
    /// the kernel and in a helper that is not inlined.
    /// </summary>
    [SkippableFact]
    public async Task SharedBufferAcrossCall_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/SharedMemory/SharedBufferAcrossCall.cs",
            ["Kernels.SharedBufferAcrossCallKernel"],
            ["neighbour mismatches 0"]);
    }

    /// <summary>
    /// A shared memory call site yields one buffer per kernel, also in a helper
    /// that two calls inline, and in two kernels that inline the same helper, in
    /// groups of several warps.
    /// </summary>
    [SkippableFact]
    public async Task SharedBufferInlinedTwice_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/SharedMemory/SharedBufferInlinedTwice.cs",
            [
                "Kernels.SharedTwiceKernel",
                "Kernels.SharedOtherKernel",
            ],
            ["shared twice mismatches 0", "other kernel mismatches 0"]);
    }

    [SkippableFact]
    public async Task SharedMemoryArray_ProducesOutput()
    {
        var result = await RunProgramAsync(
            "TestPrograms/SharedMemory/SharedMemoryArray.cs",
            ["Kernels.SharedMemoryArrayKernel"]);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut, "Process timed out");
        Assert.Equal(4, result.StdOutLines.Length);
    }

    [SkippableFact]
    public async Task SharedRefSelectSameArray_ProducesCorrectOutput()
    {
        // Even threads increment shared[t] = 10 * (t + 1), odd threads
        // shared[t + 32] = t; then data[t] = shared[t] + shared[t + 32]
        await VerifyProgramOutputAsync(
            "TestPrograms/SharedMemory/SharedRefSelectSameArray.cs",
            ["Kernels.SharedRefSelectSameArrayKernel"],
            ["11", "22", "33", "44"]);
    }

    /// <summary>
    /// Shared memory with one element per thread, several elements per thread
    /// and one element per warp of the group, for several group sizes.
    /// </summary>
    [SkippableFact]
    public async Task SharedMemoryPerThread_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/SharedMemory/SharedMemoryPerThread.cs",
            ["Kernels.PerThreadKernel"],
            [
                "per thread mismatches 0",
                "multiplier mismatches 0",
                "per warp mismatches 0",
            ]);
    }
}
