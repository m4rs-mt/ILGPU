// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FixedBufferExecutionTests.cs
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

public abstract class FixedBufferExecutionTests : ExecutionTestBase
{
    protected FixedBufferExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task FixedWrite_ProducesCorrectOutput()
    {
        // value=5: Data=[5,10,15,20], sum=50
        await VerifyProgramOutputAsync(
            "TestPrograms/FixedBuffers/FixedWrite.cs",
            ["Kernels.FixedWriteKernel", "ReadKernels.SumKernel"],
            ["50", "50", "50", "50"]);
    }
}
