// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ReinterpretCastExecutionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

using static ILGPUC.Tests.Framework.BackendCapability;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class ReinterpretCastExecutionTests : ExecutionTestBase
{
    protected ReinterpretCastExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task FloatToUInt32_ProducesCorrectOutput()
    {
        // 1.0f = 0x3F800000 = 1065353216
        await VerifyProgramOutputAsync(
            "TestPrograms/ReinterpretCast/FloatToUInt32.cs",
            ["Kernels.FloatToUInt32Kernel"],
            ["1065353216", "1065353216", "1065353216", "1065353216"]);
    }

    [SkippableFact]
    public async Task UInt32ToFloat_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ReinterpretCast/UInt32ToFloat.cs",
            ["Kernels.UInt32ToFloatKernel"],
            ["1", "1", "1", "1"]);
    }

    [SkippableFact]
    public async Task DoubleToUInt64_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/ReinterpretCast/DoubleToUInt64.cs",
            ["Kernels.DoubleToUInt64Kernel"],
            ["4607182418800017408", "4607182418800017408",
             "4607182418800017408", "4607182418800017408"]);
    }

    [SkippableFact]
    public async Task UInt64ToDouble_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/ReinterpretCast/UInt64ToDouble.cs",
            ["Kernels.UInt64ToDoubleKernel"],
            ["1", "1", "1", "1"]);
    }
}
