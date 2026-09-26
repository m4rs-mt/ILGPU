// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayExecutionTests.cs
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

public abstract class ArrayExecutionTests : ExecutionTestBase
{
    protected ArrayExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task SimpleArray_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Array/SimpleArray.cs",
            ["Kernels.SimpleArrayKernel"],
            ["3", "5", "7", "9"]);
    }

    [SkippableFact]
    public async Task ArrayBounds_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Array/ArrayBounds.cs",
            ["Kernels.ArrayBoundsKernel"],
            ["0", "1", "-1", "-1"]);
    }

    /// <summary>
    /// Null arrays on the IL stack across branches, such as <c>a ?? b</c> of two
    /// conditional null arrays in Release builds, null phis in loop bodies, the
    /// length 0 of null arrays, also of ldnull; ldlen, and a null local that a
    /// loop replaces with a new array.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O2)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O2)]
    public async Task NullArrayStackValues_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Array/NullArrayStackValues.cs",
            ["Kernels.NullArrayStackKernel"],
            [
                "1", "1", "1", "1", "575", "333", "0", "1", "0", "902",
                "-1", "-1", "-1", "11", "757", "133", "0", "0", "0", "903",
                "1", "2", "1", "11", "575", "113", "0", "2", "0", "904",
                "1", "2", "1", "11", "757", "111", "0", "2", "0", "905",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }
}
