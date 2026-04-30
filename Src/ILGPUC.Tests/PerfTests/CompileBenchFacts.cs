// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompileBenchFacts.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.Metal;
using ILGPUC.Backends;
using ILGPUC.Backends.CPU;
using ILGPUC.Backends.Cuda;
using ILGPUC.Backends.Metal;
using ILGPUC.Backends.OpenCL;
using ILGPUC.Backends.ROCm;
using ILGPUC.Frontend;
using ILGPUC.Frontend.Intrinsic;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.IR.Transformations;
using ILGPUC.Tests.Kernels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.PerfTests;

/// <summary>
/// Opt-in compile-time profiling bench. Runs through the full ILGPUC
/// pipeline for a trivial vector-multiply kernel across all five backends,
/// records per-phase timings for cold and warm iterations, and writes a CSV
/// trace + JSON summary into the configured output directory.
///
/// Replaces the standalone <c>ILGPUC.CompileBench</c> exe — same workload,
/// same outputs, lives in the test project so all perf-related code is in
/// one place. Gated on <c>ILGPU_RUN_BENCH=1</c> so it never runs in the
/// regular test suite (xunit reports it as skipped).
///
/// <para>Invocation:</para>
/// <code>
/// ILGPU_RUN_BENCH=1 dotnet test ILGPUC.Tests/ILGPUC.Tests.csproj \
///     --filter CompileBench --blame-hang-timeout 360s
/// </code>
///
/// <para>Optional environment variables:</para>
/// <list type="bullet">
/// <item><c>ILGPU_BENCH_WARMUP</c> — warm-up iterations (default 3).</item>
/// <item><c>ILGPU_BENCH_ITERATIONS</c> — measured iterations (default 20).</item>
/// <item><c>ILGPU_BENCH_OUTPUT</c> — output dir (default
/// <c>$(TestAssemblyDir)/bench-output</c>).</item>
/// </list>
/// </summary>
public sealed class CompileBenchFacts(ITestOutputHelper output)
{
    private const int DefaultWarmup = 3;
    private const int DefaultIterations = 20;

    private static readonly BackendType[] AllBackends =
    [
        BackendType.CPU,
        BackendType.Cuda,
        BackendType.Metal,
        BackendType.OpenCL,
        BackendType.ROCm,
    ];

    private enum Phase
    {
        FrontendDisassemble,
        FrontendILToIR,
        ModuleSeal,
        GlobalOptimizer,
        BackendCodegen,
        CompiledKernelWrapper,
        Total,
        FE_DisassembleWalk,
        FE_LoadDebugSymbols,
        FE_AttachSequencePoints,
        FE_IntrinsicResolveSum,
        FE_RawDisassembleSum,
    }

    private readonly record struct Sample(
        int Iteration,
        BackendType Backend,
        Phase Phase,
        double Milliseconds);

    [SkippableFact]
    [Trait("Category", "CompileBench")]
    public void CompileBench_VectorMul_AllBackends()
    {
        Skip.IfNot(
            Environment.GetEnvironmentVariable("ILGPU_RUN_BENCH") == "1",
            "Set ILGPU_RUN_BENCH=1 to run the compile-time bench.");

        var warmup = ParseIntEnv("ILGPU_BENCH_WARMUP", DefaultWarmup);
        var iterations = ParseIntEnv(
            "ILGPU_BENCH_ITERATIONS", DefaultIterations);
        var outDir = Environment.GetEnvironmentVariable("ILGPU_BENCH_OUTPUT")
            ?? Path.Combine(
                Path.GetDirectoryName(typeof(CompileBenchFacts).Assembly.Location)!,
                "bench-output");
        Directory.CreateDirectory(outDir);

        output.WriteLine("ILGPUC compile-time bench");
        output.WriteLine($"  warmup iterations  : {warmup}");
        output.WriteLine($"  measured iterations: {iterations}");
        output.WriteLine($"  backends           : {string.Join(", ", AllBackends)}");
        output.WriteLine($"  output dir         : {outDir}");

        var kernel = typeof(PerfRegressionKernels)
            .GetMethod(nameof(PerfRegressionKernels.VectorMul))!;

        var setupSw = Stopwatch.StartNew();
        Intrinsics.Init();
        setupSw.Stop();
        output.WriteLine(
            $"  Intrinsics.Init()  : " +
            $"{setupSw.Elapsed.TotalMilliseconds,8:F3} ms");

        setupSw.Restart();
        var optTransformer = Optimizer.CreateTransformer(OptimizationLevel.O1);
        setupSw.Stop();
        output.WriteLine(
            $"  Optimizer.Create   : " +
            $"{setupSw.Elapsed.TotalMilliseconds,8:F3} ms");

        var backends = AllBackends.ToDictionary(bt => bt, CreateBackend);

        // Single shared frontend cache mirrors the production
        // KernelCompiler._frontendCache lifetime.
        var frontendCache = new ILFrontendCache();
        var samples = new List<Sample>(
            (warmup + iterations) * AllBackends.Length * 12);

        output.WriteLine($"Warming up ({warmup} iterations)...");
        for (int it = 0; it < warmup; it++)
            foreach (var backend in AllBackends)
                _ = RunOnce(it, kernel, backend, backends[backend],
                    optTransformer, frontendCache, recordTo: null);

        // Reset cache so iteration 0 of the measured run is the cold path.
        frontendCache = new ILFrontendCache();

        output.WriteLine($"Measuring ({iterations} iterations)...");
        for (int it = 0; it < iterations; it++)
            foreach (var backend in AllBackends)
                _ = RunOnce(it, kernel, backend, backends[backend],
                    optTransformer, frontendCache, recordTo: samples);

        PrintAggregateStats(
            "COLD — iteration 0 only (one cold compile per backend)",
            samples.Where(s => s.Iteration == 0).ToList());
        PrintAggregateStats(
            "WARM — iterations 1+ (cache fully populated)",
            samples.Where(s => s.Iteration > 0).ToList());

        var csvPath = Path.Combine(outDir, "trace.csv");
        WriteCsv(csvPath, samples);
        output.WriteLine($"Trace written: {csvPath}");

        var jsonPath = Path.Combine(outDir, "summary.json");
        WriteJson(jsonPath, samples, warmup, iterations);
        output.WriteLine($"Summary written: {jsonPath}");
    }

