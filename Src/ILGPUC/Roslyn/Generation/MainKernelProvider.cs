// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MainKernelProvider.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.Backends;
using ILGPUC.Compilers;
using ILGPUC.Roslyn.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ILGPUC.Roslyn.Generation;

/// <summary>
/// An <see cref="ICompiledKernelProvider"/> that bridges the Roslyn frontend
/// to the ILGPUC compilation pipeline using a two-phase approach:
/// <list type="number">
/// <item>Compile with stubs -> temp in-memory assembly</item>
/// <item>Resolve MethodInfos from temp assembly -> KernelCompiler -> real code</item>
/// </list>
/// </summary>
sealed class MainKernelProvider : ICompiledKernelProvider
{
    private readonly Dictionary<string, string> _compiledSources;

    private MainKernelProvider(
        Dictionary<string, string> compiledSources,
        List<BuildDiagnostic> diagnostics,
        List<string> skippedKernels)
    {
        _compiledSources = compiledSources;
        Diagnostics = diagnostics;
        SkippedKernels = skippedKernels;
    }

    /// <summary>
    /// Returns the names of the kernel instantiations that were skipped because
    /// they throw for their type arguments. They must be removed from the
    /// analysis (<see cref="AnalysisResult.WithoutKernels"/>) before rewriting.
    /// </summary>
    public IReadOnlyList<string> SkippedKernels { get; }

    /// <summary>
    /// Returns the warnings found while resolving and compiling kernels.
    /// </summary>
    public IReadOnlyList<BuildDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Creates an <see cref="MainKernelProvider"/> by performing the two-phase
    /// compilation: stubs -> temp assembly -> resolve methods -> real compiled kernels.
    /// </summary>
    /// <param name="compilation">The Roslyn compilation of user source files.</param>
    /// <param name="analysis">Analysis results containing kernel descriptors.</param>
    /// <param name="backends">Backend instances to compile for.</param>
    /// <param name="compiler">The ILGPUC kernel compiler facade.</param>
    /// <param name="compilerManager">
    /// Optional compiler manager for binary compilation.
    /// </param>
    /// <param name="parallel">
    /// When <see langword="true"/>, all unique kernels are compiled concurrently.
    /// </param>
    /// <param name="dump">IR dump settings; pass <see langword="default"/> to skip.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A provider with real compiled kernel sources.</returns>
    internal static async Task<MainKernelProvider> CreateAsync(
        CSharpCompilation compilation,
        AnalysisResult analysis,
        Backend[] backends,
        KernelCompiler compiler,
        ICompilerManager? compilerManager = null,
        bool parallel = false,
        IRDumpSettings dump = default,
        CancellationToken ct = default)
    {
        if (analysis.HasErrors)
        {
            throw new InvalidOperationException(
                "Kernel launch sites have errors:\n" +
                string.Join("\n", analysis.Diagnostics.Where(d => d.IsError)));
        }

        // Phase 1: Rewrite with stubs so the compilation is valid, lifting
        // inline lambda kernels into methods that can be resolved below
        var stubProvider = new StubCompiledKernelProvider();
        var rewriter = new CompilationRewriter(stubProvider);
        var rewriteResult = rewriter.Rewrite(
            compilation, analysis, liftInlineKernels: true);

        // Emit to in-memory assembly
        var emitResult = CompilationEmitter.EmitToMemory(
            rewriteResult.Compilation,
            out var assemblyBytes,
            out var pdbBytes);

        if (!emitResult.Success || assemblyBytes == null || pdbBytes == null)
        {
            throw new StubCompilationException(
                [.. emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)]);
        }

        // Phase 2: Load temp assembly and resolve methods
        var tempAssembly = Assembly.Load(assemblyBytes);

        // Errors in kernel code are reported at their source locations, which
        // come from the debug information of the temp assembly
        compiler.RegisterDebugInformation(tempAssembly, pdbBytes);
        var compiledSources = new Dictionary<string, string>();
        var kernelsToCompile = new List<KernelCompileJob>();
        var diagnostics = new List<BuildDiagnostic>();

