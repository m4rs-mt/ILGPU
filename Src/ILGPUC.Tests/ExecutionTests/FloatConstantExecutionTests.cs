// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FloatConstantExecutionTests.cs
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

/// <summary>
/// Float constants must be emitted in round-trip form. A fixed-point format
/// truncates constants below the sixth decimal place to zero and rounds away
/// every digit beyond it.
/// </summary>
public abstract class FloatConstantExecutionTests : ExecutionTestBase
{
    protected FloatConstantExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task SmallFloatConstant_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatConstant/SmallFloatConstant.cs",
            ["Kernels.SmallFloatConstantKernel"],
            ["1", "1", "1", "1"]);
    }

    [SkippableFact]
    public async Task PreciseFloatConstant_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatConstant/PreciseFloatConstant.cs",
            ["Kernels.PreciseFloatConstantKernel"],
            ["1.2345678", "1.2345678", "1.2345678", "1.2345678"]);
    }

    [SkippableFact]
    public async Task PreciseDoubleConstant_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatConstant/PreciseDoubleConstant.cs",
            ["Kernels.PreciseDoubleConstantKernel"],
            [
                "3.141592653589793", "3.141592653589793",
                "3.141592653589793", "3.141592653589793",
            ]);
    }
}
