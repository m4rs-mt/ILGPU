// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LambdaExecutionTests.cs
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

public abstract class LambdaExecutionTests : ExecutionTestBase
{
    protected LambdaExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task SimpleCapture_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/SimpleCapture.cs",
            ["Kernels.SimpleCaptureKernel"],
            ["10", "11", "12", "13"]);
    }

    [SkippableFact]
    public async Task MultiCapture_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/MultiCapture.cs",
            ["Kernels.MultiCaptureKernel"],
            ["2", "5", "8", "11"]);
    }

    [SkippableFact]
    public async Task LambdaConditional_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/LambdaConditional.cs",
            ["Kernels.LambdaConditionalKernel"],
            ["0", "1", "2", "2"]);
    }

    [SkippableFact]
    public async Task LambdaAppliedTwice_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/LambdaAppliedTwice.cs",
            ["Kernels.LambdaAppliedTwiceKernel"],
            ["11", "13", "15", "17"]);
    }

    [SkippableFact]
    public async Task AccumulateClosure_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/AccumulateClosure.cs",
            ["Kernels.AccumulateClosureKernel"],
            ["1", "3", "5", "7"]);
    }

    [SkippableFact]
    public async Task ClosureReadAfterWrite_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/ClosureReadAfterWrite.cs",
            ["Kernels.ClosureReadAfterWriteKernel"],
            ["0", "2", "4", "6"]);
    }
}
