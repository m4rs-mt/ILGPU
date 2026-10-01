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
    // also when a condition selects between them. Pointers moved by bytes
    // keep their pointer types, so that a condition can select them together
    // with other pointers.
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
                "Kernels.SelectOffsetKernel",
            ],
            [
                "49", "89", "129", "169",
                "4222", "3333", "4222", "3333",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    // (byte*)p + 1 on an int pointer read the first byte, as the byte offset 1
    // was divided by the size of int.
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
            ],
            [
                "40240", "33250", "32260", "31270",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }
}