    private string RunOnce(
        int iteration,
        MethodInfo kernel,
        BackendType backendType,
        Backend backend,
        Transformer optTransformer,
        ILFrontendCache frontendCache,
        List<Sample>? recordTo)
    {
        var totalSw = Stopwatch.StartNew();

        var assemblyDir = Path.GetDirectoryName(
            kernel.DeclaringType?.Assembly.Location);
        var frontend = new ILFrontend(backendType, frontendCache, assemblyDir);

        var sw = Stopwatch.StartNew();
        frontend.LoadMethodsInstrumented([kernel], out var feTimings);
        sw.Stop();
        Record(recordTo, iteration, backendType,
            Phase.FrontendDisassemble, sw.Elapsed.TotalMilliseconds);
        Record(recordTo, iteration, backendType,
            Phase.FE_DisassembleWalk, feTimings.DisassembleTotalMs);
        Record(recordTo, iteration, backendType,
            Phase.FE_LoadDebugSymbols, feTimings.LoadDebugSymbolsMs);
        Record(recordTo, iteration, backendType,
            Phase.FE_AttachSequencePoints, feTimings.AttachSequencePointsMs);
        Record(recordTo, iteration, backendType,
            Phase.FE_IntrinsicResolveSum, feTimings.IntrinsicResolveMs);
        Record(recordTo, iteration, backendType,
            Phase.FE_RawDisassembleSum, feTimings.RawDisassembleMs);

        if (iteration == 0 && recordTo is not null)
        {
            output.WriteLine(
                $"  [{backendType}] discovered {feTimings.MethodsDiscovered} " +
                $"methods, scanned {feTimings.AssembliesScanned} assemblies " +
                $"(intrinsicResolve={feTimings.IntrinsicResolveCount}, " +
                $"rawDisassemble={feTimings.RawDisassembleCount})");
        }

        var typeManager = new TypeInformationManager();
        var moduleBuilder = new ModuleBuilder(
            new CompilationProperties(),
            new Generation(),
            Location.Unknown,
            typeManager);

        sw.Restart();
        frontend.GenerateCode(moduleBuilder, [kernel]);
        sw.Stop();
        Record(recordTo, iteration, backendType,
            Phase.FrontendILToIR, sw.Elapsed.TotalMilliseconds);

        var entryHandle = moduleBuilder.GetMethod(kernel).Declaration.Handle;
        sw.Restart();
        var module = moduleBuilder.Seal(entryHandle);
        sw.Stop();
        Record(recordTo, iteration, backendType,
            Phase.ModuleSeal, sw.Elapsed.TotalMilliseconds);

        var properties = new CompilationProperties();
        sw.Restart();
        var optimized = optTransformer.Apply(properties, typeManager, module);
        sw.Stop();
        Record(recordTo, iteration, backendType,
            Phase.GlobalOptimizer, sw.Elapsed.TotalMilliseconds);

        sw.Restart();
        var compiled = backend.GenerateCode(properties, typeManager, optimized);
        sw.Stop();
        Record(recordTo, iteration, backendType,
            Phase.BackendCodegen, sw.Elapsed.TotalMilliseconds);

        int simdWidth = backend is CPUBackend cpu ? cpu.VectorWidth : 8;
        sw.Restart();
        using var wrapperGen = new CompiledKernelGenerator(
            backend.CreateCompiledKernelEmitter(),
            backend.CreateLauncherEmitter(),
            optimized,
            compiled,
            Guid.NewGuid(),
            compiledBinary: null,
            kernelClassName: null,
            simdWidth: simdWidth,
            kernelName: null,
            launchParamTypeNames: null,
            launchParamFieldAccessors: null,
            indexDimOverride: null);
        var wrapperResult = wrapperGen.Generate();
        sw.Stop();
        Record(recordTo, iteration, backendType,
            Phase.CompiledKernelWrapper, sw.Elapsed.TotalMilliseconds);

        totalSw.Stop();
        Record(recordTo, iteration, backendType,
            Phase.Total, totalSw.Elapsed.TotalMilliseconds);

        return wrapperResult.SourceCode;
    }

