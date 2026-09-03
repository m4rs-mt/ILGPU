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
        if (analysis.Diagnostics.Count > 0)
        {
            throw new InvalidOperationException(
                "Kernel launch sites have errors:\n" +
                string.Join("\n", analysis.Diagnostics));
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
            // Resolve MethodInfo from the temp assembly
            var method = ResolveKernelMethod(tempAssembly, kernel);
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

            // The launch must match the stub generator and call-site
            // rewriter so the generated Launch signature agrees with the
            // rewritten call site.
            kernelsToCompile.Add(new KernelCompileJob(
                kernel.KernelName, method, paramTypeNames, fieldAccessors,
                kernel.GetEntryPointLaunch()));
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
        EntryPointLaunch Launch);

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
                    job.Launch,
                    ct)
                .ConfigureAwait(false);
            sb.AppendLine(result.SourceCode);
        }
        return (job.Name, sb.ToString());
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
        if (kernel.KernelMethod is { } symbol)
            return ResolveMethod(assembly, symbol);
        if (kernel.LiftedKernel is null)
            return null;

        // Lifted kernel names are unique within the compilation
        return FindLocalFunction(assembly.GetTypes(), owner: null, kernel.KernelName);
    }

    /// <summary>
    /// Finds a compiled local function in the given types. The C# compiler
    /// emits local functions as methods named
    /// <c>&lt;Owner&gt;g__Name|i_j</c>, where <c>Owner</c> is the member
    /// declaring them.
    /// </summary>
    /// <param name="types">The types to search.</param>
    /// <param name="owner">
    /// The name of the declaring member, or <see langword="null"/> to accept
    /// any member.
    /// </param>
    /// <param name="name">The name of the local function.</param>
    /// <returns>The method if exactly one matches; otherwise null.</returns>
    private static MethodInfo? FindLocalFunction(
        IEnumerable<Type> types,
        string? owner,
        string name)
    {
        var marker = $">g__{name}|";
        var prefix = owner is null ? "<" : $"<{owner}>";
        var matches = types
            .SelectMany(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.DeclaredOnly))
            .Where(m => m.Name.StartsWith(prefix, StringComparison.Ordinal)
                && m.Name.Contains(marker, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
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

        MethodInfo? method;
        if (symbol.MethodKind == MethodKind.LocalFunction)
        {
            // Local functions are emitted under mangled names on the type
            // (or a closure type nested in it) containing their owner
            var owner = symbol.ContainingSymbol;
            while (owner is IMethodSymbol
                {
                    MethodKind: MethodKind.LocalFunction
                        or MethodKind.AnonymousFunction,
                })
            {
                owner = owner.ContainingSymbol;
            }
            var types = assembly.GetTypes()
                .Where(t => t == type || IsNestedIn(t, type));
            method = FindLocalFunction(types, owner.Name, symbol.Name);
        }
        else
        {
            method = type.GetMethod(
                symbol.Name,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance);
        }

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
    /// Returns <see langword="true"/> if <paramref name="type"/> is nested,
    /// directly or transitively, in <paramref name="outer"/>.
    /// </summary>
    private static bool IsNestedIn(Type type, Type outer)
    {
        for (var current = type.DeclaringType; current != null;
            current = current.DeclaringType)
        {
            if (current == outer)
                return true;
        }
        return false;
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
