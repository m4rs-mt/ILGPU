// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedOperandCompileTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.PerfTests;

/// <summary>
/// Compile-time regression tests for kernels whose pure values share operands.
/// Every round of <see cref="PerfRegressionKernels.SharedOperandChain"/> reads
/// its intermediate value twice, so the number of operand paths through its
/// pure values doubles with every round;
/// <see cref="PerfRegressionKernels.SharedOperandFieldChain"/> reads it twice
/// from a field of a local structure. A walk over pure operands that does not
/// remember the values it has visited takes 2^64 steps on these kernels and
/// never finishes, whereas the whole pipeline compiles each of them for all
/// five backends in about a second. The time bound only turns such a hang into
/// a failure and is far above any legitimate compile time.
/// </summary>
public sealed class SharedOperandCompileTests(ITestOutputHelper output)
{
    private static readonly TimeSpan TimeBound = TimeSpan.FromMinutes(2);

    private static readonly BackendType[] AllBackends =
    [
        BackendType.CPU,
        BackendType.Cuda,
        BackendType.Metal,
        BackendType.OpenCL,
        BackendType.ROCm,
    ];

    [Theory]
    [InlineData(OptimizationLevel.O0)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    public void SharedOperandChain_CompilesOnAllBackendsInBoundedTime(
        OptimizationLevel level) =>
        CompileOnAllBackends(nameof(PerfRegressionKernels.SharedOperandChain), level);

    [Theory]
    [InlineData(OptimizationLevel.O0)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.O2)]
    public void SharedOperandFieldChain_CompilesOnAllBackendsInBoundedTime(
        OptimizationLevel level) =>
        CompileOnAllBackends(
            nameof(PerfRegressionKernels.SharedOperandFieldChain),
            level);

    /// <summary>
    /// Compiles the given kernel for all backends and fails if this does not
    /// finish within <see cref="TimeBound"/>.
    /// </summary>
    private void CompileOnAllBackends(string kernelName, OptimizationLevel level)
    {
        var kernel = typeof(PerfRegressionKernels).GetMethod(kernelName)!;
        var properties = new CompilationProperties(OptimizationLevel: level);

        var elapsed = RunWithTimeBound(() =>
        {
            using var helper = new CompilationHelper();
            foreach (var backend in AllBackends)
            {
                var source = helper.GenerateBackendCode(kernel, backend, properties)
                    .SourceCode;
                Assert.False(
                    string.IsNullOrEmpty(source),
                    $"{backend} produced no source");
            }
        });
        output.WriteLine(
            $"[{kernelName} {level}] all backends compiled in " +
            $"{elapsed.TotalMilliseconds:F0} ms");
    }

    /// <summary>
    /// Runs the given compilation on a background thread and fails if it does
    /// not finish within <see cref="TimeBound"/>.
    /// </summary>
    private static TimeSpan RunWithTimeBound(Action compile)
    {
        ExceptionDispatchInfo? failure = null;
        var stopwatch = Stopwatch.StartNew();
        var thread = new Thread(() =>
        {
            try
            {
                compile();
            }
            catch (Exception e)
            {
                failure = ExceptionDispatchInfo.Capture(e);
            }
        })
        {
            IsBackground = true,
        };
        thread.Start();

        Assert.True(
            thread.Join(TimeBound),
            $"Compilation did not finish within {TimeBound.TotalSeconds} s");
        failure?.Throw();
        return stopwatch.Elapsed;
    }
}
