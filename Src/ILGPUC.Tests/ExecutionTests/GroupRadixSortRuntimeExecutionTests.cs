// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GroupRadixSortRuntimeExecutionTests.cs
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
/// Group radix sort execution tests with runtime-parameterized inputs.
/// Inputs are read from an <c>ArrayView</c> so the optimizer cannot fold
/// the sort into a compile-time constant.
/// </summary>
public abstract class GroupRadixSortRuntimeExecutionTests : ExecutionTestBase
{
    protected GroupRadixSortRuntimeExecutionTests(
        ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task GroupRadixSortAscendingInt32_Runtime_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/GroupRadixSortRuntime/" +
                "GroupRadixSortAscendingInt32Runtime.cs",
            ["Kernels.GroupRadixSortAscendingInt32RuntimeKernel"],
            ["1", "2", "3", "4", "5", "6", "7", "8"]);
    }
}
