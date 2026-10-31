// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerArithmeticExecutionTests.cs
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

namespace ILGPUC.Tests.ExecutionTests;

/// <summary>
/// Pointer arithmetic in Debug and Release IL: Release IL converts pointers to
/// untyped native integers (conv.u) before moving them by byte offsets.
/// </summary>
public abstract class PointerArithmeticExecutionTests : ExecutionTestBase
{
    protected PointerArithmeticExecutionTests(
        ITestOutputHelper output,
        BackendType backend)
        : base(output, backend) { }

    // Pointers to locals move by elements and untyped addresses by bytes,
    // also when a condition selects between them and when they move backward,
    // as in p -= 2, p-- and p - (nint)2. Pointers moved by bytes keep their
    // pointer types, so that a condition can select them together with other
    // pointers. The uint indices stay below 2^31.
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    public async Task LocalPointers_ProduceCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/PointerArithmetic/LocalPointers.cs",
            [
                "Kernels.LocalOffsetsKernel",
                "Kernels.IndexKernel",
                "Kernels.MoveKernel",
                "Kernels.IntBackKernel",
                "Kernels.DifferenceKernel",
                "Kernels.SelectOffsetKernel",
            ],
            [
                "49", "89", "129", "169",
                "26", "160", "66", "86",
                "3510", "6740", "9930", "13180",
                "123", "112", "131", "123",
                "192", "202", "212", "222",
                "104222", "33333", "304222", "33333",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    // (byte*)p + 1 on an int pointer read the first byte, as the byte offset 1
    // was divided by the size of int. Pointers to padded structures move by
    // elements, and byte offsets such as (nint)i * 4 of byte pointers must not
    // overflow on 32 bits, and neither must b - int.MinValue, also when the
    // offset is a native integer made from an integer.
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    public async Task TypedPointers_ProduceCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/PointerArithmetic/TypedPointers.cs",
            [
                "Kernels.BytesKernel",
                "Kernels.ElementTypesKernel",
                "Kernels.StructElementsKernel",
                "Kernels.HelperKernel",
                "Kernels.NativeIntOffsetKernel",
                "Kernels.LargeOffsetsKernel",
                "Kernels.NegativeOffsetsKernel",
                "Kernels.NativeBytesKernel",
                "Kernels.StructOffsetsKernel",
            ],
            [
                "40240", "33250", "32260", "31270",
                "52", "96", "248", "172",
                "15", "94", "35", "134",
                "23", "43", "63", "83",
                "10", "40", "3", "40",
                "4294967296", "17179869200", "4294967304", "4294967270",
                "2147483648", "2147483648", "2147483648", "2147483647",
                "21474836481", "21474836482", "21474836473", "21474836474",
                "2110", "13331", "4330", "15551",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }
}
