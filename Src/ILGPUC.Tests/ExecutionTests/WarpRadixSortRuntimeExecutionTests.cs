// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: WarpRadixSortRuntimeExecutionTests.cs
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
/// Warp radix sort execution tests with runtime-parameterized inputs.
/// Inputs are read from an <c>ArrayView</c> so the optimizer cannot fold
/// the sort into a compile-time constant.
/// </summary>
public abstract class WarpRadixSortRuntimeExecutionTests : ExecutionTestBase
{
    protected WarpRadixSortRuntimeExecutionTests(
        ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task WarpRadixSortAscendingInt32_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/WarpRadixSortRuntime/" +
                "WarpRadixSortAscendingInt32Runtime.cs",
            ["Kernels.WarpRadixSortAscendingInt32RuntimeKernel"],
            ["1", "2", "3", "4", "5", "6", "7", "8"]);
    }

    [SkippableFact]
    public async Task WarpRadixSortDescendingInt32_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/WarpRadixSortRuntime/" +
                "WarpRadixSortDescendingInt32Runtime.cs",
            ["Kernels.WarpRadixSortDescendingInt32RuntimeKernel"],
            ["8", "7", "6", "5", "4", "3", "2", "1"]);
    }
}
