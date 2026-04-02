// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicIfExecutionTests.cs
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

public abstract class BasicIfExecutionTests : ExecutionTestBase
{
    protected BasicIfExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task IfTrue_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicIf/IfTrue.cs",
            ["Kernels.IfTrueKernel"],
            ["42", "42", "42", "42"]);
    }

    [SkippableFact]
    public async Task IfFalse_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicIf/IfFalse.cs",
            ["Kernels.IfFalseKernel"],
            ["23", "23", "23", "23"]);
    }

    [SkippableFact]
    public async Task IfSideEffects_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicIf/IfSideEffects.cs",
            ["Kernels.IfSideEffectsKernel"],
            ["0", "0", "1", "1"]);
    }

    [SkippableFact]
    public async Task IfAndOr_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicIf/IfAndOr.cs",
            ["Kernels.IfAndOrKernel"],
            ["1", "1", "1", "1"]);
    }

    [SkippableFact]
    public async Task NestedIf_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicIf/NestedIf.cs",
            ["Kernels.NestedIfKernel"],
            ["3", "3", "3", "3"]);
    }

    [SkippableFact]
    public async Task IfChain_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicIf/IfChain.cs",
            ["Kernels.IfChainKernel"],
            ["30", "30", "30", "30"]);
    }
}
