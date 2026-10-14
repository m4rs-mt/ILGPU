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
    // pointer fields or passed to helpers. Metal and OpenCL qualified pointers
    // stored in shared memory and in local arrays with two address spaces and
    // kept the fields of structures selected at O1 and O2 or tested for null
    // generic, and setting the pointer field of a holder parameter in a helper
    // failed with an internal error on every backend
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
                "Kernels.ConditionalFieldKernel",
                "Kernels.SharedHolderKernel",
                "Kernels.ViewHolderArrayKernel",
                "Kernels.HolderPointerKernel",
                "Kernels.HolderBytesKernel",
                "Kernels.RetargetKernel",
                "Kernels.FieldNullCheckKernel",
            ],
            [
                "143", "253", "363", "473",
                "20", "41", "62", "83",
                "21", "11", "41", "31",
                "27", "47", "67", "87",
                "16", "26", "36", "46",
                "11", "8", "31", "8",
                "21", "10", "43", "32",
                "11", "22", "33", "40",
                "32", "42", "52", "62",
                "15", "25", "35", "45",
                "1516", "2526", "3536", "4546",
                "9", "21", "29", "41",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    // At O0, Metal and OpenCL kept a holder generic that was assigned holders
    // selected from its own value, also a structure holding a single pointer
    // incremented through an instance method, so that its pointer to a local
    // was emitted as a pointer to global memory and failed to compile natively
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O1)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O2)]
    public async Task SelfUpdating_ProduceCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/PointerField/SelfUpdating.cs",
            [
                "Kernels.SelfUpdatingHolderKernel",
                "Kernels.SelfUpdatingCountedKernel",
                "Kernels.SelfUpdatingCounterKernel",
            ],
            [
                "30", "30", "50", "30",
                "30", "30", "50", "30",
                "311", "321", "331", "341",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    // Structures holding pointers into different address spaces are selected
    // as structures holding generic pointers, which Metal and OpenCL reject as
    // pointers that can point to global and local memory
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release, OptimizationLevel.O0)]
    [InlineData(RoslynOptimizationLevel.Debug, OptimizationLevel.O1)]
    public async Task GenericFields_ProduceCorrectOutput(
        RoslynOptimizationLevel programOptimization,
        OptimizationLevel level)
    {
        RequireCapability(Backend, BackendCapability.GenericAddressSpace);
        await VerifyProgramOutputAsync(
            "TestPrograms/PointerField/GenericFields.cs",
            ["Kernels.SelectHolderKernel"],
            ["110", "220", "310", "420"],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    // The CPU stored the expression of a view element address instead of the
    // address, and Metal and OpenCL emitted pointers to pointers with two
    // address spaces and kept pointer locals whose address is taken generic.
    // Metal rejected comparing pointers to pointer locals holding pointers
    // into different address spaces as comparing distinct pointer types
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
                "Kernels.PointerToPointerKernel",
                "Kernels.SharedPointerToPointerKernel",
                "Kernels.ViewPointerToPointerKernel",
                "Kernels.MixedPointerToPointerCompareKernel",
            ],
            [
                "1181", "900", "3201", "920",
                "10", "11", "12", "13",
                "121", "231", "341", "451",
                "131", "151", "171", "191",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }
}
