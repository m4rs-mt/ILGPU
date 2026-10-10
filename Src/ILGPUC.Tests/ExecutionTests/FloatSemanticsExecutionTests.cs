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
    /// MathF.Pow and MathF.CopySign follow .NET on negative bases, signed
    /// zeros, infinities and NaNs, for per-lane, uniform and mixed operands.
    /// </summary>
    [SkippableFact]
    public async Task PowCopySign_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatSemantics/PowCopySign.cs",
            ["Kernels.PowCopySignKernel"],
            [
                "1024 40000000 1024 C0000000 4",
                "-8 40000000 -8 C0000000 4",
                "4 40000000 4 C0000000 4",
                "nan 40000000 nan C0000000 4",
                "nan 41000000 nan C1000000 64",
                "-inf 80000000 -0.5 80000000 0",
                "inf 80000000 0.25 80000000 0",
                "-inf 7F800000 -8 FF800000 inf",
                "-0 FF800000 -0.125 FF800000 inf",
                "1 3F800000 inf BF800000 1",
                "1 7FC00000 1 FFC00000 nan",
                "1 C0400000 1 C0400000 9",
                "1 40400000 1 C0400000 9",
                "0 C0400000 0 C0400000 9",
                "0 FF800000 -0.5 FF800000 inf",
                "nan C0000000 nan C0000000 4",
                "nan 40A00000 nan C0A00000 25",
                "nan 7FC00000 -2 FFC00000 nan",
                "1 3F800000 nan BF800000 1",
                "1 BF800000 0 BF800000 1",
                "-0.125 C0000000",
            ]);
    }

    /// <summary>
    /// Math.Pow and Math.CopySign on doubles, with the same inputs as
    /// <see cref="PowCopySign_ProducesCorrectOutput"/>.
    /// </summary>
    [SkippableFact]
    public async Task PowCopySignDouble_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatSemantics/PowCopySignDouble.cs",
            ["Kernels.PowCopySignDoubleKernel"],
            [
                "1024 4000000000000000 1024 C000000000000000 4",
                "-8 4000000000000000 -8 C000000000000000 4",
                "4 4000000000000000 4 C000000000000000 4",
                "nan 4000000000000000 nan C000000000000000 4",
                "nan 4020000000000000 nan C020000000000000 64",
                "-inf 8000000000000000 -0.5 8000000000000000 0",
                "inf 8000000000000000 0.25 8000000000000000 0",
                "-inf 7FF0000000000000 -8 FFF0000000000000 inf",
                "-0 FFF0000000000000 -0.125 FFF0000000000000 inf",
                "1 3FF0000000000000 inf BFF0000000000000 1",
                "1 7FF8000000000000 1 FFF8000000000000 nan",
                "1 C008000000000000 1 C008000000000000 9",
                "1 4008000000000000 1 C008000000000000 9",
                "0 C008000000000000 0 C008000000000000 9",
                "0 FFF0000000000000 -0.5 FFF0000000000000 inf",
                "nan C000000000000000 nan C000000000000000 4",
                "nan 4014000000000000 nan C014000000000000 25",
                "nan 7FF8000000000000 -2 FFF8000000000000 nan",
                "1 3FF0000000000000 nan BFF0000000000000 1",
                "1 BFF0000000000000 0 BFF0000000000000 1",
                "-0.125 C000000000000000",
            ]);
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

    /// <summary>
    /// MathF.Round rounds halfway cases in [1, 2) to even, XMath.Cbrt keeps the
    /// sign of negative inputs, XMath.Exp2 folds to 2^x and float.IsInteger and
    /// float.IsOddInteger are exact.
    /// </summary>
    [SkippableFact]
    public async Task RoundingAndRoots_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/FloatSemantics/RoundingAndRoots.cs",
            ["Kernels.RoundingAndRootsKernel"],
            [
                "2000", "-2000", "1500", "9500", "0", "0",
                "-2000", "2000", "-1500", "6500", "0", "0",
                "0", "0", "500", "8500", "0", "0",
                "2000", "-2000", "2500", "10500", "0", "0",
                "-2000", "2000", "-2000", "6000", "1000", "0",
                "3000", "-3000", "3000", "11000", "1000", "1000",
                "1000", "-1000", "1250", "9250", "0", "0",
                "-1000", "1000", "-750", "7250", "0", "0"
            ]);
    }
}
