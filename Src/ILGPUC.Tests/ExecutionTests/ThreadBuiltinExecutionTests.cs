// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ThreadBuiltinExecutionTests.cs
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
