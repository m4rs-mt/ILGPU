// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilePerfRegressionTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Frontend;
using ILGPUC.Tests.Kernels;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.PerfTests;

/// <summary>
/// Compile-time performance regression test. Asserts that the frontend
/// disassembly + transitive walk for a small set of representative kernels
/// stays within deterministic, workload-derived counter budgets exposed by
/// <see cref="LoadMethodsTimings"/>. These counters are the *cause* of the
/// wall-clock numbers we care about: locking them in catches every
/// regression that matters, while remaining immune to CI hardware variance.
///
/// Baselines were established after round-3 lazy walk
/// (commit <c>7fe20cf8e</c>): the trivial <c>VectorMul</c> kernel discovers
/// ~22 methods (down from ~1720 pre-round-3). Budgets carry ~30% headroom
/// so trivial reorderings don't churn the baseline.
///
/// To update a budget: run the test, copy the actual counter from the
/// failure message into the corresponding constant, and justify the bump in
/// the PR description.
/// </summary>
public sealed class CompilePerfRegressionTests(ITestOutputHelper output)
    : PerfTestBase(output)
{
    // ---- Budgets for the trivial VectorMul kernel ----
    // Today (post-round-3): 22 methods, 3 assemblies. Headroom to 30 / 6.
    //
    // Only per-instance counters are asserted on. The Raw/Intrinsic
    // counters in LoadMethodsTimings come from process-wide static
    // accumulators (see ILFrontend.cs s_collectSubCounters comment) which
    // cross-contaminate when xunit runs tests in parallel. Methods-
    // discovered and assemblies-scanned are populated from the frontend
    // instance's own _methods dict, so they are deterministic regardless
    // of test concurrency. Methods-discovered is also the cause-of-perf
    // metric we care about (1720 pre-round-3, 22 post) — locking it in
    // is sufficient to catch every regression that matters.
    private const int MethodsDiscoveredBudget_VectorMul = 30;
    private const int AssembliesScannedBudget_VectorMul = 6;

    // ---- Budget for the 8-deep remap chain kernel ----
    // Adds 8 helper layers + XMath.Abs + a few incidentals over VectorMul.
    private const int MethodsDiscoveredBudget_DeepChain = 45;

    // ---- Budget for the cache-reuse scenario ----
    // After warming the cache with VectorMul, compiling VectorAdd should
    // not grow the discovered-methods set beyond a small delta (just the
    // new entry kernel, since all transitive bodies are already in the
    // cache). MethodsDiscovered is per-instance, so unlike RawDisassemble
    // counters it survives parallel test execution.
    private const int MethodsDiscoveredDelta_CacheWarm = 3;

    [Fact]
    public void VectorMul_DiscoversMinimalMethods()
    {
        var kernel = typeof(PerfRegressionKernels)
            .GetMethod(nameof(PerfRegressionKernels.VectorMul))!;

        var t = LoadAndMeasure([kernel]);
        Log("VectorMul", t);

        Assert.True(
            t.MethodsDiscovered <= MethodsDiscoveredBudget_VectorMul,
            $"MethodsDiscovered={t.MethodsDiscovered}, " +
            $"budget={MethodsDiscoveredBudget_VectorMul}");
        Assert.True(
            t.AssembliesScanned <= AssembliesScannedBudget_VectorMul,
            $"AssembliesScanned={t.AssembliesScanned}, " +
            $"budget={AssembliesScannedBudget_VectorMul}");
    }

    [Fact]
    public void DeepChainAbs_DiscoversBoundedMethods()
    {
        var kernel = typeof(DeepCallStackKernels)
            .GetMethod(nameof(DeepCallStackKernels.DeepChainAbsKernel))!;

        var t = LoadAndMeasure([kernel]);
        Log("DeepChainAbs", t);

        // If Path A starts re-walking BCL transitively from Math.Abs (the
        // pre-round-3 failure mode), MethodsDiscovered would balloon into
        // the thousands. The 45 budget catches that with comfortable
        // headroom for the legitimate chain (8 layers + XMath.Abs + a few
        // incidentals) above the VectorMul baseline.
        Assert.True(
            t.MethodsDiscovered <= MethodsDiscoveredBudget_DeepChain,
            $"MethodsDiscovered={t.MethodsDiscovered}, " +
            $"budget={MethodsDiscoveredBudget_DeepChain}");
    }

    [Fact]
    public void CacheReuse_SecondCompileGrowsCacheMinimally()
    {
        var first = typeof(PerfRegressionKernels)
            .GetMethod(nameof(PerfRegressionKernels.VectorMul))!;
        var second = typeof(PerfRegressionKernels)
            .GetMethod(nameof(PerfRegressionKernels.VectorAdd))!;

        var cache = new ILFrontendCache();
        _ = LoadAndMeasure([first], cache);
        var afterFirst = cache.Disassembly.Count;
        _ = LoadAndMeasure([second], cache);
        var afterSecond = cache.Disassembly.Count;
        var delta = afterSecond - afterFirst;
        Output.WriteLine(
            $"CacheReuse: cache size {afterFirst} -> {afterSecond} " +
            $"(delta={delta})");

        // After warming the cache with VectorMul, compiling VectorAdd
        // should add only a tiny number of new entries — the new entry
        // method itself, and possibly a body or two unique to VectorAdd.
        // If a future change silently bypasses the cache (e.g. wrong key,
        // hash mismatch, premature eviction), the delta jumps to the
        // VectorMul total (~20) and this assertion fires.
        Assert.True(
            delta <= MethodsDiscoveredDelta_CacheWarm,
            $"second-compile cache-grow delta={delta}, " +
            $"budget={MethodsDiscoveredDelta_CacheWarm} " +
            $"(first-compile cache size was {afterFirst})");
    }

    [Fact]
    public void Walkability_ClassifiesBclAsNonWalkable()
    {
        var kernel = typeof(PerfRegressionKernels)
            .GetMethod(nameof(PerfRegressionKernels.VectorMul))!;

        var frontend = LoadAndReturnFrontend([kernel]);

        var field = typeof(ILFrontend).GetField(
            "_entryWalkableNames",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var walkable = (HashSet<string>?)field!.GetValue(frontend);
        Assert.NotNull(walkable);

        Output.WriteLine(
            $"walkable assemblies: {string.Join(", ", walkable!)}");

        // ILGPU must be walkable (intrinsic discovery depends on it).
        Assert.Contains("ILGPU", walkable);

        // BCL must NOT be walkable. Catches a regression where the
        // classifier rules in ComputeEntryWalkableNames /
        // ILFrontendCache.IsBclAssemblyName widen accidentally — the
        // VectorMul counter test catches gate-bypass regressions, this
        // catches classifier-widening regressions, and together they
        // pin both ends of the lazy-walk contract.
        Assert.DoesNotContain("System.Private.CoreLib", walkable);
        Assert.DoesNotContain("System.Runtime", walkable);
    }
}
