// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalArrayExecutionTests.cs
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

public abstract class LocalArrayExecutionTests : ExecutionTestBase
{
    protected LocalArrayExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task WriteRead_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/WriteRead.cs",
            ["Kernels.WriteReadKernel"],
            ["10", "20", "30", "40"]);
    }

    [SkippableFact]
    public async Task Length_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/Length.cs",
            ["Kernels.LengthKernel"],
            ["5", "5", "5", "5"]);
    }

    [SkippableFact]
    public async Task Accumulate_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/Accumulate.cs",
            ["Kernels.AccumulateKernel"],
            ["6", "6", "6", "6"]);
    }

    /// <summary>
    /// The scratch buffer must be private per thread. The other programs in this
    /// family write thread-independent values, so a backend that shares one buffer
    /// across threads (or across the lanes of a vectorized CPU group) still yields
    /// the right answer; this one does not.
    /// </summary>
    [SkippableFact]
    public async Task ThreadVaryingScratch_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/ThreadVaryingScratch.cs",
            ["Kernels.ThreadVaryingScratchKernel"],
            ["0", "11", "22", "33"]);
    }

    /// <summary>
    /// fixed statements on arrays, which test the array for null, compiled in
    /// Debug and in Release. At O0, the pointer to the first element is merged
    /// with the null pointer of an empty array in a phi, also for arrays whose
    /// length is not a constant, also through a void pointer, and the pointer
    /// pinned on an empty array is compared with null.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O0)]
    public async Task FixedArray_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/FixedArray.cs",
            [
                "Kernels.FixedArrayKernel",
                "Kernels.FixedSelectKernel",
                "Kernels.FixedVoidKernel",
            ],
            [
                "0", "11", "22", "33",
                "6", "46", "86", "126",
                "-2", "-2", "-2", "-2",
                "606", "303", "1414", "707",
                "-7", "-7", "-7", "-7",
                "6", "3", "14", "7",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    /// <summary>
    /// A fixed statement on the null array of a default structure and on a
    /// local array, whose pointers flow into two pointer phis of one block with
    /// the null pointer, one uniform and one per thread. At O0, both phis take
    /// the null pointer when the block is entered.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release)]
    [InlineData(RoslynOptimizationLevel.Debug)]
    public async Task FixedNullPointers_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/FixedNullPointers.cs",
            ["Kernels.FixedNullPointersKernel"],
            ["-10", "-9", "-8", "-7"],
            new CompilationProperties { OptimizationLevel = OptimizationLevel.O0 },
            programOptimization);
    }

    [SkippableFact]
    public async Task IndexCompute_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/IndexCompute.cs",
            ["Kernels.IndexComputeKernel"],
            ["100", "200", "300", "400"]);
    }

    [SkippableFact]
    public async Task FloatType_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/FloatType.cs",
            ["Kernels.FloatArrayKernel"],
            ["1.5", "2.5", "3.5", "4.5"]);
    }

    [SkippableFact]
    public async Task AllocateInLoop_ProducesCorrectOutput()
    {
        // A fresh zeroed array per iteration: the elements sum up to i + 3
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/AllocateInLoop.cs",
            ["Kernels.AllocateInLoopKernel"],
            ["1271", "1271", "1271", "1271"]);
    }

    [SkippableFact]
    public async Task AllocateInHelperInLoop_ProducesCorrectOutput()
    {
        // A fresh zeroed array per helper call, also when inlined into a loop
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/AllocateInHelperInLoop.cs",
            ["Kernels.AllocateInHelperInLoopKernel"],
            ["1271", "1271", "1271", "1271"]);
    }

    /// <summary>
    /// Local memory per thread in 1D, 2D and 3D and in a helper that is not
    /// inlined, for several group sizes.
    /// </summary>
    [SkippableFact]
    public async Task LocalMemoryPerThread_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/LocalMemoryPerThread.cs",
            ["Kernels.LocalPerThreadKernel"],
            [
                "local 1D mismatches 0",
                "local helper mismatches 0",
                "local 2D mismatches 0",
                "local 3D mismatches 0",
            ]);
    }

    /// <summary>
    /// References that select one of two elements of a local array per thread,
    /// at constant indices and at an index that is not a constant, and a field
    /// of one of two elements of a local array of structures. At O0, the
    /// element addresses, and the field addresses derived from them, flow into
    /// a phi.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O2)]
    public async Task RefSelect_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/LocalArray/RefSelect.cs",
            [
                "Kernels.RefSelectKernel",
                "Kernels.RefSelectIndexKernel",
                "Kernels.RefFieldSelectKernel",
            ],
            [
                "10", "2", "30", "4",
                "11", "2", "30", "4",
                "10", "2", "30", "4",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }
}
