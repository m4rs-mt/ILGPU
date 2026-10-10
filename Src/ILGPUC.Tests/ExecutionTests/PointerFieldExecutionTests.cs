// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerFieldExecutionTests.cs
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
/// Pointers stored in structure fields and in pointer locals whose address is
/// taken, in Debug and Release IL.
/// </summary>
public abstract class PointerFieldExecutionTests : ExecutionTestBase
{
    protected PointerFieldExecutionTests(
        ITestOutputHelper output,
        BackendType backend)
        : base(output, backend) { }

    // The generated CPU code did not compile: structures with pointer fields
    // were not unsafe, and per-lane pointers were not converted from and to
    // pointer fields or passed to helpers
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O2)]
    public async Task Fields_ProduceCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/PointerField/Fields.cs",
            [
                "Kernels.LocalFieldKernel",
                "Kernels.ViewFieldKernel",
                "Kernels.SharedFieldKernel",
                "Kernels.HelperFieldKernel",
                "Kernels.LocalHelperFieldKernel",
                "Kernels.HolderPointerKernel",
            ],
            [
                "143", "253", "363", "473",
                "20", "41", "62", "83",
                "21", "11", "41", "31",
                "27", "47", "67", "87",
                "16", "26", "36", "46",
                "32", "42", "52", "62",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    // The CPU stored the expression of a view element address instead of the
    // address
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O2)]
    public async Task PointerToPointer_ProduceCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/PointerField/PointerToPointer.cs",
            [
                "Kernels.ViewPointerToPointerKernel",
            ],
            [
                "121", "231", "341", "451",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }
}
