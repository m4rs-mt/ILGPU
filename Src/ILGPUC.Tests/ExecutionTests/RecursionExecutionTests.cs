// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RecursionExecutionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.ExecutionTests;

/// <summary>
/// Tests kernels calling helpers that call themselves, directly or through each
/// other, which are compiled as recursive functions instead of being inlined. The
/// Metal and OpenCL backends reject them (see <c>RecursionTests</c>), but run
/// helpers calling themselves only in tail position, which become loops, and
/// recursive helpers called with constant arguments, which are evaluated at
/// compile time.
/// </summary>
public abstract class RecursionExecutionTests : ExecutionTestBase
{
    protected RecursionExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    /// <summary>
    /// Runs the given test program, which keeps recursive calls, on backends that
    /// support them.
    /// </summary>
    private Task VerifyAsync(
        string program,
        string expected,
        CompilationProperties? props = null)
    {
        Skip.If(
            Backend is BackendType.Metal or BackendType.OpenCL,
            $"{Backend} does not support recursive calls");
        return VerifyWithoutRecursionAsync(program, expected, props);
    }

    /// <summary>
    /// Runs the given test program, which has no recursive calls left after
    /// compilation, on all backends.
    /// </summary>
    private Task VerifyWithoutRecursionAsync(
        string program,
        string expected,
        CompilationProperties? props = null,
        Microsoft.CodeAnalysis.OptimizationLevel programOptimization =
            Microsoft.CodeAnalysis.OptimizationLevel.Release) =>
        VerifyProgramOutputAsync(
            $"TestPrograms/Recursion/{program}.cs",
            [$"Kernels.{program}Kernel"],
            [expected],
            props,
            programOptimization);

    [SkippableFact]
    public Task Factorial_ProducesCorrectOutput() =>
        VerifyAsync("Factorial", "1 2 6 24 120 720 5040 40320");

    [SkippableFact]
    public Task FactorialO0_ProducesCorrectOutput() =>
        VerifyAsync(
            "Factorial",
            "1 2 6 24 120 720 5040 40320",
            new CompilationProperties { OptimizationLevel = OptimizationLevel.O0 });

    [SkippableFact]
    public Task FactorialO2_ProducesCorrectOutput() =>
        VerifyAsync(
            "Factorial",
            "1 2 6 24 120 720 5040 40320",
            new CompilationProperties { OptimizationLevel = OptimizationLevel.O2 });

    [SkippableFact]
    public Task GenericFactorial_ProducesCorrectOutput() =>
        VerifyAsync("GenericFactorial", "2 5 12 34 135 741 5068 40356");

    [SkippableFact]
    public Task MutualRecursion_ProducesCorrectOutput() =>
        VerifyAsync("MutualRecursion", "1 0 0 2 1 6 0 14 1 30 0 62");

    [SkippableFact]
    public Task Fibonacci_ProducesCorrectOutput() =>
        VerifyAsync("Fibonacci", "0 1 1 2 3 5 8 13 21 34");

    [SkippableFact]
    public Task RecursiveStores_ProducesCorrectOutput() =>
        VerifyWithoutRecursionAsync(
            "RecursiveStores",
            "1 0 0 0 5 6 0 0 9 10 13 0 13 14 17 22");

    [SkippableFact]
    public Task RecursionWithLoop_ProducesCorrectOutput() =>
        VerifyAsync("RecursionWithLoop", "0 0 2 8 20 40 70 112");

    [SkippableFact]
    public Task TailRecursion_ProducesCorrectOutput() =>
        VerifyWithoutRecursionAsync(
            "TailRecursion",
            "4 0 0 0 2 1 1 0 4 3 1 2 2 6 0 0");

    [SkippableFact]
    public Task TailRecursionDebugBuild_ProducesCorrectOutput() =>
        VerifyWithoutRecursionAsync(
            "TailRecursion",
            "4 0 0 0 2 1 1 0 4 3 1 2 2 6 0 0",
            programOptimization: Microsoft.CodeAnalysis.OptimizationLevel.Debug);

    [SkippableFact]
    public Task TailRecursionO0_ProducesCorrectOutput() =>
        VerifyWithoutRecursionAsync(
            "TailRecursion",
            "4 0 0 0 2 1 1 0 4 3 1 2 2 6 0 0",
            new CompilationProperties { OptimizationLevel = OptimizationLevel.O0 });

    [SkippableFact]
    public Task TailRecursionPassingAddresses_ProducesCorrectOutput() =>
        VerifyAsync(
            "TailRecursionAddresses",
            "0 0 2 2 5 5 9 9 14 14 20 20 27 27 35 35");

    [SkippableFact]
    public Task TailRecursionStoringAddresses_ProducesCorrectOutput()
    {
        Skip.If(
            Backend is BackendType.CPU,
            "The CPU backend does not support views of nint");
        return VerifyAsync(
            "TailRecursionStoredAddresses",
            "0 0 2 2 5 5 9 9 14 14 20 20 27 27 35 35");
    }

    [SkippableFact]
    public Task ConstantRecursion_ProducesCorrectOutput() =>
        VerifyWithoutRecursionAsync("ConstantRecursion", "1120 1121 1122 1123");
}
