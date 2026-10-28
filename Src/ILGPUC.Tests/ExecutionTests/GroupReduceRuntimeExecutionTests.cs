// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupReduceRuntimeExecutionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

/// <summary>
/// Group reduce execution tests with runtime-parameterized inputs.
/// Inputs are read from an <c>ArrayView</c> so the optimizer cannot fold
/// the reduction into a compile-time constant.
/// </summary>
public abstract class GroupReduceRuntimeExecutionTests : ExecutionTestBase
{
    protected GroupReduceRuntimeExecutionTests(
        ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task GroupAllReduceAdd_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GroupReduceRuntime/GroupAllReduceAddRuntime.cs",
            ["Kernels.GroupAllReduceAddRuntimeKernel"],
            ["10", "10", "10", "10"]);
    }

    /// <summary>
    /// Group and warp reductions and scans with an unsigned minimum or maximum
    /// over uint and ulong values with the highest bit set, as lambdas and as
    /// method groups of Math and XMath, in groups of several warps.
    /// </summary>
    [SkippableFact]
    public async Task UnsignedCollectives_Runtime_ProducesCorrectOutput()
    {
        string[] names =
        [
            "group reduce max",
            "group reduce min",
            "group all reduce max",
            "group all reduce min",
            "group inclusive scan max",
            "group inclusive scan min",
            "group exclusive scan max",
            "group exclusive scan min",
            "warp reduce max",
            "warp reduce min",
            "warp all reduce max",
            "warp all reduce min",
            "warp inclusive scan max",
            "warp inclusive scan min",
            "warp exclusive scan max",
            "warp exclusive scan min",
        ];
        var expected = new string[2 * names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            expected[i] = $"uint {names[i]} mismatches 0";
            expected[names.Length + i] = $"ulong {names[i]} mismatches 0";
        }
        await VerifyProgramOutputAsync(
            "TestPrograms/GroupReduceRuntime/UnsignedCollectivesRuntime.cs",
            ["Kernels.UIntCollectivesKernel", "Kernels.ULongCollectivesKernel"],
            expected);
    }

    [SkippableFact]
    public async Task GroupAllReduceMax_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GroupReduceRuntime/GroupAllReduceMaxRuntime.cs",
            ["Kernels.GroupAllReduceMaxRuntimeKernel"],
            ["4", "4", "4", "4"]);
    }

    /// <summary>
    /// Group and warp collectives over short and sbyte values with a negative
    /// identity, and with an unsigned minimum or maximum over ushort and byte
    /// values with the highest bit set, computed in 32 bits, in groups of several
    /// warps.
    /// </summary>
    [SkippableFact]
    public async Task SmallIntCollectives_Runtime_ProducesCorrectOutput()
    {
        string[] names =
        [
            "group reduce max",
            "group all reduce max",
            "group all reduce min",
            "group inclusive scan max",
            "group exclusive scan max",
            "group exclusive scan min",
            "warp exclusive scan max",
            "warp all reduce max",
            "warp inclusive scan min",
        ];
        await VerifyProgramOutputAsync(
            "TestPrograms/GroupReduceRuntime/SmallIntCollectivesRuntime.cs",
            [
                "Kernels.ShortCollectivesKernel",
                "Kernels.SByteCollectivesKernel",
                "Kernels.UShortCollectivesKernel",
                "Kernels.ByteCollectivesKernel",
            ],
            [
                .. names.Select(name => $"short {name} mismatches 0"),
                .. names.Select(name => $"sbyte {name} mismatches 0"),
                .. names.Select(name => $"ushort {name} mismatches 0"),
                .. names.Select(name => $"byte {name} mismatches 0"),
            ]);
    }

    /// <summary>
    /// Group reductions and scans, each alone in a loop over changing values, in
    /// groups of as many warps as the device allows, each reusing the shared
    /// memory of its call site in the next iteration.
    /// </summary>
    [SkippableFact]
    public async Task GroupCollectivesLoop_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GroupReduceRuntime/GroupCollectivesLoopRuntime.cs",
            [
                "Kernels.GroupReduceLoopKernel",
                "Kernels.GroupAllReduceLoopKernel",
                "Kernels.GroupInclusiveScanLoopKernel",
                "Kernels.GroupExclusiveScanLoopKernel",
            ],
            [
                "group reduce max mismatches 0",
                "group all reduce add mismatches 0",
                "group inclusive scan add mismatches 0",
                "group exclusive scan add mismatches 0",
            ]);
    }
}
