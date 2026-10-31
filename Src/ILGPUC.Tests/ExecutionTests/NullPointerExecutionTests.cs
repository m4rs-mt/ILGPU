// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: NullPointerExecutionTests.cs
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

public abstract class NullPointerExecutionTests : ExecutionTestBase
{
    protected NullPointerExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    /// <summary>
    /// Null checks of pointers to locals, view elements and shared memory and of
    /// references, compiled in Debug and in Release. O0 keeps the helpers and
    /// Unsafe.NullRef calls that O1 inlines.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O2)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O2)]
    public async Task NullChecks_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/NullPointer/NullChecks.cs",
            [
                "Kernels.LocalNullCheckKernel",
                "Kernels.LocalNullSelectKernel",
                "Kernels.ElementNullCheckKernel",
                "Kernels.SharedNullCheckKernel",
                "Kernels.HelperNullCheckKernel",
                "Kernels.NullRefCheckKernel",
                "Kernels.LoopNullCheckKernel",
                "Kernels.RefHelperNullCheckKernel",
            ],
            [
                "11 122 132 142",
                "-1 21 31 41",
                "110 20 130 40",
                "11 122 31 142",
                "1007 1060 1090 1120",
                "110 20 130 40",
                "-1 21 32 43",
                "7 100 7 140",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }
}
