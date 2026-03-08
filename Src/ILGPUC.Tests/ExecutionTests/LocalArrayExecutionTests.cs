// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalArrayExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class LocalArrayExecutionTests : ExecutionTestBase
{
    protected LocalArrayExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task WriteRead_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/WriteRead.cs",
            ["Kernels.WriteReadKernel"],
            ["10", "20", "30", "40"]);
    }

    [SkippableFact]
    public async Task Length_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/Length.cs",
            ["Kernels.LengthKernel"],
            ["5", "5", "5", "5"]);
    }

    [SkippableFact]
    public async Task Accumulate_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/Accumulate.cs",
            ["Kernels.AccumulateKernel"],
            ["6", "6", "6", "6"]);
    }

    [SkippableFact]
    public async Task IndexCompute_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/IndexCompute.cs",
            ["Kernels.IndexComputeKernel"],
            ["100", "200", "300", "400"]);
    }

    [SkippableFact]
    public async Task FloatType_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/FloatType.cs",
            ["Kernels.FloatArrayKernel"],
            ["1.5", "2.5", "3.5", "4.5"]);
    }
}
