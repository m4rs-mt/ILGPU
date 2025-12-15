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

    private MainKernelProvider(Dictionary<string, string> compiledSources)
    {
        _compiledSources = compiledSources;
    }

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
        // Phase 1: Rewrite with stubs so the compilation is valid
        var stubProvider = new StubCompiledKernelProvider();
        var rewriter = new CompilationRewriter(stubProvider);
        var rewriteResult = rewriter.Rewrite(compilation, analysis);

        // Emit to in-memory assembly
        var emitResult = CompilationEmitter.EmitToMemory(
            rewriteResult.Compilation,
            out var assemblyBytes,
            out _);

        if (!emitResult.Success || assemblyBytes == null)
        {
            throw new InvalidOperationException(
                "Failed to emit temp assembly for kernel resolution. Errors:\n" +
                string.Join("\n", emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString())
                    .Take(20)));
        }

        // Phase 2: Load temp assembly and resolve methods
        var tempAssembly = Assembly.Load(assemblyBytes);
        var compiledSources = new Dictionary<string, string>();
        var kernelsToCompile = new List<KernelCompileJob>();

        foreach (var kernel in analysis.UniqueKernels)
        {
            if (kernel.KernelMethod is null)
            {
                // Inline kernels can't be resolved via reflection — use stubs
                compiledSources[kernel.KernelName] =
                    stubProvider.GetCompiledKernelSource(kernel);
                continue;
            }

            // Resolve MethodInfo from the temp assembly
            var method = ResolveMethod(tempAssembly, kernel.KernelMethod);
            if (method is null)
            {
                await Console.Error.WriteLineAsync(
                    $"Warning: Could not resolve method for kernel " +
                    $"'{kernel.KernelName}', using stub.");
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

            // Compute index dimensions from the launch variant — must match
            // the stub generator and call-site rewriter so the generated
            // Launch method signature agrees with the rewritten call site.
            // Determine index dimensions. For grouped launches, check if
            // the kernel method's first parameter is an index type — if so,
            // it receives the index (dim=1); if not, it's a grouped kernel
            // without an index parameter (dim=0).
            int indexDimOverride;
            if (kernel.Variant == LaunchVariant.Grouped)
            {
                var firstParam = kernel.KernelMethod?.Parameters
                    .FirstOrDefault();
                indexDimOverride = firstParam is not null
                    && firstParam.Type.Name is "Index1D" or "LongIndex1D"
                        or "Index2D" or "LongIndex2D"
                        or "Index3D" or "LongIndex3D"
                        or "KernelIndex"
                    ? 1 : 0;
            }
            else
            {
                indexDimOverride = kernel.Variant switch
                {
                    LaunchVariant.Auto2D or LaunchVariant.AutoLong2D
                        or LaunchVariant.Auto2DStride
                        or LaunchVariant.AutoLong2DStride => 2,
                    LaunchVariant.Auto3D or LaunchVariant.AutoLong3D
                        or LaunchVariant.Auto3DStride
                        or LaunchVariant.AutoLong3DStride => 3,
                    _ => 1,
                };
            }

            kernelsToCompile.Add(new KernelCompileJob(
                kernel.KernelName, method, paramTypeNames, fieldAccessors,
                indexDimOverride));
        }

        if (parallel)
        {
            var tasks =
                from k in kernelsToCompile
                select CompileAllBackendsAsync(
                    k, backends, compiler, compilerManager, dump, ct);
            foreach (var (name, source) in await Task.WhenAll(tasks).ConfigureAwait(false))
                compiledSources[name] = source;
        }
        else
        {
            foreach (var job in kernelsToCompile)
            {
                var (_, source) = await CompileAllBackendsAsync(
                    job, backends, compiler, compilerManager, dump, ct);
                compiledSources[job.Name] = source;
            }
        }

        return new MainKernelProvider(compiledSources);
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
        int IndexDimOverride);

    /// <summary>
    /// Returns the pre-compiled C# source for <paramref name="kernel"/>.
    /// </summary>
    /// <remarks>
    /// If the kernel was not compiled (e.g., an inline lambda that could not be
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
    /// Compiles a single kernel for all backends and returns the concatenated source.
    /// </summary>
    private static async Task<(string Name, string Source)> CompileAllBackendsAsync(
        KernelCompileJob job,
        Backend[] backends,
        KernelCompiler compiler,
        ICompilerManager? compilerManager,
        IRDumpSettings dump,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        foreach (var backend in backends)
        {
            ct.ThrowIfCancellationRequested();
            var result = await compiler
                .CompileKernelAsync(
                    job.Method,
                    backend,
                    compilerManager,
                    dump,
                    job.LaunchParamTypeNames,
                    job.LaunchParamFieldAccessors,
                    job.Name,
                    job.IndexDimOverride,
                    ct)
                .ConfigureAwait(false);
            sb.AppendLine(result.SourceCode);
        }
        return (job.Name, sb.ToString());
    }

    /// <summary>
    /// Resolves a MethodInfo from a loaded assembly using Roslyn symbol info.
    /// For generic methods (e.g. <c>Kernel&lt;LambdaClosure, long&gt;</c>),
    /// the open generic definition is specialized with concrete type arguments.
    /// </summary>
    private static MethodInfo? ResolveMethod(
        Assembly assembly,
        IMethodSymbol symbol)
    {
        var typeName = symbol.ContainingType.ToDisplayString();
        var type = assembly.GetType(typeName);
        if (type == null)
            return null;

        var method = type.GetMethod(
            symbol.Name,
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance);

        if (method is null)
            return null;

        // Specialize open generic methods with concrete type arguments
        if (symbol.IsGenericMethod && method.IsGenericMethodDefinition)
        {
            var typeArgs = new Type[symbol.TypeArguments.Length];
            for (int i = 0; i < typeArgs.Length; i++)
            {
                var resolved = ResolveTypeSymbol(assembly, symbol.TypeArguments[i]);
                if (resolved is null)
                    return null;
                typeArgs[i] = resolved;
            }
            method = method.MakeGenericMethod(typeArgs);
        }

        return method;
    }

    /// <summary>
    /// Resolves a Roslyn <see cref="ITypeSymbol"/> to a runtime <see cref="Type"/>
    /// using the temp assembly and well-known system types.
    /// </summary>
    private static Type? ResolveTypeSymbol(Assembly assembly, ITypeSymbol symbol)
    {
        // Handle well-known primitive / system types
        var specialType = symbol.SpecialType;
        if (specialType != SpecialType.None)
        {
            return specialType switch
            {
                SpecialType.System_Boolean => typeof(bool),
                SpecialType.System_Byte => typeof(byte),
                SpecialType.System_SByte => typeof(sbyte),
                SpecialType.System_Int16 => typeof(short),
                SpecialType.System_UInt16 => typeof(ushort),
                SpecialType.System_Int32 => typeof(int),
                SpecialType.System_UInt32 => typeof(uint),
                SpecialType.System_Int64 => typeof(long),
                SpecialType.System_UInt64 => typeof(ulong),
                SpecialType.System_Single => typeof(float),
                SpecialType.System_Double => typeof(double),
                SpecialType.System_String => typeof(string),
                SpecialType.System_Object => typeof(object),
                _ => Type.GetType(symbol.ToDisplayString())
            };
        }

        // Try the temp assembly first, then fallback to Type.GetType
        var fullName = symbol.ToDisplayString();
        return assembly.GetType(fullName)
            ?? Type.GetType(fullName);
    }
}
