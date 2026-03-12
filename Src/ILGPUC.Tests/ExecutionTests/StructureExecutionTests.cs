// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructureExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class StructureExecutionTests : ExecutionTestBase
{
    protected StructureExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task StructFieldAccess_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/StructFieldAccess.cs",
            ["Kernels.StructFieldAccessKernel"],
            ["142", "142", "142", "142"]);
    }

    [SkippableFact]
    public async Task StructPassByValue_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/StructPassByValue.cs",
            ["Kernels.StructPassByValueKernel"],
            ["1253", "1253", "1253", "1253"]);
    }

    /// <summary>
    /// Tests local struct construction with per-field writes.
    /// Exercises LoadFieldAddress on local alloca — the vectorized LFA
    /// path on CPU. GPU backends handle this through the shared code generator.
    /// </summary>
    [SkippableFact]
    public async Task StructLocalBuild_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/StructLocalBuild.cs",
            ["Kernels.StructLocalBuildKernel"],
            ["10", "10", "10", "10"]);
    }
}