        foreach (var kernel in analysis.UniqueKernels)
        {
            // Resolve MethodInfo from the temp assembly
            var method = ResolveKernelMethod(tempAssembly, kernel);
            if (method is null && kernel.LiftedKernel is not null)
            {
                // Lifted inline kernels are emitted by the rewrite above, so a
                // stub would only hide that the kernel was not compiled
                throw new InvalidOperationException(
                    $"Could not resolve the lifted inline kernel " +
                    $"'{kernel.KernelName}'.");
            }
            if (method is null)
            {
                diagnostics.Add(BuildDiagnostic.Warning(
                    BuildDiagnosticCodes.UnresolvedKernel,
                    kernel.LaunchInfo.Invocation.GetLocation(),
                    $"Could not resolve kernel method " +
                    $"'{kernel.KernelMethod?.ToDisplayString() ?? kernel.KernelName}' " +
                    $"in the compiled " +
                    $"assembly, so no kernel is compiled for it and launching it " +
                    $"throws a NotSupportedException."));
                compiledSources[kernel.KernelName] =
                    stubProvider.GetCompiledKernelSource(kernel);
                continue;
            }

            // Extract user-facing parameter type names + field accessors from
            // the Roslyn analysis. Without these, CompiledKernelGenerator
            // falls back to synthetic Struct_{id} names that are never
            // declared, causing CS0246 in the generated source.
            var paramTypeNames = kernel.Parameters
                .Select(p => LauncherStubGenerator.FormatTypeNamePublic(p.Type))
                .ToArray();
            var fieldAccessors = kernel.Parameters
                .Select(p => LauncherStubGenerator.ExtractFieldAccessors(p.Type))
                .ToArray();

            // The launch must match the stub generator and call-site
            // rewriter so the generated Launch signature agrees with the
            // rewritten call site.
            kernelsToCompile.Add(new KernelCompileJob(
                kernel.KernelName, method, paramTypeNames, fieldAccessors,
                kernel.GetEntryPointLaunch(), kernel));
        }

        var skippedKernels = new List<string>();
        var reportedWarnings = new HashSet<string>();
        void AddResult(
            KernelCompileJob job,
            string? source,
            Exception? skipReason,
            IReadOnlyList<BuildDiagnostic> warnings)
        {
            // Warnings about a helper are reported once, although every kernel
            // calling it is compiled for every backend
            foreach (var warning in warnings)
            {
                if (reportedWarnings.Add(warning.ToString()))
                    diagnostics.Add(warning);
            }

            if (source is not null)
            {
                compiledSources[job.Name] = source;
                return;
            }
            skippedKernels.Add(job.Name);
            diagnostics.Add(CreateSkippedDiagnostic(job, skipReason!));
        }

        if (parallel)
        {
            var tasks =
                from k in kernelsToCompile
                select CompileOrSkipAsync(
                    k, backends, compiler, compilerManager, dump, ct);
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            for (int i = 0; i < results.Length; ++i)
            {
                AddResult(
                    kernelsToCompile[i],
                    results[i].Source,
                    results[i].Skip,
                    results[i].Warnings);
            }
        }
        else
        {
            foreach (var job in kernelsToCompile)
            {
                var (source, skip, warnings) = await CompileOrSkipAsync(
                    job, backends, compiler, compilerManager, dump, ct);
                AddResult(job, source, skip, warnings);
            }
        }

