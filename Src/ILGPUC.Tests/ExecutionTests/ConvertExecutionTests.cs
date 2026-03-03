// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConvertExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

using static ILGPUC.Tests.Framework.BackendCapability;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class ConvertExecutionTests : ExecutionTestBase
{
    protected ConvertExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task Truncate_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/Truncate.cs",
            ["Kernels.TruncateKernel"],
            ["42", "42", "42", "42"]);
    }

    [SkippableFact]
    public async Task FloatToInt_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/FloatToInt.cs",
            ["Kernels.FloatToIntKernel"],
            ["3", "3", "3", "3"]);
    }

    [SkippableFact]
    public async Task Promote_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/Promote.cs",
            ["Kernels.PromoteKernel"],
            ["42", "42", "42", "42"]);
    }

    [SkippableFact]
    public async Task IntToFloat_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/IntToFloat.cs",
            ["Kernels.IntToFloatKernel"],
            ["7", "7", "7", "7"]);
    }

    [SkippableFact]
    public async Task FloatToDouble_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/FloatToDouble.cs",
            ["Kernels.FloatToDoubleKernel"],
            ["3", "3", "3", "3"]);
    }

    [SkippableFact]
    public async Task DoubleToFloat_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/DoubleToFloat.cs",
            ["Kernels.DoubleToFloatKernel"],
            ["2", "2", "2", "2"]);
    }

    [SkippableFact]
    public async Task ByteToInt_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/ByteToInt.cs",
            ["Kernels.ByteToIntKernel"],
            ["200", "200", "200", "200"]);
    }

    [SkippableFact]
    public async Task IntToByte_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/IntToByte.cs",
            ["Kernels.IntToByteKernel"],
            ["44", "44", "44", "44"]);
    }

    [SkippableFact]
    public async Task SignExtend_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/SignExtend.cs",
            ["Kernels.SignExtendKernel"],
            ["-10", "-10", "-10", "-10"]);
    }
}
