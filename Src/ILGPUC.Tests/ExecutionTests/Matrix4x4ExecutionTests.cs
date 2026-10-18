// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Matrix4x4ExecutionTests.cs
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
/// End-to-end tests for <see cref="System.Numerics.Matrix4x4.Decompose"/>
/// and <see cref="ILGPU.Numerics.Matrix4x4Extensions.TryDecomposeIterative"/>.
/// </summary>
public abstract class Matrix4x4ExecutionTests : ExecutionTestBase
{
    protected Matrix4x4ExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task Matrix4x4Decompose_AlignedTRS_ProducesCorrectOutput()
    {
        await VerifyProgramOutputWithToleranceAsync(
            "TestPrograms/Matrix4x4/Matrix4x4Decompose.cs",
            ["Kernels.DecomposeKernel"],
            [
                "2.00", "3.00", "4.00",
                "0.00", "0.00", "0.00", "1.00",
                "10.00", "20.00", "30.00",
            ]);
    }

    [SkippableFact]
    public async Task Matrix4x4DecomposeIterative_AlignedTRS_ProducesCorrectOutput()
    {
        await VerifyProgramOutputWithToleranceAsync(
            "TestPrograms/Matrix4x4/Matrix4x4DecomposeIterative.cs",
            ["Kernels.DecomposeIterativeKernel"],
            [
                "2.00", "3.00", "4.00",
                "0.00", "0.00", "0.00", "1.00",
                "10.00", "20.00", "30.00",
            ]);
    }
}
