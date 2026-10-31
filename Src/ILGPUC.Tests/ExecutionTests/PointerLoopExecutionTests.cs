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
    // and failed to compile them, fixed statements on local arrays in loops
    // failed to lower at O1 and O2 on every backend, and Apple's GPU compiler
    // dropped the stores of a pointer loop over a local array at O1 and O2
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
                "Kernels.FixedArrayKernel",
                "Kernels.FixedStoreKernel",
                "Kernels.FixedStoreInitKernel",
                "Kernels.FixedStoreLength3Kernel",
                "Kernels.ViewNotEqualKernel",
                "Kernels.LocalSearchKernel",
                "Kernels.HelperKernel",
            ],
            [
                "19", "29", "39", "49",
                "14366", "25516", "36666", "47816",
                "10", "21", "32", "43",
                "10", "21", "32", "43",
                "10", "21", "32", "40",
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

    // Metal and OpenCL kept structure fields and pointer locals whose address
    // is taken generic when they were assigned pointers derived from their own
    // values (c.P++, (*pp)++), and rejected comparing them with end pointers as
    // comparing pointers to global and local memory
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O2)]
    public async Task StoredLoops_ProduceCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/PointerLoop/StoredLoops.cs",
            [
                "Kernels.FieldLessKernel",
                "Kernels.TrailingFieldLessKernel",
                "Kernels.PointerToPointerLessKernel",
                "Kernels.PointerToPointerCompareKernel",
            ],
            [
                "194", "294", "394", "494",
                "132", "232", "332", "432",
                "19", "29", "39", "49",
                "131", "141", "151", "161",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    // Metal and OpenCL reject structure fields and pointer locals that point
    // to local memory on one iteration and to global memory on another
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O1)]
    public async Task MixedStoredLoops_ProduceCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        RequireCapability(Backend, BackendCapability.GenericAddressSpace);
        await VerifyProgramOutputAsync(
            "TestPrograms/PointerLoop/MixedStoredLoops.cs",
            ["Kernels.MixedFieldLoopKernel", "Kernels.MixedPointerLoopKernel"],
            [
                "33", "63", "93", "123",
                "30", "60", "90", "120",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    // Metal and OpenCL reject comparisons of pointers into different address
    // spaces (ILGPU1216)
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O1)]
    public async Task MixedSpaces_ProduceCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        RequireCapability(Backend, BackendCapability.GenericAddressSpace);
        await VerifyProgramOutputAsync(
            "TestPrograms/PointerLoop/MixedSpaces.cs",
            ["Kernels.MixedSpaceKernel"],
            ["12", "22", "32", "42"],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }
}
