// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MemoryBufferExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class MemoryBufferExecutionTests : ExecutionTestBase
{
    protected MemoryBufferExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task Copy_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/MemoryBuffer/Copy.cs",
            ["Kernels.CopyKernel"],
            ["100", "200", "300", "400"]);
    }

    [SkippableFact]
    public async Task Scale_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/MemoryBuffer/Scale.cs",
            ["Kernels.ScaleKernel"],
            ["10", "20", "30", "40"]);
    }
}
