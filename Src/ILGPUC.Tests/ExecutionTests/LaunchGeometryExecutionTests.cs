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

    [SkippableFact]
    public async Task Implicit2DBounds_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LaunchGeometry/Implicit2DBounds.cs",
            ["Kernels.Implicit2DBoundsKernel"],
            ["mismatches 0", "clobbered 0"]);
    }
}
