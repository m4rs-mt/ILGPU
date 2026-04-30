// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: PerfTestBase.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Frontend;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit.Abstractions;

namespace ILGPUC.Tests.PerfTests;

/// <summary>
/// Shared helper for compile-time perf regression tests. Wraps
/// <see cref="ILFrontend.LoadMethodsInstrumented"/> so individual tests can
/// stay focused on the assertions.
/// </summary>
public abstract class PerfTestBase(ITestOutputHelper output)
{
    /// <summary>
    /// Output sink shared with derived test classes so they don't have to
    /// capture <c>ITestOutputHelper</c> twice (which would trigger CS9107).
    /// </summary>
    protected ITestOutputHelper Output { get; } = output;


    /// <summary>
    /// Runs the disassembly + transitive walk for <paramref name="kernels"/>
    /// against a fresh <see cref="ILFrontend"/> and returns the resulting
    /// counters. Backend defaults to CPU because the round-3 walkability
    /// classifier is backend-independent and CPU has zero side-effects on
    /// counter values.
    /// </summary>
    internal LoadMethodsTimings LoadAndMeasure(
        IReadOnlyList<MethodBase> kernels,
        ILFrontendCache? cache = null,
        BackendType backendType = BackendType.CPU)
    {
        var assemblyDir = Path.GetDirectoryName(
            kernels[0].DeclaringType?.Assembly.Location);
        var frontend = new ILFrontend(backendType, cache, assemblyDir);
        frontend.LoadMethodsInstrumented(kernels, out var timings);
        return timings;
    }

    /// <summary>
    /// Runs the disassembly + transitive walk and exposes the underlying
    /// frontend so the walkability test can reflect into private state.
    /// </summary>
    internal ILFrontend LoadAndReturnFrontend(
        IReadOnlyList<MethodBase> kernels,
        ILFrontendCache? cache = null,
        BackendType backendType = BackendType.CPU)
    {
        var assemblyDir = Path.GetDirectoryName(
            kernels[0].DeclaringType?.Assembly.Location);
        var frontend = new ILFrontend(backendType, cache, assemblyDir);
        frontend.LoadMethodsInstrumented(kernels, out _);
        return frontend;
    }

    /// <summary>
    /// Logs the measured counters so a failed budget assertion can be
    /// debugged without re-running with extra instrumentation.
    /// </summary>
    internal void Log(string label, LoadMethodsTimings t) =>
        Output.WriteLine(
            $"[{label}] discovered={t.MethodsDiscovered} " +
            $"assemblies={t.AssembliesScanned} " +
            $"rawDisassemble={t.RawDisassembleCount} " +
            $"intrinsicResolve={t.IntrinsicResolveCount}");
}
