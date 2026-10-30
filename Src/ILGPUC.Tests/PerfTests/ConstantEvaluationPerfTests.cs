// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ConstantEvaluationPerfTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Frontend;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using System;
using System.IO;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.PerfTests;

/// <summary>
/// Compile-time performance regression test of the constant evaluator: calls with
/// constant arguments are interpreted once per distinct method and arguments in a
/// compilation, whether they fold to a constant or exceed the instruction budget,
/// however many call sites repeat them.
/// </summary>
public sealed class ConstantEvaluationPerfTests(ITestOutputHelper output)
    : PerfTestBase(output)
{
    // Kernels defined here (not in Kernels/) to avoid auto-discovery by
    // KernelRegistry
    private static class Kernels
    {
        private static int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);

        private static int Start() => 0;

        private static int SlowSum(int n)
        {
            int sum = Start();
            for (int i = 0; i < n; ++i)
                sum += i;
            return sum;
        }

        public static void DistinctCallsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = Factorial(5) + Factorial(6) + SlowSum(1_000_000);
        }

        public static void RepeatedCallsKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] =
                Factorial(5) + Factorial(6) + SlowSum(1_000_000) +
                Factorial(5) + Factorial(6) + SlowSum(1_000_000) +
                Factorial(6) + SlowSum(1_000_000) + Factorial(5) +
                SlowSum(1_000_000);
        }
    }

    /// <summary>
    /// Compiles <paramref name="kernelName"/> with a fresh frontend cache and
    /// returns its frontend, cache and IR.
    /// </summary>
    private static (ILFrontend Frontend, ILFrontendCache Cache, string IR) Compile(
        string kernelName)
    {
        var kernel = typeof(Kernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)!;
        using var helper = new CompilationHelper(BackendType.CPU);
        var cache = new ILFrontendCache();
        var module = helper.CompileToModuleWithFrontend(
            kernel,
            null,
            BackendType.CPU,
            cache,
            out var frontend);
        using var writer = new StringWriter();
        module.Dump(
            writer,
            IRDumpMode.Normalized,
            IRPrinterFormat.LLVM,
            IRDumpPoint.AfterFrontend);
        return (frontend, cache, writer.ToString());
    }

    [Fact]
    public void RepeatedCalls_AreInterpretedOncePerDistinctArguments()
    {
        var distinct = Compile(nameof(Kernels.DistinctCallsKernel));
        var repeated = Compile(nameof(Kernels.RepeatedCallsKernel));
        Output.WriteLine(
            $"distinct: evaluations={distinct.Frontend.ConstantEvaluationCount} " +
            $"interpretations={distinct.Frontend.ConstantInterpretationCount} " +
            $"memo={distinct.Cache.ConstantEvaluations.Count}");
        Output.WriteLine(
            $"repeated: evaluations={repeated.Frontend.ConstantEvaluationCount} " +
            $"interpretations={repeated.Frontend.ConstantInterpretationCount} " +
            $"memo={repeated.Cache.ConstantEvaluations.Count}");

        // Every call site is evaluated, but each distinct method and arguments is
        // interpreted once and remembered with its result or budget failure
        Assert.True(
            repeated.Frontend.ConstantEvaluationCount >
            distinct.Frontend.ConstantEvaluationCount);
        Assert.Equal(
            distinct.Frontend.ConstantInterpretationCount,
            repeated.Frontend.ConstantInterpretationCount);
        Assert.Equal(
            repeated.Cache.ConstantEvaluations.Count,
            repeated.Frontend.ConstantInterpretationCount);
    }

    [Fact]
    public void RepeatedCalls_KeepTheirResults()
    {
        var repeated = Compile(nameof(Kernels.RepeatedCallsKernel));

        // The factorials fold to constants, the sums exceeding the budget remain
        // calls
        Assert.DoesNotContain("@Factorial", repeated.IR, StringComparison.Ordinal);
        Assert.Contains("120", repeated.IR, StringComparison.Ordinal);
        Assert.Contains("720", repeated.IR, StringComparison.Ordinal);
        Assert.Equal(
            4,
            repeated.IR.Split("call i32 @SlowSum").Length - 1);
    }
}
