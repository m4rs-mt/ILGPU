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
    /// Null arrays and their null tests, compiled in Debug and in Release: null
    /// arrays are distinct from empty arrays, also in helpers that are not
    /// inlined, and Array.Empty returns the same array for the same element
    /// type, which is not null, and different arrays for int, uint, an int enum
    /// and a structure of one int, whose IR type is the same.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O2)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O2)]
    public async Task NullArrays_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Array/NullArrays.cs",
            [
                "Kernels.NullArrayKernel",
                "Kernels.NullReferenceKernel",
                "Kernels.EmptyArrayKernel",
            ],
            [
                "1", "3", "3", "-1", "-1", "1", "0", "-2",
                "-1", "5", "-2", "-1", "-1", "0", "0", "-2",
                "3", "3", "3", "0", "-1", "11", "11", "-2",
                "-1", "5", "-2", "0", "4", "10", "11", "3",
                "1", "-1", "1", "3", "31", "0",
                "1", "-1", "1", "3", "31", "0",
                "0", "4", "0", "2", "41", "1",
                "0", "4", "0", "2", "41", "1",
                "1111", "1011", "1111", "1011",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
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

    /// <summary>
    /// Every call of a helper that allocates an array returns a new array, also
    /// when the helper is inlined twice into the kernel, and two new empty arrays
    /// are different, while Array.Empty returns the same array. Helpers that are
    /// not inlined keep arrays they only use inside, return from a single call or
    /// pass on, and Array.Empty, as well.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O2)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O2)]
    public async Task ArrayHelperCalls_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Array/ArrayHelperCalls.cs",
            ["Kernels.ArrayHelperCallKernel"],
            [
                "12", "0", "1", "1", "1", "1", "5", "101",
                "12", "0", "1", "3", "3", "4", "5", "101",
                "12", "0", "1", "5", "5", "7", "5", "101",
                "12", "0", "1", "7", "7", "10", "5", "101",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    /// <summary>
    /// Every iteration of a loop reuses the buffer of an array allocated in it,
    /// which the build accepts where no iteration uses an array of an earlier
    /// iteration after allocating its own: arrays used in their iteration, the
    /// array of the last iteration after the loop, the array of the previous
    /// iteration before the allocation, arrays swapped in a loop, the arrays of a
    /// helper that is not inlined and Array.Empty.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O2)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O2)]
    public async Task ArrayLoopAllocations_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Array/ArrayLoopAllocations.cs",
            ["Kernels.ArrayLoopAllocationKernel"],
            [
                "1", "201", "3", "3", "1", "1", "1",
                "21", "402", "6", "4", "103", "3", "2",
                "24", "603", "10", "5", "306", "6", "3",
                "64", "804", "15", "6", "610", "10", "4",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }
}
