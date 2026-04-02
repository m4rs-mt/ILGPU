// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompareExecutionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using static ILGPUC.Tests.Framework.BackendCapability;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class CompareExecutionTests : ExecutionTestBase
{
    protected CompareExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task CompareIntLessThan_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareInt/LessThan.cs",
            ["Kernels.LessThanKernel"],
            ["1", "0", "1", "1"]);
    }

    [SkippableFact]
    public async Task CompareFloatLessThan_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareFloat/LessThanFloat.cs",
            ["Kernels.LessThanKernel"],
            ["1", "0", "1", "1"]);
    }

    // --- CompareInt: remaining operators ---

    [SkippableFact]
    public async Task CompareIntLessEqual_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareInt/LessEqual.cs",
            ["Kernels.LessEqualKernel"],
            ["1", "0", "1", "1"]);
    }

    [SkippableFact]
    public async Task CompareIntGreaterThan_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareInt/GreaterThan.cs",
            ["Kernels.GreaterThanKernel"],
            ["1", "0", "1", "0"]);
    }

    [SkippableFact]
    public async Task CompareIntGreaterEqual_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareInt/GreaterEqual.cs",
            ["Kernels.GreaterEqualKernel"],
            ["1", "1", "1", "1"]);
    }

    [SkippableFact]
    public async Task CompareIntEqual_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareInt/Equal.cs",
            ["Kernels.EqualKernel"],
            ["1", "0", "1", "0"]);
    }

    [SkippableFact]
    public async Task CompareIntNotEqual_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareInt/NotEqual.cs",
            ["Kernels.NotEqualKernel"],
            ["0", "1", "0", "1"]);
    }

    // --- CompareInt: long variants ---

    [SkippableFact]
    public async Task CompareIntLessThanLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareInt/LessThanLong.cs",
            ["Kernels.LessThanKernel"],
            ["1", "0", "1", "1"]);
    }

    [SkippableFact]
    public async Task CompareIntLessEqualLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareInt/LessEqualLong.cs",
            ["Kernels.LessEqualKernel"],
            ["1", "0", "1", "1"]);
    }

    [SkippableFact]
    public async Task CompareIntGreaterThanLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareInt/GreaterThanLong.cs",
            ["Kernels.GreaterThanKernel"],
            ["1", "0", "1", "0"]);
    }

    [SkippableFact]
    public async Task CompareIntGreaterEqualLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareInt/GreaterEqualLong.cs",
            ["Kernels.GreaterEqualKernel"],
            ["1", "1", "1", "1"]);
    }

    [SkippableFact]
    public async Task CompareIntEqualLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareInt/EqualLong.cs",
            ["Kernels.EqualKernel"],
            ["1", "0", "1", "0"]);
    }

    [SkippableFact]
    public async Task CompareIntNotEqualLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareInt/NotEqualLong.cs",
            ["Kernels.NotEqualKernel"],
            ["0", "1", "0", "1"]);
    }

    // --- CompareFloat: remaining float operators ---

    [SkippableFact]
    public async Task CompareFloatLessEqual_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareFloat/LessEqualFloat.cs",
            ["Kernels.LessEqualKernel"],
            ["1", "0", "1", "1"]);
    }

    [SkippableFact]
    public async Task CompareFloatGreaterThan_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareFloat/GreaterThanFloat.cs",
            ["Kernels.GreaterThanKernel"],
            ["1", "0", "1", "0"]);
    }

    [SkippableFact]
    public async Task CompareFloatGreaterEqual_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareFloat/GreaterEqualFloat.cs",
            ["Kernels.GreaterEqualKernel"],
            ["1", "1", "1", "1"]);
    }

    [SkippableFact]
    public async Task CompareFloatEqual_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareFloat/EqualFloat.cs",
            ["Kernels.EqualKernel"],
            ["1", "0", "1", "0"]);
    }

    [SkippableFact]
    public async Task CompareFloatNotEqual_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareFloat/NotEqualFloat.cs",
            ["Kernels.NotEqualKernel"],
            ["0", "1", "0", "1"]);
    }

    // --- CompareFloat: double variants ---

    [SkippableFact]
    public async Task CompareDoubleLessThan_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareFloat/LessThanDouble.cs",
            ["Kernels.LessThanKernel"],
            ["1", "0", "1", "1"]);
    }

    [SkippableFact]
    public async Task CompareDoubleLessEqual_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareFloat/LessEqualDouble.cs",
            ["Kernels.LessEqualKernel"],
            ["1", "0", "1", "1"]);
    }

    [SkippableFact]
    public async Task CompareDoubleGreaterThan_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareFloat/GreaterThanDouble.cs",
            ["Kernels.GreaterThanKernel"],
            ["1", "0", "1", "0"]);
    }

    [SkippableFact]
    public async Task CompareDoubleGreaterEqual_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareFloat/GreaterEqualDouble.cs",
            ["Kernels.GreaterEqualKernel"],
            ["1", "1", "1", "1"]);
    }

    [SkippableFact]
    public async Task CompareDoubleEqual_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareFloat/EqualDouble.cs",
            ["Kernels.EqualKernel"],
            ["1", "0", "1", "0"]);
    }

    [SkippableFact]
    public async Task CompareDoubleNotEqual_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/CompareFloat/NotEqualDouble.cs",
            ["Kernels.NotEqualKernel"],
            ["0", "1", "0", "1"]);
    }
}
