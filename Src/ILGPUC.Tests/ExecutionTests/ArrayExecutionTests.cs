// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class ArrayExecutionTests : ExecutionTestBase
{
    protected ArrayExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task SimpleArray_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Array/SimpleArray.cs",
            ["Kernels.SimpleArrayKernel"],
            ["3", "5", "7", "9"]);
    }

    [SkippableFact]
    public async Task ArrayBounds_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Array/ArrayBounds.cs",
            ["Kernels.ArrayBoundsKernel"],
            ["0", "1", "-1", "-1"]);
    }
}
