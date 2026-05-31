// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DivergentMemoryExecutionTests.cs
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

public abstract class DivergentMemoryExecutionTests : ExecutionTestBase
{
    protected DivergentMemoryExecutionTests(
        ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    /// <summary>
    /// Skips tests that the GPU backends do not translate correctly yet.
    /// </summary>
    private void SkipOnGpuBackends(string reason) =>
        Skip.If(Backend != BackendType.CPU, reason);

    [SkippableFact]
    public async Task LocalArrayPerLane_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentMemory/LocalArrayPerLane.cs",
            ["Kernels.LocalArrayPerLaneKernel"],
            [
                "0", "401", "1004", "1809", "2816", "25", "636", "1449", "2400", "0",
                "401", "1004"
            ]);
    }

    [SkippableFact]
    public async Task LocalArrayDivergentWrite_ProducesCorrectOutput()
    {
        SkipOnGpuBackends(
            "Local arrays written in divergent branches are not yet supported");
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentMemory/LocalArrayDivergentWrite.cs",
            ["Kernels.LocalArrayDivergentWriteKernel"],
            [
                "30", "300", "3000", "3", "30", "70", "700", "7000", "7", "30", "300",
                "3000"
            ]);
    }

    [SkippableFact]
    public async Task ScalarIndexStore_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/DivergentMemory/ScalarIndexStore.cs",
            ["Kernels.ScalarIndexStoreKernel"],
            ["51", "0", "14", "0"]);
    }
}
