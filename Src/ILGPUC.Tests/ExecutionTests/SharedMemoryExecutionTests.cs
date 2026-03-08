// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemoryExecutionTests.cs
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
}
