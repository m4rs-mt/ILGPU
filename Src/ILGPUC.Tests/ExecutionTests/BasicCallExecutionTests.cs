// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicCallExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class BasicCallExecutionTests : ExecutionTestBase
{
    protected BasicCallExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task SimpleCall_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/SimpleCall.cs",
            ["Kernels.SimpleCallKernel"],
            ["0", "2", "4", "6"]);
    }

    [SkippableFact]
    public async Task NestedCall_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/NestedCall.cs",
            ["Kernels.NestedCallKernel"],
            ["20", "20", "20", "20"]);
    }

    [SkippableFact]
    public async Task CallWithOut_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/CallWithOut.cs",
            ["Kernels.CallWithOutKernel"],
            ["30", "30", "30", "30"]);
    }

    [SkippableFact]
    public async Task CallWithRef_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/CallWithRef.cs",
            ["Kernels.CallWithRefKernel"],
            ["14", "14", "14", "14"]);
    }

    [SkippableFact]
    public async Task ChainCall_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/ChainCall.cs",
            ["Kernels.ChainCallKernel"],
            ["22", "22", "22", "22"]);
    }
}
