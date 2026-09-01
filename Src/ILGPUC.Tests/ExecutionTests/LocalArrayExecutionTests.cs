// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalArrayExecutionTests.cs
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

    /// <summary>
    /// The scratch buffer must be private per thread. The other programs in this
    /// family write thread-independent values, so a backend that shares one buffer
    /// across threads (or across the lanes of a vectorized CPU group) still yields
    /// the right answer; this one does not.
    /// </summary>
    [SkippableFact]
    public async Task ThreadVaryingScratch_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/ThreadVaryingScratch.cs",
            ["Kernels.ThreadVaryingScratchKernel"],
            ["0", "11", "22", "33"]);
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

    [SkippableFact]
    public async Task AllocateInLoop_ProducesCorrectOutput()
    {
        // A fresh zeroed array per iteration: the elements sum up to i + 3
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/AllocateInLoop.cs",
            ["Kernels.AllocateInLoopKernel"],
            ["1271", "1271", "1271", "1271"]);
    }

    [SkippableFact]
    public async Task AllocateInHelperInLoop_ProducesCorrectOutput()
    {
        // A fresh zeroed array per helper call, also when inlined into a loop
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/AllocateInHelperInLoop.cs",
            ["Kernels.AllocateInHelperInLoopKernel"],
            ["1271", "1271", "1271", "1271"]);
    }
}
