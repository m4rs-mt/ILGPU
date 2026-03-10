// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpReduceExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class WarpReduceExecutionTests : ExecutionTestBase
{
    protected WarpReduceExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task WarpAllReduceAdd_ProducesCorrectOutput()
    {
        // 4 threads, each contributes index+1, all-reduce sum = 10
        await VerifyProgramOutputAsync(
            "TestPrograms/WarpReduce/WarpAllReduceAdd.cs",
            ["Kernels.WarpAllReduceAddKernel"],
            ["10", "10", "10", "10"]);
    }
}
