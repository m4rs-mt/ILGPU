// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class AtomicExecutionTests : ExecutionTestBase
{
    protected AtomicExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task AtomicAdd_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/AtomicAdd.cs",
            ["Kernels.AtomicAddKernel"],
            ["4"]);
    }

    [SkippableFact]
    public async Task AtomicMax_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/AtomicMax.cs",
            ["Kernels.AtomicMaxKernel"],
            ["99"]);
    }

    [SkippableFact]
    public async Task AtomicMin_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/AtomicMin.cs",
            ["Kernels.AtomicMinKernel"],
            ["5"]);
    }

    [SkippableFact]
    public async Task AtomicExchange_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/AtomicExchange.cs",
            ["Kernels.AtomicExchangeKernel"],
            ["42"]);
    }
}