        return new MainKernelProvider(compiledSources, diagnostics, skippedKernels);
    }

    /// <summary>
    /// One kernel's compile inputs: the resolved <see cref="MethodInfo"/>
    /// plus the Roslyn-derived launch-side parameter metadata that has to
    /// be threaded through to <see cref="CompiledKernelGenerator"/>.
    /// </summary>
    private readonly record struct KernelCompileJob(
        string Name,
        MethodInfo Method,
        string[]? LaunchParamTypeNames,
        string[]?[]? LaunchParamFieldAccessors,
        EntryPointLaunch Launch,
        KernelDescriptor Kernel);

    /// <summary>
    /// Returns the pre-compiled C# source for <paramref name="kernel"/>.
    /// </summary>
    /// <remarks>
    /// If the kernel was not compiled (e.g., a method that could not be
    /// resolved via reflection), falls back to a generated stub so the rewritten
    /// code still compiles.
    /// </remarks>
    public string GetCompiledKernelSource(KernelDescriptor kernel)
    {
        if (_compiledSources.TryGetValue(kernel.KernelName, out var source))
            return source;

        // Fallback to stub if somehow missing
        return LauncherStubGenerator.GenerateStub(kernel);
    }

    /// <summary>
    /// Compiles a kernel, or reports that it is skipped: a kernel instantiation
    /// that was only found through a generic wrapper and fails because an
    /// exception is reachable for its type arguments is not compiled. Launching it
    /// with these type arguments throws at run time, as the same code would on
    /// the CPU. Direct launches and all other errors still fail the build.
    /// </summary>
    private static async Task<(
        string? Source,
        Exception? Skip,
        IReadOnlyList<BuildDiagnostic> Warnings)> CompileOrSkipAsync(
        KernelCompileJob job,
        Backend[] backends,
        KernelCompiler compiler,
        ICompilerManager? compilerManager,
        IRDumpSettings dump,
        CancellationToken ct)
    {
        try
        {
            var (_, source, warnings) = await CompileAllBackendsAsync(
                job, backends, compiler, compilerManager, dump, ct)
                .ConfigureAwait(false);
            return (source, null, warnings);
        }
        catch (Exception e) when (
            job.Kernel.ConcreteTypeArgs is not null &&
            Frontend.ReachableThrowException.IsCausedBy(e))
        {
            return (null, e, []);
        }
    }

    /// <summary>
    /// Creates the warning for a skipped kernel instantiation.
    /// </summary>
    private static BuildDiagnostic CreateSkippedDiagnostic(
        KernelCompileJob job,
        Exception reason)
    {
        var innermost = reason;
        while (innermost.InnerException is not null)
            innermost = innermost.InnerException;
        return BuildDiagnostic.Warning(
            BuildDiagnosticCodes.SkippedThrowingInstantiation,
            job.Kernel.LaunchInfo.Invocation.GetLocation(),
            $"The kernel instantiation " +
            $"'{job.Kernel.KernelMethod?.ToDisplayString() ?? job.Name}' is " +
            $"reached through " +
            $"generic code, but throws an exception for these type arguments, so it " +
            $"is not compiled and launching it with them throws a " +
            $"NotSupportedException. {innermost.Message}");
    }

    /// <summary>
    /// Compiles a single kernel for all backends and returns the concatenated source
    /// and the warnings about its code.
    /// </summary>
    private static async Task<(
        string Name,
        string Source,
        IReadOnlyList<BuildDiagnostic> Warnings)> CompileAllBackendsAsync(
        KernelCompileJob job,
        Backend[] backends,
        KernelCompiler compiler,
        ICompilerManager? compilerManager,
        IRDumpSettings dump,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        var warnings = new List<BuildDiagnostic>();
        var launchSite = job.Kernel.LaunchInfo.Invocation.GetLocation();
        foreach (var backend in backends)
        {
            ct.ThrowIfCancellationRequested();
            KernelCompilationResult result;
            try
            {
                result = await compiler
                    .CompileKernelAsync(
                        job.Method,
                        backend,
                        compilerManager,
                        dump,
                        job.LaunchParamTypeNames,
                        job.LaunchParamFieldAccessors,
                        job.Name,
                        job.Launch,
                        ct)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                KernelCompilationErrors.TryCreateDiagnostic(
                    exception,
                    launchSite,
                    out var diagnostic))
            {
                // Reported as a build error at the offending source location
                throw new KernelDiagnosticException(diagnostic!, exception);
            }
            sb.AppendLine(result.SourceCode);
            foreach (var warning in result.Warnings)
                warnings.Add(KernelCompilationErrors.CreateWarning(warning, launchSite));
        }
        return (job.Name, sb.ToString(), warnings);
    }

    /// <summary>
    /// Resolves the method to compile for <paramref name="kernel"/> from an
    /// assembly emitted from a compilation rewritten with lifted inline
    /// kernels: the named kernel method, or the static local function an
    /// inline lambda was lifted into.
    /// </summary>
    internal static MethodInfo? ResolveKernelMethod(
        Assembly assembly,
        KernelDescriptor kernel)
    {
        // Specializations of local functions in generic code keep their
        // definition, and lifted kernels are not symbols of the compilation
        if (kernel.KernelMethod is { } symbol &&
            (kernel.ConcreteTypeArgs is null || !kernel.IsLocalKernel))
        {
            return RuntimeSymbolResolver.ResolveMethod(assembly, symbol);
        }
        var definition = kernel.GenericDefinition;
        var name = kernel.KernelMethod?.Name ?? kernel.LiftedKernel?.Identifier.ValueText;
        if (definition is null || name is null)
            return null;
        var method = RuntimeSymbolResolver.ResolveLocalFunction(
            assembly,
            definition,
            name,
            kernel.ConcreteTypeArgs ?? kernel.GetTypeArguments());
        if (kernel.KernelMethod is not null)
            return method;

        // The lifted kernel must not capture anything, which would add a
        // closure parameter to the index and the captured values.
        return method is { IsStatic: true }
            && method.GetParameters().Length == kernel.Parameters.Length + 1
            ? method
            : null;
    }
}

