// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicExecutionTests.cs
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

public abstract class AtomicExecutionTests : ExecutionTestBase
{
    protected AtomicExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task AtomicAdd_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/AtomicAdd.cs",
            ["Kernels.AtomicAddKernel"],
            ["4"]);
    }

    [SkippableFact]
    public async Task AtomicMax_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/AtomicMax.cs",
            ["Kernels.AtomicMaxKernel"],
            ["99"]);
    }

    [SkippableFact]
    public async Task AtomicMin_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/AtomicMin.cs",
            ["Kernels.AtomicMinKernel"],
            ["5"]);
    }

    [SkippableFact]
    public async Task AtomicMinMaxLong_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Int64AtomicMinMax);
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/AtomicMinMaxLong.cs",
            ["Kernels.AtomicMinMaxLongKernel"],
            ["-2000", "1000"]);
    }

    [SkippableFact]
    public async Task AtomicExchange_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/AtomicExchange.cs",
            ["Kernels.AtomicExchangeKernel"],
            ["42"]);
    }

    /// <summary>
    /// Unsigned and float atomics, mapped to native atomics where the target
    /// has them and to compare-and-swap loops otherwise.
    /// </summary>
    [SkippableFact]
    public async Task AtomicUnsignedFloat_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/AtomicUnsignedFloat.cs",
            ["Kernels.AtomicUnsignedFloatKernel"],
            [
                "10", "2147483651", "2147483648", "4294967280", "4026531840",
                "15", "3735928559", "2415919104",
                "5", "2.5", "-2", "7.25", "3.5", "4", "4.5",
            ]);
    }

    [SkippableFact]
    public async Task AtomicAddInLoop_ProducesCorrectOutput()
    {
        // Three increments per thread: the last returns 2, the counter ends at 3
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/AtomicAddInLoop.cs",
            ["Kernels.AtomicAddInLoopKernel"],
            ["23", "23", "23", "23"]);
    }

    /// <summary>
    /// A float atomic max in a branch whose result flows into the join of an
    /// if/else, lowered to a compare-and-swap loop where the target has no native
    /// float max.
    /// </summary>
    [SkippableFact]
    public async Task AtomicMaxJoin_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/AtomicMaxJoin.cs",
            ["Kernels.AtomicMaxJoinKernel"],
            ["2", "1"]);
    }

    /// <summary>
    /// A custom atomic built with Atomic.MakeAtomic in a branch whose result flows
    /// into the join of an if/else.
    /// </summary>
    [SkippableFact]
    public async Task MakeAtomicJoin_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Atomics/MakeAtomicJoin.cs",
            ["Kernels.MakeAtomicJoinKernel"],
            ["2", "1", "5"]);
    }
}
