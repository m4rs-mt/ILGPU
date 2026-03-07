// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayViewExecutionTests.cs
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class ArrayViewExecutionTests : ExecutionTestBase
{
    protected ArrayViewExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task LoadStore_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/LoadStore.cs",
            ["Kernels.LoadStoreKernel"],
            ["10", "20", "30", "40"]);
    }

    [SkippableFact]
    public async Task Length_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/Length.cs",
            ["Kernels.LengthKernel"],
            ["8", "8", "8", "8"]);
    }

    [SkippableFact]
    public async Task IsValid_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/IsValid.cs",
            ["Kernels.IsValidKernel"],
            ["1", "1", "1", "1"]);
    }

    [SkippableFact]
    public async Task SubView_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/SubView.cs",
            ["Kernels.SubViewKernel"],
            ["30", "40", "-1", "-1"]);
    }
}
