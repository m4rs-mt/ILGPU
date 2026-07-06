// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vector3ExecutionTests.cs
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
/// End-to-end test of a kernel that calls
/// <c>System.Numerics.Vector3.Transform(Vector3, Matrix4x4)</c>.
/// </summary>
public abstract class Vector3ExecutionTests : ExecutionTestBase
{
    protected Vector3ExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task Vector3Transform_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Vector3/Vector3Transform.cs",
            ["Kernels.Vector3TransformKernel"],
            ["11", "22", "33", "0", "0", "0", "14", "25", "36"]);
    }
}