    private static void Record(
        List<Sample>? recordTo,
        int iteration,
        BackendType backend,
        Phase phase,
        double ms) =>
        recordTo?.Add(new Sample(iteration, backend, phase, ms));

    private static Backend CreateBackend(BackendType type) => type switch
    {
        BackendType.CPU => new CPUBackend(vectorWidth: 8),
        BackendType.Cuda => new CudaBackend(
            CudaArchitecture.SM_80, CudaInstructionSet.ISA_80),
        BackendType.Metal => new MetalBackend(
            MetalTargetOS.macOS, MetalGPUFamily.Apple7),
        BackendType.OpenCL => new OpenCLIntelBackend(2, 0),
        BackendType.ROCm => new ROCmBackend("gfx1100"),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static int ParseIntEnv(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    private void PrintAggregateStats(string label, List<Sample> samples)
    {
        output.WriteLine(label);
        output.WriteLine(new string('=', 86));
        if (samples.Count == 0)
        {
            output.WriteLine("(no samples)");
            return;
        }
        output.WriteLine(
            $"{"Backend",-8} {"Phase",-22} {"Count",5}  " +
            $"{"Mean",8} {"Median",8} {"Min",8} {"Max",8} {"P95",8}");
        output.WriteLine(new string('-', 86));

        var grouped = samples
            .GroupBy(s => (s.Backend, s.Phase))
            .OrderBy(g => g.Key.Backend)
            .ThenBy(g => (int)g.Key.Phase);

        foreach (var g in grouped)
        {
            var times = g.Select(s => s.Milliseconds).OrderBy(x => x).ToArray();
            double mean = times.Average();
            double median = times[times.Length / 2];
            double min = times[0];
            double max = times[^1];
            double p95 = times[Math.Min(times.Length - 1,
                (int)(times.Length * 0.95))];
            output.WriteLine(
                $"{g.Key.Backend,-8} {g.Key.Phase,-22} {times.Length,5}  " +
                $"{mean,8:F3} {median,8:F3} {min,8:F3} {max,8:F3} " +
                $"{p95,8:F3}");
        }
    }

    private static void WriteCsv(string path, List<Sample> samples)
    {
        using var w = new StreamWriter(path);
        w.WriteLine("iteration,backend,phase,ms");
        foreach (var s in samples)
        {
            w.Write(s.Iteration.ToString(CultureInfo.InvariantCulture));
            w.Write(',');
            w.Write(s.Backend);
            w.Write(',');
            w.Write(s.Phase);
            w.Write(',');
            w.WriteLine(s.Milliseconds.ToString(
                "F4", CultureInfo.InvariantCulture));
        }
    }

    private static void WriteJson(
        string path,
        List<Sample> samples,
        int warmup,
        int iterations)
    {
        static object[] Summarize(IEnumerable<Sample> set) => set
            .GroupBy(s => (s.Backend, s.Phase))
            .OrderBy(g => g.Key.Backend)
            .ThenBy(g => (int)g.Key.Phase)
            .Select(g =>
            {
                var times = g.Select(s => s.Milliseconds)
                    .OrderBy(x => x).ToArray();
                return (object)new
                {
                    backend = g.Key.Backend.ToString(),
                    phase = g.Key.Phase.ToString(),
                    count = times.Length,
                    mean_ms = Math.Round(times.Average(), 4),
                    median_ms = Math.Round(times[times.Length / 2], 4),
                    min_ms = Math.Round(times[0], 4),
                    max_ms = Math.Round(times[^1], 4),
                    p95_ms = Math.Round(
                        times[Math.Min(times.Length - 1,
                            (int)(times.Length * 0.95))], 4),
                };
            })
            .ToArray();

        var doc = new
        {
            machine = Environment.MachineName,
            os = Environment.OSVersion.ToString(),
            cpu_count = Environment.ProcessorCount,
            timestamp = DateTimeOffset.UtcNow,
            warmup_iterations = warmup,
            measured_iterations = iterations,
            kernel = "PerfRegressionKernels.VectorMul (a[i] = b[i] * c[i])",
            summary = Summarize(samples),
            cold = Summarize(samples.Where(s => s.Iteration == 0)),
            warm = Summarize(samples.Where(s => s.Iteration > 0)),
        };

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
    }
}
