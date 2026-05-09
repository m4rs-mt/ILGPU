// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AtomicsFloat64ExecutionTests.cs
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
/// Family B.1 regression: Float64 atomic add. The custom-CAS form
/// (<c>Atomic.MakeAtomic</c> with a delegate) was the path that exposed
/// three independent emitter defects fixed on <c>temp5</c>:
/// self-loop classification, missing <c>FloatAsIntCast</c> /
/// <c>IntAsFloatCast</c> dispatch arms, and element-type-blind pointer-cast
/// emission.
///
/// Skip-gates: backends without Float64 atomics (Metal — no Float64 atomic
/// path) or without 64-bit atomic-CAS (OpenCL — needs
/// <c>cl_khr_int64_base_atomics</c>) are filtered by the existing
/// <see cref="BackendCapability"/> machinery.
/// </summary>
public abstract class AtomicsFloat64ExecutionTests : ExecutionTestBase
{
    protected AtomicsFloat64ExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task AtomicAddDouble_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64Atomics);
        await VerifyProgramOutputAsync(
            "TestPrograms/AtomicsFloat64/AtomicAddDouble.cs",
            ["Kernels.AtomicAddDoubleKernel"],
            ["10"]);
    }

    [SkippableFact]
    public async Task AtomicMakeAtomicAddDouble_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64Atomics);
        await VerifyProgramOutputAsync(
            "TestPrograms/AtomicsFloat64/AtomicMakeAtomicAddDouble.cs",
            ["Kernels.AtomicMakeAtomicAddDoubleKernel"],
            ["10"]);
    }
}
