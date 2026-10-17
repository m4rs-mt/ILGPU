// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructureExecutionTests.cs
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

public abstract class StructureExecutionTests : ExecutionTestBase
{
    protected StructureExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    [SkippableFact]
    public async Task StructFieldAccess_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/StructFieldAccess.cs",
            ["Kernels.StructFieldAccessKernel"],
            ["142", "142", "142", "142"]);
    }

    [SkippableFact]
    public async Task StructPassByValue_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/StructPassByValue.cs",
            ["Kernels.StructPassByValueKernel"],
            ["1253", "1253", "1253", "1253"]);
    }

    /// <summary>
    /// Struct parameters with signed byte and unsigned fields are passed by value.
    /// </summary>
    [SkippableFact]
    public async Task StructPassByValueFields_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/StructPassByValueFields.cs",
            ["Kernels.StructPassByValueFieldsKernel"],
            ["8000059693", "8000059694", "8000059695", "8000059696"]);
    }

    /// <summary>
    /// Tests local struct construction with per-field writes.
    /// Exercises LoadFieldAddress on local alloca — the vectorized LFA
    /// path on CPU. GPU backends handle this through the shared code generator.
    /// </summary>
    [SkippableFact]
    public async Task StructLocalBuild_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/StructLocalBuild.cs",
            ["Kernels.StructLocalBuildKernel"],
            ["10", "10", "10", "10"]);
    }

    /// <summary>
    /// In-place updates of struct fields stored in a buffer go through the
    /// field address, which must use the field's byte offset.
    /// </summary>
    [SkippableFact]
    public async Task StructFieldAddress_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/StructFieldAddress.cs",
            ["Kernels.StructFieldAddressKernel"],
            ["0", "1", "13", "1", "2", "23", "2", "3", "33", "3", "4", "43"]);
    }

    /// <summary>
    /// In-place updates of struct fields behind alignment padding go through
    /// field addresses that include the padding.
    /// </summary>
    [SkippableFact]
    public async Task StructFieldAddressPadding_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/StructFieldAddressPadding.cs",
            ["Kernels.StructFieldAddressPaddingKernel"],
            ["11 3000 18 130", "12 3003 19 131", "13 3006 20 132", "14 3009 21 133"]);
    }

    /// <summary>
    /// A nested struct keeps its alignment within the flattened outer struct
    /// so that device and host layouts agree.
    /// </summary>
    [SkippableFact]
    public async Task NestedStructLayout_ProducesCorrectOutput()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/NestedStructLayout.cs",
            ["Kernels.NestedStructLayoutKernel"],
            ["7", "1", "2", "3", "9", "7", "2", "4", "6", "9"]);
    }

    /// <summary>
    /// Instance methods on a readonly record struct in a kernel local receive
    /// their receiver by reference from local memory.
    /// </summary>
    [SkippableTheory]
    [InlineData(OptimizationLevel.O0)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    public async Task RecordStructInstanceCall_ProducesCorrectOutput(
        OptimizationLevel level)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/RecordStructInstanceCall.cs",
            ["Kernels.PackKernel", "Kernels.DistanceKernel"],
            [
                "16909060", "33752069", "50595078", "67438087",
                "16909060", "84281112", "16909060", "84281112",
            ],
            new CompilationProperties { OptimizationLevel = level });
    }

    /// <summary>
    /// A struct whose only field is a pointer or reference has the IR type of that
    /// pointer, and a read of its field from a struct value gives the pointer itself
    /// instead of dereferencing the value as the address of the struct.
    /// </summary>
    [SkippableTheory]
    [InlineData(OptimizationLevel.O0, RoslynOptimizationLevel.Release)]
    [InlineData(OptimizationLevel.O1, RoslynOptimizationLevel.Release)]
    [InlineData(OptimizationLevel.O2, RoslynOptimizationLevel.Release)]
    [InlineData(OptimizationLevel.O1, RoslynOptimizationLevel.Debug)]
    public async Task PointerStructValue_ProducesCorrectOutput(
        OptimizationLevel level,
        RoslynOptimizationLevel programOptimization)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/PointerStructValue.cs",
            ["Kernels.HelperKernel", "Kernels.RefKernel", "Kernels.RefHelperKernel"],
            ["1 8 15 22", "1 8 15 22", "1 8 15 22"],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    /// <summary>
    /// A struct holds the address of a local or of a field of a local struct, and
    /// the kernel reads the local through it: the stored address keeps the local in
    /// memory, also when the pointer or reference is the only field of the struct.
    /// </summary>
    [SkippableTheory]
    [InlineData(OptimizationLevel.O0, RoslynOptimizationLevel.Release)]
    [InlineData(OptimizationLevel.O1, RoslynOptimizationLevel.Release)]
    [InlineData(OptimizationLevel.O2, RoslynOptimizationLevel.Release)]
    [InlineData(OptimizationLevel.O1, RoslynOptimizationLevel.Debug)]
    public async Task LocalAddressInStruct_ProducesCorrectOutput(
        OptimizationLevel level,
        RoslynOptimizationLevel programOptimization)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/LocalAddressInStruct.cs",
            [
                "Kernels.ConstructorKernel",
                "Kernels.InitializerKernel",
                "Kernels.HelperKernel",
                "Kernels.PaddedKernel",
                "Kernels.RefKernel",
                "Kernels.StructFieldKernel",
                "Kernels.FieldAddressKernel",
            ],
            [
                "1 8 15 22",
                "1 8 15 22",
                "1 8 15 22",
                "1 8 15 22",
                "1 8 15 22",
                "1 8 15 22",
                "1 8 15 22",
            ],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }

    /// <summary>
    /// A struct with a pointer field and a second field is carried through a loop
    /// that assigns the address of a local to its pointer field.
    /// </summary>
    [SkippableTheory]
    [InlineData(OptimizationLevel.O1, RoslynOptimizationLevel.Release)]
    [InlineData(OptimizationLevel.O2, RoslynOptimizationLevel.Release)]
    [InlineData(OptimizationLevel.O1, RoslynOptimizationLevel.Debug)]
    public async Task PointerFieldLoop_ProducesCorrectOutput(
        OptimizationLevel level,
        RoslynOptimizationLevel programOptimization)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/Structure/PointerFieldLoop.cs",
            ["Kernels.AssignKernel", "Kernels.SelectKernel"],
            ["1 12 23 34 45 56 67 78", "1 12 16 28 35 48 58 72"],
            new CompilationProperties { OptimizationLevel = level },
            programOptimization);
    }
}
