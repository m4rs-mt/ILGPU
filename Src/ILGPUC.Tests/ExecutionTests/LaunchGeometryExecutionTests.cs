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
        SkipOnSingleBlockCpu();
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
        SkipOnSingleBlockCpu();
        await VerifyProgramOutputAsync(
            "TestPrograms/LaunchGeometry/GroupedKernelIndex.cs",
            ["Kernels.GroupedKernelIndexKernel"],
            [
                "config 4x64", "mismatches 0", "disagreements 0",
                "config 8x256", "mismatches 0", "disagreements 0",
            ]);
    }

    /// <summary>
    /// The CPU backend runs a kernel as a single vectorized block (Grid.Index
    /// is 0, Group.Dimension is the SIMD width), so it does not model
    /// explicitly grouped launches yet.
    /// </summary>
    private void SkipOnSingleBlockCpu() =>
        Skip.If(
            Backend == BackendType.CPU,
            "The CPU backend does not model grids and groups of grouped launches");

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