/// <summary>
/// Thrown when the project, rewritten with stub kernels, does not compile, so
/// that the kernels cannot be resolved.
/// </summary>
sealed class StubCompilationException : InvalidOperationException
{
    /// <summary>
    /// The number of compiler errors reported as their own diagnostics.
    /// </summary>
    private const int MaxReportedErrors = 5;

    /// <summary>
    /// Creates an exception without compiler errors.
    /// </summary>
    public StubCompilationException()
        : this([])
    { }

    /// <summary>
    /// Creates an exception without compiler errors.
    /// </summary>
    public StubCompilationException(string message)
        : base(message)
    { }

    /// <summary>
    /// Creates an exception without compiler errors.
    /// </summary>
    public StubCompilationException(string message, Exception innerException)
        : base(message, innerException)
    { }

    /// <summary>
    /// Creates an exception for the given compiler errors.
    /// </summary>
    public StubCompilationException(IReadOnlyList<Diagnostic> errors)
        : base(
            "Failed to emit temp assembly for kernel resolution. Errors:\n" +
            string.Join("\n", errors.Take(20)))
    {
        Errors = errors;
    }

    /// <summary>
    /// Returns the compiler errors.
    /// </summary>
    public IReadOnlyList<Diagnostic> Errors { get; } = [];

    /// <summary>
    /// Converts the compiler errors into build errors: the first few with their
    /// locations, and a summary that explains the likely causes.
    /// </summary>
    public IEnumerable<BuildDiagnostic> ToBuildDiagnostics()
    {
        foreach (var error in Errors.Take(MaxReportedErrors))
        {
            yield return BuildDiagnostic.Error(
                BuildDiagnosticCodes.CompilationErrors,
                error.Location,
                $"{error.Id}: {error.GetMessage(CultureInfo.InvariantCulture)}");
        }
        yield return BuildDiagnostic.Error(
            BuildDiagnosticCodes.CompilationErrors,
            null,
            $"ILGPU could not compile the project to find its kernels " +
            $"({Errors.Count} error(s)). If the C# compiler builds the project " +
            $"without these errors, ILGPU sees it differently: check that its " +
            $"C# version supports the language features used (ILGPU supports up " +
            $"to C# {LanguageVersion.Latest.MapSpecifiedToEffectiveVersion().ToDisplayString()}), " +
            $"and look for warnings about source generators (ILGPU1009).");
    }
}
