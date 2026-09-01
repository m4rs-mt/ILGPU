// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LaunchGeometryExecutionTests.cs
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
/// Pins how launches map onto grids and groups: explicitly grouped launches
/// with and without a KernelIndex parameter, and implicitly grouped launches
/// whose extents are not multiples of the group size.
/// </summary>
public abstract class LaunchGeometryExecutionTests : ExecutionTestBase
{
    protected LaunchGeometryExecutionTests(
        ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task GroupedImplicitIndex_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LaunchGeometry/GroupedImplicitIndex.cs",
            ["Kernels.GroupedImplicitIndexKernel"],
            [
                "config 4x64", "grid 4 group 64", "mismatches 0",
                "config 8x256", "grid 8 group 256", "mismatches 0",
            ]);
    }

    /// <summary>
    /// Grouped kernels without an index parameter whose first parameter has
    /// the type of an index keep it as a kernel argument.
    /// </summary>
    [SkippableFact]
    public async Task GroupedScalarFirstParameter_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LaunchGeometry/GroupedScalarFirstParameter.cs",
            [
                "Kernels.IntFirstKernel",
                "Kernels.StructFirstKernel",
                "Kernels.WideStructFirstKernel",
                "Kernels.TripleStructFirstKernel",
                "Kernels.LongStructFirstKernel",
                "Kernels.LongTripleStructFirstKernel",
            ],
            [
                "1000 1003 1006 1009 1100 1103 1106 1109",
                "2000 2005 2010 2015 2100 2105 2110 2115",
                "3000 3007 3014 3021 3100 3107 3114 3121",
                "4000 4002 4004 4006 4300 4302 4304 4306",
                "5000 5011 5022 5033 5100 5111 5122 5133",
                "6000 6013 6026 6039 6400 6413 6426 6439",
            ]);
    }

    [SkippableFact]
    public async Task GroupedKernelIndex_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LaunchGeometry/GroupedKernelIndex.cs",
            ["Kernels.GroupedKernelIndexKernel"],
            [
                "config 4x64", "mismatches 0", "disagreements 0",
                "config 8x256", "mismatches 0", "disagreements 0",
            ]);
    }

    [SkippableFact]
    public async Task GroupedKernelIndexHelper_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LaunchGeometry/GroupedKernelIndexHelper.cs",
            ["Kernels.KernelIndexHelperKernel"],
            ["0 1 2 3 1000 1001 1002 1003 2000 2001 2002 2003"]);
    }

    /// <summary>
    /// The CPU backend runs groups wider than its SIMD lanes in several calls
    /// and rejects them for kernels that synchronize their group.
    /// </summary>
    [SkippableFact]
    public async Task GroupedWideGroups_ProducesCorrectOutput()
    {
        Skip.If(
            Backend != BackendType.CPU,
            "Emulates a CPU device with wide groups");
        await VerifyProgramOutputAsync(
            "TestPrograms/LaunchGeometry/GroupedWideGroups.cs",
            ["Kernels.WideGroupKernel", "Kernels.WideBarrierKernel"],
            ["group 32 mismatches 0", "group 20 mismatches 0", "barrier rejected True"]);
    }

    [SkippableFact]
    public async Task Implicit1DBounds_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LaunchGeometry/Implicit1DBounds.cs",
            ["Kernels.Implicit1DBoundsKernel"],
            ["mismatches 0", "clobbered 0"]);
    }

    /// <summary>
    /// Implicitly grouped launches over <c>LongIndex1D</c>, <c>LongIndex2D</c>
    /// and <c>LongIndex3D</c> extents give every thread its own index.
    /// </summary>
    [SkippableFact]
    public async Task LongIndexLaunches_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LaunchGeometry/LongIndexLaunches.cs",
            [
                "Kernels.Long1DKernel",
                "Kernels.Long1DCopyKernel",
                "Kernels.Long2DKernel",
                "Kernels.Long3DKernel",
            ],
            [
                "7 10 13 16 19 22 25 28 31 34 37 40 43 46 49 52 55 58 61",
                "0 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18",
                "0 1 2 3 4 100 101 102 103 104 200 201 202 203 204",
                "0 1 2 3 4 100 101 102 103 104 200 201 202 203 204 " +
                    "10000 10001 10002 10003 10004 10100 10101 10102 10103 10104 " +
                    "10200 10201 10202 10203 10204",
            ]);
    }

    [SkippableFact]
    public async Task Implicit2DBounds_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LaunchGeometry/Implicit2DBounds.cs",
            ["Kernels.Implicit2DBoundsKernel"],
            ["mismatches 0", "clobbered 0"]);
    }
}
