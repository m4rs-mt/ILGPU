// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BasicCallExecutionTests.cs
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

public abstract class BasicCallExecutionTests : ExecutionTestBase
{
    protected BasicCallExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task SimpleCall_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/SimpleCall.cs",
            ["Kernels.SimpleCallKernel"],
            ["0", "2", "4", "6"]);
    }

    [SkippableFact]
    public async Task NestedCall_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/NestedCall.cs",
            ["Kernels.NestedCallKernel"],
            ["20", "20", "20", "20"]);
    }

    [SkippableFact]
    public async Task CallWithOut_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/CallWithOut.cs",
            ["Kernels.CallWithOutKernel"],
            ["30", "30", "30", "30"]);
    }

    [SkippableFact]
    public async Task CallWithRef_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/CallWithRef.cs",
            ["Kernels.CallWithRefKernel"],
            ["14", "14", "14", "14"]);
    }

    /// <summary>
    /// An address-taken local written, passed to an inlined call and read back
    /// inside a loop that gives SimplifyControlFlow chains to merge (see
    /// <c>IRTests/InlinedAllocaIRTests</c>).
    /// </summary>
    [SkippableFact]
    public async Task MergedAllocaChain_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/InlinedAlloca/MergedAllocaChain.cs",
            ["Kernels.MergedAllocaChainKernel"],
            ["96", "96", "96", "96"]);
    }

    [SkippableFact]
    public async Task ChainCall_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/ChainCall.cs",
            ["Kernels.ChainCallKernel"],
            ["22", "22", "22", "22"]);
    }

    [SkippableFact]
    public async Task HelperUniformArgument_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/HelperUniformArgument.cs",
            ["Kernels.HelperUniformArgumentKernel"],
            ["1", "11", "21", "31", "41", "51", "61", "71", "81", "1", "11", "21"]);
    }

    [SkippableFact]
    public async Task HelperSwitchReturns_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/HelperSwitchReturns.cs",
            ["Kernels.HelperSwitchReturnsKernel"],
            ["5", "-1", "14", "-3", "28", "-5", "-6", "-7", "-8", "5", "-1", "14"]);
    }

    [SkippableFact]
    public async Task HelperInactiveLanes_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/HelperInactiveLanes.cs",
            ["Kernels.HelperInactiveLanesKernel"],
            ["100", "0", "0", "103", "0", "0", "106", "0", "0", "109", "0", "0"]);
    }

    /// <summary>
    /// A value read before a store and passed to an inlined helper after the store
    /// keeps the value read.
    /// </summary>
    [SkippableFact]
    public async Task CallArgumentLoadBeforeStore_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/CallArgumentLoadBeforeStore.cs",
            ["Kernels.CallArgumentLoadBeforeStoreKernel"],
            ["1", "2", "3", "4"]);
    }

    /// <summary>
    /// A value read before an inlined helper that overwrites it and passed to a
    /// second inlined helper keeps the value read.
    /// </summary>
    [SkippableFact]
    public async Task CallArgumentLoadBeforeInlinedStore_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/BasicCall/CallArgumentLoadBeforeInlinedStore.cs",
            ["Kernels.CallArgumentLoadBeforeInlinedStoreKernel"],
            ["1", "2", "3", "4"]);
    }
}
