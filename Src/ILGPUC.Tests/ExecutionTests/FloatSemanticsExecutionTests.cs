// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FloatSemanticsExecutionTests.cs
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
/// IEEE semantics under the default math mode: NaN and infinity checks must
/// not be folded away by the native compiler. The fast math modes opt into
/// the native compilers' fast math.
/// </summary>
public abstract class FloatSemanticsExecutionTests : ExecutionTestBase
{
    protected FloatSemanticsExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task NaNInfinityChecks_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatSemantics/NaNInfinityChecks.cs",
            ["Kernels.NaNInfinityChecksKernel"],
            ["0", "5", "2", "2"]);
    }

    /// <summary>
    /// Unordered comparisons (bge.un, blt.un) are true for NaN operands, and
    /// moving a constant to the right-hand side keeps the comparison ordered.
    /// </summary>
    [SkippableFact]
    public async Task NaNComparisons_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatSemantics/NaNComparisons.cs",
            ["Kernels.NaNComparisonsKernel"],
            ["26", "8", "57", "22"]);
    }

    /// <summary>
    /// The % operator on floats is emitted as fmod.
    /// </summary>
    [SkippableFact]
    public async Task FloatRemainder_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatSemantics/FloatRemainder.cs",
            ["Kernels.FloatRemainderKernel"],
            ["1.5", "-1.5", "2", "1.25"]);
    }

    /// <summary>
    /// The minimum int and long values are emitted as well-typed literals.
    /// </summary>
    [SkippableFact]
    public async Task SignBitMask_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatSemantics/SignBitMask.cs",
            ["Kernels.SignBitMaskKernel"],
            ["1", "-1", "-1", "1"]);
    }

    /// <summary>
    /// MathF.Truncate and MathF.Round compile to device code on every backend.
    /// </summary>
    [SkippableFact]
    public async Task TruncateRound_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatSemantics/TruncateRound.cs",
            ["Kernels.TruncateRoundKernel"],
            ["2", "3", "3", "-2", "-3", "-3", "2", "2", "3", "-3", "-4", "-4"]);
    }

    /// <summary>
    /// Float min, max and clamp propagate NaN operands.
    /// </summary>
    [SkippableFact]
    public async Task NaNMinMax_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatSemantics/NaNMinMax.cs",
            ["Kernels.NaNMinMaxKernel"],
            ["0", "0", "0", "0", "0", "1", "1", "1", "1", "1"]);
    }

    /// <summary>
    /// The fast math mode still compiles and computes ordinary values.
    /// </summary>
    [SkippableFact]
    public async Task FastMath_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatConstant/PreciseFloatConstant.cs",
            ["Kernels.PreciseFloatConstantKernel"],
            ["1.2345678", "1.2345678", "1.2345678", "1.2345678"],
            new CompilationProperties(MathMode: MathMode.Fast));
    }

    /// <summary>
    /// The 32-bit-only fast math mode compiles double arithmetic as float.
    /// </summary>
    [SkippableFact]
    public async Task Fast32BitOnly_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatSemantics/Fast32BitOnly.cs",
            ["Kernels.Fast32BitOnlyKernel"],
            ["2", "3", "4", "5"],
            new CompilationProperties(MathMode: MathMode.Fast32BitOnly));
    }
}
