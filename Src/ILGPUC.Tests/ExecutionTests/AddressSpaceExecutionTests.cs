// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AddressSpaceExecutionTests.cs
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
/// References into shared, local and global memory that flow through phis and
/// helper methods (address space inference).
/// </summary>
public abstract class AddressSpaceExecutionTests : ExecutionTestBase
{
    protected AddressSpaceExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend)
    {
        Skip.If(
            backend == BackendType.CPU,
            "The CPU backend cannot emit references held in phis or passed " +
            "to helper methods (raw pointers outside an unsafe context).");
    }

    [SkippableFact]
    public async Task SharedPointerLoop_ProducesCorrectOutput()
    {
        // shared = [1, 2, 3, 4]; thread t sums shared[0..t]
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/SharedPointerLoop.cs",
            ["Kernels.SharedPointerLoopKernel"],
            ["1", "3", "6", "10"]);
    }

    [SkippableFact]
    public async Task SharedRefHelper_ProducesCorrectOutput()
    {
        // shared[t] = 10 * t, incremented by a helper through a reference
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/SharedRefHelper.cs",
            ["Kernels.SharedRefHelperKernel"],
            ["1", "11", "21", "31"]);
    }

    [SkippableFact]
    public async Task LocalRefHelper_ProducesCorrectOutput()
    {
        // value = 10 * t, incremented by a helper through a reference
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/LocalRefHelper.cs",
            ["Kernels.LocalRefHelperKernel"],
            ["1", "11", "21", "31"]);
    }

    [SkippableFact]
    public async Task SharedRefReturn_ProducesCorrectOutput()
    {
        // A helper returns a (generic) reference to shared[t + 1] = t + 100
        RequireCapability(Backend, BackendCapability.GenericAddressSpace);
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/SharedRefReturn.cs",
            ["Kernels.SharedRefReturnKernel"],
            ["100", "101", "102", "103"]);
    }

    [SkippableFact]
    public async Task MixedSpaceRef_ProducesCorrectOutput()
    {
        // Even threads increment shared[t] = 10 * (t + 1), odd threads data[t] = t;
        // then data[t] += shared[t]
        RequireCapability(Backend, BackendCapability.GenericAddressSpace);
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/MixedSpaceRef.cs",
            ["Kernels.MixedSpaceRefKernel"],
            ["11", "22", "33", "44"]);
    }
}
