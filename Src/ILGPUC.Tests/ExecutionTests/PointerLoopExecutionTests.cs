// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PointerLoopExecutionTests.cs
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
/// Loops over pointers that compare two pointers to find their end, in Debug
/// and Release IL.
/// </summary>
public abstract class PointerLoopExecutionTests : ExecutionTestBase
{
    protected PointerLoopExecutionTests(
        ITestOutputHelper output,
        BackendType backend)
        : base(output, backend) { }

    // Metal and OpenCL kept the loop pointers compared with end pointers generic
    // and failed to compile them
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O2)]
    public async Task LocalLoops_ProduceCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/PointerLoop/LocalLoops.cs",
            [
                "Kernels.LocalLessKernel",
                "Kernels.ViewNotEqualKernel",
                "Kernels.LocalSearchKernel",
                "Kernels.HelperKernel",
            ],
            [
                "19", "29", "39", "49",
                "100", "20", "30", "40",
                "-1", "-1", "1", "1",
                "119", "229", "339", "449",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O1)]
    public async Task SharedLoops_ProduceCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/PointerLoop/SharedLoops.cs",
            ["Kernels.SharedLessKernel"],
            ["-40", "-80", "-120", "-160"],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }
}
