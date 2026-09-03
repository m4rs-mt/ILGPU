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

    /// <summary>
    /// A local function capturing a parameter and a local of the kernel, which
    /// the C# compiler passes through a closure struct with auto layout.
    /// </summary>
    [SkippableFact]
    public async Task LocalFunctionCapture_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/LocalFunctionCapture.cs",
            ["Kernels.LocalFunctionCaptureKernel"],
            ["5", "15", "25", "35"]);
    }

    [SkippableFact]
    public async Task MultiCapture_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/MultiCapture.cs",
            ["Kernels.MultiCaptureKernel"],
            ["2", "5", "8", "11"]);
    }

    /// <summary>
    /// Two different lambdas whose delegate handles cross a block boundary. Each
    /// call site must devirtualize to its own lambda body.
    /// </summary>
    [SkippableFact]
    public async Task TwoLambdasCrossBlock_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/TwoLambdasCrossBlock.cs",
            ["Kernels.TwoLambdasCrossBlockKernel"],
            ["1", "4", "7", "10"]);
    }

    /// <summary>
    /// Two closures whose captured values are read from blocks other than the entry
    /// block. Regression guard — this shape produced 0 for both captures on
    /// every backend, and no execution test covered it (single-capture closures never
    /// reached the offending pass, and the single-block MultiCapture shape read before
    /// the misplaced allocas).
    /// </summary>
    [SkippableFact]
    public async Task TwoClosuresCrossBlock_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/TwoClosuresCrossBlock.cs",
            ["Kernels.TwoClosuresCrossBlockKernel"],
            ["29", "14", "18", "22"]);
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

    [SkippableFact]
    public async Task SelectedDelegate_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/SelectedDelegate.cs",
            ["Kernels.SelectedDelegateKernel"],
            ["0", "2", "4", "6"]);
    }

    [SkippableFact]
    public async Task ThreeCapturesLoop_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/ThreeCapturesLoop.cs",
            ["Kernels.ThreeCapturesLoopKernel"],
            ["-1", "-1", "0", "2"]);
    }
}
