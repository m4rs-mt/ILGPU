// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
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

    /// <summary>
    /// Launch lambdas pass lambda expressions to a kernel method taking a Func,
    /// which are lifted as inline kernels that bind the kernel method to them.
    /// </summary>
    [SkippableFact]
    public async Task InlineDelegateArgument_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/InlineDelegateArgument.cs",
            [],
            ["9", "8", "7", "6"]);
    }

    /// <summary>
    /// Launch lambdas whose single call passes values computed from the lambda
    /// parameter (index.GroupIndex, i * 3) are lifted as inline kernels, so
    /// these values are computed inside the kernel.
    /// </summary>
    [SkippableFact]
    public async Task LambdaParameterArgument_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/LambdaParameterArgument.cs",
            [],
            ["0 1 2 3 10 11 12 13", "0 3 6 9"]);
    }

    /// <summary>
    /// A launch lambda passes a delegate parameter of the launching method to a
    /// kernel method taking a Func (issue #463). One kernel is compiled for
    /// each lambda and static method its callers in two files pass, and a
    /// delegate passed through reflection is rejected at the launch.
    /// </summary>
    [SkippableFact]
    public async Task DelegateParameter_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            [
                "TestPrograms/Lambda/DelegateParameter.cs",
                "TestPrograms/Lambda/DelegateParameterCallers.cs",
            ],
            [],
            ["-1 -2 -3 -4", "0 1 2 3", "0 2 4 6", "NotSupportedException"]);
    }

    /// <summary>
    /// An inline kernel invokes a delegate parameter of the launching method,
    /// which callers pass an anonymous method and a static lambda to.
    /// </summary>
    [SkippableFact]
    public async Task InlineDelegateParameter_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/InlineDelegateParameter.cs",
            [],
            ["13 16 19 22", "11 14 17 20"]);
    }

    /// <summary>
    /// A captured delegate parameter is forwarded through a recursive chain of
    /// host methods, each of which is called with its own lambda.
    /// </summary>
    [SkippableFact]
    public async Task ForwardedDelegateParameter_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/ForwardedDelegateParameter.cs",
            [],
            ["6 7 8 9", "60 70 80 90", "59 69 79 89"]);
    }

    /// <summary>
    /// The same static method passed from two call sites shares its kernel,
    /// and two captured delegate parameters are bound to each combination of
    /// their targets.
    /// </summary>
    [SkippableFact]
    public async Task SharedDelegateTarget_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Lambda/SharedDelegateTarget.cs",
            [],
            ["2 4 6 8", "4 8 12 16", "1 5 9 13", "1 9 17 25", "3 7 11 15"]);
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
