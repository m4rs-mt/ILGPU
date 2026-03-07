// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlineLambdaExecutionTests.cs
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

public abstract class InlineLambdaExecutionTests : ExecutionTestBase
{
    protected InlineLambdaExecutionTests(
        ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task InlineCaptureView_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/InlineCaptureView.cs",
            [],
            ["0", "1", "2", "3"]);
    }

    [SkippableFact]
    public async Task InlineCaptureViewAndScalar_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/InlineCaptureViewAndScalar.cs",
            [],
            ["10", "20", "30", "40"]);
    }

    [SkippableFact]
    public async Task InlineCaptureReadWrite_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/InlineCaptureReadWrite.cs",
            [],
            ["10", "21", "32", "43"]);
    }

    [SkippableFact]
    public async Task InlineCaptureMultipleScalars_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/InlineCaptureMultipleScalars.cs",
            [],
            ["7", "10", "13", "16"]);
    }

    [SkippableFact]
    public async Task InlineConditional_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/InlineConditional.cs",
            [],
            ["0", "1", "2", "2"]);
    }

    [SkippableFact]
    public async Task InlineExpressionBody_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/InlineExpressionBody.cs",
            [],
            ["100", "101", "102", "103"]);
    }

    [SkippableFact]
    public async Task InlineMultiStatement_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/InlineMultiStatement.cs",
            [],
            ["7", "9", "11", "13"]);
    }

    [SkippableFact]
    public async Task InlineCaptureBuffer_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/InlineCaptureBuffer.cs",
            [],
            ["0", "1", "2", "3"]);
    }

    [SkippableFact]
    public async Task InlineCaptureBufferReadWrite_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/InlineCaptureBufferReadWrite.cs",
            [],
            ["2", "4", "6", "8"]);
    }

    [SkippableFact]
    public async Task InlineWithInnerClosure_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/InlineWithInnerClosure.cs",
            [],
            ["1", "3", "5", "7"]);
    }
}
