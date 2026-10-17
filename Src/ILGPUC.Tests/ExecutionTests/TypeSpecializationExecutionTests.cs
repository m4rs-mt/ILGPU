// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeSpecializationExecutionTests.cs
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
/// Type-specialized generic helpers (issue #1427): <c>typeof(T)</c> tests select
/// the arithmetic per instantiation and remove the throwing fallback at compile
/// time. The programs are compiled in Debug and in Release, because the C#
/// compiler emits different IL shapes for type tests in each.
/// </summary>
public abstract class TypeSpecializationExecutionTests : ExecutionTestBase
{
    protected TypeSpecializationExecutionTests(
        ITestOutputHelper output,
        BackendType backend)
        : base(output, backend) { }

    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release)]
    [InlineData(RoslynOptimizationLevel.Debug)]
    public async Task GenericSin_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/TypeSpecialization/GenericSin.cs",
            ["Kernels.Kernel2"],
            ["0.4794", "0.8415", "0.4794", "0.8415"],
            programOptimization: programOptimization);
    }

    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release)]
    [InlineData(RoslynOptimizationLevel.Debug)]
    public async Task GenericSinDouble_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization)
    {
        RequireCapability(Backend, BackendCapability.Float64);
        await VerifyProgramOutputAsync(
            "TestPrograms/TypeSpecialization/GenericSinDouble.cs",
            ["Kernels.Kernel2"],
            ["0.4794", "0.8415", "0.8776", "0.5403"],
            programOptimization: programOptimization);
    }

    /// <summary>
    /// An instantiation found through a generic wrapper that throws for its type
    /// arguments is skipped at build time and throws when launched.
    /// </summary>
    [SkippableFact]
    public async Task SkippedInstantiation_ThrowsAtLaunch()
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/TypeSpecialization/SkippedInstantiation.cs",
            ["Kernels.Kernel2"],
            ["0.4794", "0.8415", "KernelNotCompiledException"]);
    }

    /// <summary>
    /// Type tests in an inline lambda kernel of a generic method are evaluated
    /// for every instantiation of the method.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release)]
    [InlineData(RoslynOptimizationLevel.Debug)]
    public async Task InlineTypeTests_ProducesCorrectOutput(
        RoslynOptimizationLevel programOptimization)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/TypeSpecialization/InlineTypeTests.cs",
            [],
            ["2 4 6", "1 3 5"],
            programOptimization: programOptimization);
    }

    /// <summary>
    /// Type tests with patterns (<c>x is float f</c>, <c>default(T) is float</c>,
    /// <c>x.Equals(default)</c>) fold for boxed value types.
    /// </summary>
    [SkippableTheory]
    [InlineData(RoslynOptimizationLevel.Release)]
    [InlineData(RoslynOptimizationLevel.Debug)]
    public async Task TypeTests_ProduceCorrectOutput(
        RoslynOptimizationLevel programOptimization)
    {
        await VerifyProgramOutputAsync(
            "TestPrograms/TypeSpecialization/TypeTests.cs",
            ["Kernels.Classify"],
            ["1", "1", "2", "1", "2", "2", "4.5", "1.5"],
            programOptimization: programOptimization);
    }
}
