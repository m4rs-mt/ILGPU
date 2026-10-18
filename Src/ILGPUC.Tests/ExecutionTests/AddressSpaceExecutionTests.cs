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
        : base(output, backend) { }

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
    public async Task GlobalRefHelper_ProducesCorrectOutput()
    {
        // data[t] = 10 * t, incremented by a helper through a reference
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/GlobalRefHelper.cs",
            ["Kernels.GlobalRefHelperKernel"],
            ["1", "11", "21", "31"]);
    }

    [SkippableFact]
    public async Task GlobalPointerLoop_ProducesCorrectOutput()
    {
        // input = [1, 2, 3, 4]; thread t sums input[0..t] through a reference
        // whose loop exits diverge per thread
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/GlobalPointerLoop.cs",
            ["Kernels.GlobalPointerLoopKernel"],
            ["1", "3", "6", "10"]);
    }

    [SkippableFact]
    public async Task UniformPointerLoop_ProducesCorrectOutput()
    {
        // input = [1, 2, 3, 4]; all threads sum the input through a reference
        // advanced by a uniform loop
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/UniformPointerLoop.cs",
            ["Kernels.UniformPointerLoopKernel"],
            ["10", "11", "12", "13"]);
    }

    [SkippableFact]
    public async Task SharedRefSelect_ProducesCorrectOutput()
    {
        // Even threads increment shared[t] = 10 * (t + 1), odd threads
        // shared[t + 32] = t; then data[t] = shared[t] + shared[t + 32]
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/SharedRefSelect.cs",
            ["Kernels.SharedRefSelectKernel"],
            ["11", "22", "33", "44"]);
    }

    [SkippableFact]
    public async Task RefHelperLoop_ProducesCorrectOutput()
    {
        // input = [1, 2, 3, 4]; a helper sums input[0..t] through a reference
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/RefHelperLoop.cs",
            ["Kernels.RefHelperLoopKernel"],
            ["1", "3", "6", "10"]);
    }

    [SkippableFact]
    public async Task LocalPointerAddress_ProducesCorrectOutput()
    {
        // A local pointer into global memory is changed and read through a
        // pointer to it, directly and in a helper; a helper reads through a
        // local array of pointers
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/LocalPointerAddress.cs",
            ["Kernels.DirectKernel", "Kernels.HelperKernel", "Kernels.ArrayKernel"],
            ["22 8 8 22", "1 8 15 22", "1 8 15 22"]);
    }

    [SkippableFact]
    public async Task LocalAddressInMemory_ProducesCorrectOutput()
    {
        // The addresses of local variables and of shared memory are stored to
        // memory and read back: in a local pointer whose address is taken, in a
        // view of nint and in a view of structs holding a pointer
        RequireCapability(Backend, BackendCapability.GenericAddressSpace);
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/LocalAddressInMemory.cs",
            [
                "Kernels.LocalPointerKernel",
                "Kernels.AddressViewKernel",
                "Kernels.SharedAddressKernel",
            ],
            ["3 9 31 23", "2 9 16 23", "2 3 4 5"]);
    }

    [SkippableFact]
    public async Task NullPointerLoop_ProducesCorrectOutput()
    {
        // x = t + 1, y = 10 * (t + 1); a pointer alternates between x and y
        // until it becomes null after t + 1 iterations
        RequireCapability(Backend, BackendCapability.GenericAddressSpace);
        await VerifyProgramOutputAsync(
            "TestPrograms/AddressSpace/NullPointerLoop.cs",
            ["Kernels.NullPointerLoopKernel"],
            ["1 22 36 88"]);
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
