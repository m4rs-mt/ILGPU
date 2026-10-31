// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConvertExecutionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using RoslynOptimizationLevel = Microsoft.CodeAnalysis.OptimizationLevel;

using static ILGPUC.Tests.Framework.BackendCapability;

namespace ILGPUC.Tests.ExecutionTests;

public abstract class ConvertExecutionTests : ExecutionTestBase
{
    protected ConvertExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task Truncate_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/Truncate.cs",
            ["Kernels.TruncateKernel"],
            ["42", "42", "42", "42"]);
    }

    [SkippableFact]
    public async Task FloatToInt_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/FloatToInt.cs",
            ["Kernels.FloatToIntKernel"],
            ["3", "3", "3", "3"]);
    }

    [SkippableFact]
    public async Task Promote_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/Promote.cs",
            ["Kernels.PromoteKernel"],
            ["42", "42", "42", "42"]);
    }

    [SkippableFact]
    public async Task IntToFloat_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/IntToFloat.cs",
            ["Kernels.IntToFloatKernel"],
            ["7", "7", "7", "7"]);
    }

    /// <summary>
    /// conv.r.un resolves its target type from the operand on the stack.
    /// </summary>
    [SkippableFact]
    public async Task UIntToFloat_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/UIntToFloat.cs",
            ["Kernels.UIntToFloatKernel"],
            ["4000000000", "4000000000", "4000000000", "4000000000"]);
    }

    /// <summary>
    /// conv.r.un of a 32-bit operand converts to double without rounding
    /// through float first.
    /// </summary>
    [SkippableFact]
    public async Task UIntToDouble_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/UIntToDouble.cs",
            ["Kernels.UIntToDoubleKernel"],
            ["4000000001", "4000000001", "4000000001", "4000000001"]);
    }

    /// <summary>
    /// Unsigned 32-bit offsets widened to long zero-extend in element addresses.
    /// </summary>
    [SkippableFact]
    public async Task UnsignedElementOffset_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/UnsignedElementOffset.cs",
            ["Kernels.UnsignedElementOffsetKernel"],
            ["10 11 12 13"]);
    }

    /// <summary>
    /// Unsigned 32-bit offsets converted to nuint zero-extend in element
    /// addresses, both per-thread and constant offsets.
    /// </summary>
    [SkippableFact]
    public async Task NativeUnsignedOffset_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/NativeUnsignedOffset.cs",
            ["Kernels.NativeUnsignedOffsetKernel"],
            ["10 12", "11 12", "12 12", "13 12"]);
    }

    /// <summary>
    /// 8 and 16-bit values converted to uint extend with their own signs before
    /// they are zero-extended to ulong and nuint, as on the host.
    /// </summary>
    [SkippableFact]
    public async Task NarrowUnsignedWidening_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/NarrowUnsignedWidening.cs",
            ["Kernels.NarrowUnsignedWideningKernel"],
            [
                "4294967295 4294967295 255 65535 4294967295 4294967295 255 65535 " +
                    "-1 4294967295 13 13",
                "4294967168 4294934528 128 32768 0 4294934528 0 32768 " +
                    "-128 4294967295 12 12",
                "127 32767 127 32767 4294967168 4294967168 128 65408 " +
                    "127 4294967295 11 11",
                "5 5 5 5 5 5 5 5 5 4294967295 10 10",
            ]);
    }

    [SkippableFact]
    public async Task FloatToDouble_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/FloatToDouble.cs",
            ["Kernels.FloatToDoubleKernel"],
            ["3", "3", "3", "3"]);
    }

    [SkippableFact]
    public async Task DoubleToFloat_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/DoubleToFloat.cs",
            ["Kernels.DoubleToFloatKernel"],
            ["2", "2", "2", "2"]);
    }

    /// <summary>
    /// Bools computed at run time convert to 1.0 and 0.0, not to the bit
    /// patterns 1 and 0.
    /// </summary>
    [SkippableFact]
    public async Task BoolToFloat_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/BoolToFloat.cs",
            ["Kernels.BoolToFloatKernel"],
            BoolToFloatOutput);
    }

    [SkippableFact]
    public async Task BoolToDouble_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/BoolToDouble.cs",
            ["Kernels.BoolToDoubleKernel"],
            BoolToFloatOutput);
    }

    [SkippableFact]
    public async Task BoolToHalf_ProducesCorrectOutput()
    {
        RequireCapability(Backend, BackendCapability.Float16);
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertFloat/BoolToHalf.cs",
            ["Kernels.BoolToHalfKernel"],
            BoolToFloatOutput);
    }

    private static readonly string[] BoolToFloatOutput =
        ["0", "1", "0", "0", "1", "3.5", "1", "3.5"];

    [SkippableFact]
    public async Task ByteToInt_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/ByteToInt.cs",
            ["Kernels.ByteToIntKernel"],
            ["200", "200", "200", "200"]);
    }

    [SkippableFact]
    public async Task IntToByte_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/IntToByte.cs",
            ["Kernels.IntToByteKernel"],
            ["44", "44", "44", "44"]);
    }

    [SkippableFact]
    public async Task SignExtend_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/SignExtend.cs",
            ["Kernels.SignExtendKernel"],
            ["-10", "-10", "-10", "-10"]);
    }

    /// <summary>
    /// Chars in buffers and kernel parameters are unsigned 16-bit integers, so
    /// values from 0x8000 widen, shift, compare, divide and convert to float as
    /// unsigned values.
    /// </summary>
    [SkippableFact]
    public async Task CharUnsigned_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/CharUnsigned.cs",
            ["Kernels.CharUnsignedKernel"],
            [
                "65 4 0 21 32834 65",
                "32767 2047 0 10922 0 32767",
                "32768 2048 1 10922 1 32768",
                "65535 4095 1 21845 32768 65535",
            ]);
    }

    /// <summary>
    /// Native integers, which are pointers in the IR, compute with their integer
    /// values, also when they are parameters of helpers, and unsigned integers
    /// extend to them with zeros.
    /// </summary>
    [SkippableTheory]
    [InlineData(OptimizationLevel.O0)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    public async Task NativeInt_ProducesCorrectOutput(OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/NativeInt.cs",
            ["Kernels.NativeIntKernel"],
            [
                "-1 0 2 -1 4 -1 4294967295",
                "0 2 8 -1 0 -205 4294967294",
                "1 3 14 0 4 -7 4294967293",
                "1 5 20 1 0 -211 4294967292",
            ],
            new CompilationProperties { OptimizationLevel = level });
    }

    /// <summary>
    /// Pointers convert to integers and back, also addresses that differ per
    /// lane and integers read from memory, and the difference of two pointers
    /// counts their elements.
    /// </summary>
    [SkippableTheory]
    [InlineData(OptimizationLevel.O0)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    public async Task PointerInt_ProducesCorrectOutput(OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertInt/PointerInt.cs",
            ["Kernels.PointerIntKernel"],
            ["1 3 4 5 4", "1 13 14 15 4", "1 23 24 25 4", "1 33 34 35 4"],
            new CompilationProperties { OptimizationLevel = level });
    }

    /// <summary>
    /// Native integers negate and invert, and the address of a local field
    /// converted to a native integer and moved by 4 bytes still writes the next
    /// field, which Metal lost.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release)]
    [InlineData(RoslynOptimizationLevel.Debug)]
    public async Task NativeIntegerUnary_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertPointer/NativeIntegerUnary.cs",
            ["Kernels.NativeIntegerUnaryKernel"],
            ["-11", "-1021", "-2031", "-3041"],
            programOptimization: programOptimization);
    }

    /// <summary>
    /// Differences of view element pointers count elements and bytes, also
    /// in the C# of CPU kernels, where the converted addresses need
    /// parentheses.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release)]
    [InlineData(RoslynOptimizationLevel.Debug)]
    public async Task PointerDifferences_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertPointer/PointerDifferences.cs",
            ["Kernels.PointerDifferencesKernel"],
            ["-290", "824", "1938", "3052"],
            programOptimization: programOptimization);
    }

    /// <summary>
    /// The address of a local variable converted to an integer and back to a
    /// pointer, also in a field of a structure passed to a helper, keeps
    /// pointing to local memory, so backends without generic pointers do not
    /// reject it with ILGPU1215.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release)]
    [InlineData(RoslynOptimizationLevel.Debug)]
    public async Task LocalAddressRoundTrip_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/ConvertPointer/LocalAddressRoundTrip.cs",
            ["Kernels.RoundTripKernel", "Kernels.FieldKernel"],
            ["0 0", "1 2", "2 4", "3 6"],
            programOptimization: programOptimization);
    }
}
