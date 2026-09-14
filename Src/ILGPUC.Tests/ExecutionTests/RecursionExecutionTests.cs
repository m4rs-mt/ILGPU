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
/// other, which are compiled as recursive functions instead of being inlined.
/// </summary>
public abstract class RecursionExecutionTests : ExecutionTestBase
{
    protected RecursionExecutionTests(ITestOutputHelper output, BackendType backend)
        : base(output, backend) { }

    /// <summary>
    /// Runs the given test program on backends that support recursive calls.
    /// </summary>
    private Task VerifyAsync(
        string program,
        string expected,
        CompilationProperties? props = null)
    {
        Skip.If(
            Backend is BackendType.Metal or BackendType.OpenCL,
            $"{Backend} does not support recursive calls");
        return VerifyProgramOutputAsync(
            $"TestPrograms/Recursion/{program}.cs",
            [$"Kernels.{program}Kernel"],
            [expected],
            props);
    }

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
        VerifyAsync("RecursiveStores", "1 0 0 0 5 6 0 0 9 10 13 0 13 14 17 22");

    [SkippableFact]
    public Task RecursionWithLoop_ProducesCorrectOutput() =>
        VerifyAsync("RecursionWithLoop", "0 0 2 8 20 40 70 112");
}
