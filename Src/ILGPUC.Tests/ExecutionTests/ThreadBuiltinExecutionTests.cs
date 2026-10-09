// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ThreadBuiltinExecutionTests.cs
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

public abstract class ThreadBuiltinExecutionTests : ExecutionTestBase
{
    protected ThreadBuiltinExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task AllBuiltins_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ThreadBuiltin/AllBuiltins.cs",
            ["Kernels.AllBuiltinsKernel"],
            ["0", "1", "2", "3"]);
    }
}
