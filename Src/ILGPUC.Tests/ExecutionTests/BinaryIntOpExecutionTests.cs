// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: BinaryIntOpExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class BinaryIntOpExecutionTests : ExecutionTestBase
{
    protected BinaryIntOpExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task AddInt_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/AddInt.cs",
            ["Kernels.AddKernel"],
            ["11", "22", "33", "44"]);
    }

    [SkippableFact]
    public async Task MulInt_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/MulInt.cs",
            ["Kernels.MulKernel"],
            ["20", "30", "40", "50"]);
    }

    [SkippableFact]
    public async Task SubInt_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/SubInt.cs",
            ["Kernels.SubKernel"],
            ["9", "18", "27", "36"]);
    }

    [SkippableFact]
    public async Task SubLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/SubLong.cs",
            ["Kernels.SubKernel"],
            ["9", "18", "27", "36"]);
    }

    [SkippableFact]
    public async Task DivInt_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/DivInt.cs",
            ["Kernels.DivKernel"],
            ["5", "5", "6", "5"]);
    }

    [SkippableFact]
    public async Task DivLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/DivLong.cs",
            ["Kernels.DivKernel"],
            ["5", "5", "6", "5"]);
    }

    [SkippableFact]
    public async Task AndInt_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/AndInt.cs",
            ["Kernels.AndKernel"],
            ["15", "0", "15", "0"]);
    }

    [SkippableFact]
    public async Task AndLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/AndLong.cs",
            ["Kernels.AndKernel"],
            ["15", "0", "15", "0"]);
    }

    [SkippableFact]
    public async Task OrInt_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/OrInt.cs",
            ["Kernels.OrKernel"],
            ["255", "255", "255", "255"]);
    }

    [SkippableFact]
    public async Task OrLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/OrLong.cs",
            ["Kernels.OrKernel"],
            ["255", "255", "255", "255"]);
    }

    [SkippableFact]
    public async Task XorInt_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/XorInt.cs",
            ["Kernels.XorKernel"],
            ["240", "255", "0", "0"]);
    }

    [SkippableFact]
    public async Task XorLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/XorLong.cs",
            ["Kernels.XorKernel"],
            ["240", "255", "0", "0"]);
    }

    [SkippableFact]
    public async Task AddLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/AddLong.cs",
            ["Kernels.AddKernel"],
            ["11", "22", "33", "44"]);
    }

    [SkippableFact]
    public async Task MulLong_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BinaryIntOp/MulLong.cs",
            ["Kernels.MulKernel"],
            ["20", "30", "40", "50"]);
    }
}
