// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayViewExecutionTests.cs
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

    [SkippableFact]
    public async Task SubViewSelect_ProducesCorrectOutput()
    {
        // data[i] = 3 * i + 1; thread t selects data[0..32) (even t) or
        // data[32..48) (odd t)
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/SubViewSelect.cs",
            [
                "Kernels.SelectKernel",
                "Kernels.IfElseKernel",
                "Kernels.HelperKernel",
                "Kernels.AtomicKernel",
                "Kernels.ContiguousKernel",
            ],
            [
                "132", "10016", "732", "10616",
                "32001", "16100", "32007", "16106",
                "33", "116", "39", "122",
                "1", "3", "0", "0", "2", "4", "0", "0",
                "132", "732", "10016", "10616",
            ]);
    }

    [SkippableFact]
    public async Task PerLaneViewHelper_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/PerLaneViewHelper.cs",
            ["Kernels.PerLaneViewHelperKernel"],
            ["3 3 11 7", "3 2 3 4 11 6 7 8"]);
    }

    [SkippableFact]
    public async Task PerLaneViewReturn_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/PerLaneViewReturn.cs",
            ["Kernels.PerLaneViewReturnKernel"],
            [
                "0 24 3 27 6 30 9 33",
                "0 3 6 9 100 102 104 106 24 27 30 33 101 103 105 107"
            ]);
    }

    [SkippableFact]
    public async Task PerLaneLocalArrays_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/PerLaneLocalArrays.cs",
            ["Kernels.PerLaneLocalArraysKernel"],
            ["5 28 27 50 49 72 71 94"]);
    }

    [SkippableFact]
    public async Task PerLaneSubViews_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/PerLaneSubViews.cs",
            ["Kernels.PerLaneSubViewsKernel"],
            [
                "0 24 3 27 6 30 9 33",
                "0 3 6 9 100 102 104 106 24 27 30 33 101 103 105 107"
            ]);
    }

    [SkippableFact]
    public async Task PerLaneViewParameters_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/PerLaneViewParameters.cs",
            ["Kernels.PerLaneViewParametersKernel"],
            [
                "0 24 3 27 6 30 9 33",
                "0 3 6 9 100 102 104 106 24 27 30 33 101 103 105 107"
            ]);
    }

    [SkippableFact]
    public async Task PerLaneViewLength_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/PerLaneViewLength.cs",
            ["Kernels.PerLaneViewLengthKernel"],
            ["3303 5508 3303 5508"]);
    }

    [SkippableFact]
    public async Task PerLaneViewAtomic_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/PerLaneViewAtomic.cs",
            ["Kernels.PerLaneViewAtomicKernel"],
            ["0 16 0 20"]);
    }

    [SkippableFact]
    public async Task PerLaneViewLoop_ProducesCorrectOutput()
    {
        Skip.If(
            Backend != BackendType.CPU,
            "GPU backends read stale lengths of views that are shrunk in a " +
            "loop which the lanes leave in different iterations.");
        await VerifyProgramOutputAsync(
            "TestPrograms/ArrayView/PerLaneViewLoop.cs",
            ["Kernels.PerLaneViewLoopKernel"],
            ["801 702 603 504 801 702 603 504"]);
    }
}
