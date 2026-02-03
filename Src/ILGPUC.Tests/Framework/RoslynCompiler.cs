// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: RoslynCompiler.cs
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Compiles C# source code into assemblies using the Roslyn compiler.
/// </summary>
static class RoslynCompiler
{
    /// <summary>
    /// Compiles source code strings into a class library DLL.
    /// </summary>
    public static void CompileToLibrary(
        string[] sources,
        string outputPath,
        MetadataReference[]? additionalReferences = null) =>
        Compile(
            sources,
            outputPath,
            OutputKind.DynamicallyLinkedLibrary,
            additionalReferences);

    /// <summary>
    /// Compiles source code strings into a console application DLL.
    /// </summary>
    public static void CompileToExecutable(
        string[] sources,
        string outputPath,
        MetadataReference[]? additionalReferences = null) =>
        Compile(
            sources,
            outputPath,
            OutputKind.ConsoleApplication,
            additionalReferences);

    /// <summary>
    /// Returns MetadataReferences for the .NET runtime + ILGPU.
    /// </summary>
    public static MetadataReference[] GetDefaultReferences()
    {
        var refs = new List<MetadataReference>();

        // Core BCL assemblies from the runtime directory
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        string[] coreAssemblies =
        [
            "System.Runtime.dll",
            "System.Runtime.InteropServices.dll",
            "System.Console.dll",
            "System.Collections.dll",
            "System.Linq.dll",
            "System.Threading.dll",
            "System.Threading.Thread.dll",
            "System.Numerics.Vectors.dll",
            "System.Memory.dll",
            "netstandard.dll",
        ];

        foreach (var asm in coreAssemblies)
        {
            var path = Path.Combine(runtimeDir, asm);
            if (File.Exists(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }

        // System.Private.CoreLib (for object, int, etc.)
        refs.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

        // ILGPU
        refs.Add(MetadataReference.CreateFromFile(
            typeof(ILGPU.Index1D).Assembly.Location));

        return refs.ToArray();
    }

    /// <summary>
    /// Creates a CSharpCompilation from source strings without emitting.
    /// Used by ProgramBuilder for the Roslyn frontend rewriting pipeline.
    /// </summary>
    public static CSharpCompilation CreateCompilation(
        string[] sources,
        string assemblyName,
        OutputKind outputKind = OutputKind.ConsoleApplication,
        MetadataReference[]? additionalReferences = null)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.CSharp13);

        var syntaxTrees = sources.Select((src, i) =>
            CSharpSyntaxTree.ParseText(
                src,
                parseOptions,
                path: $"source{i}.cs")).ToArray();

        var references = GetDefaultReferences();
        if (additionalReferences is { Length: > 0 })
            references = [.. references, .. additionalReferences];

        var options = new CSharpCompilationOptions(outputKind)
            .WithOptimizationLevel(Microsoft.CodeAnalysis.OptimizationLevel.Release)
            .WithAllowUnsafe(true)
            .WithNullableContextOptions(NullableContextOptions.Enable);

        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            options);
    }

    /// <summary>
    /// Emits a CSharpCompilation to disk.
    /// </summary>
    public static void Emit(CSharpCompilation compilation, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var stream = File.Create(outputPath);
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString());
            throw new InvalidOperationException(
                $"Roslyn compilation failed:\n{string.Join('\n', errors)}");
        }
    }

    /// <summary>
    /// Compiles the given sources and additional references.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void Compile(
        string[] sources,
        string outputPath,
        OutputKind outputKind,
        MetadataReference[]? additionalReferences)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.CSharp13);

        var syntaxTrees = sources.Select((src, i) =>
            CSharpSyntaxTree.ParseText(
                src,
                parseOptions,
                path: $"source{i}.cs")).ToArray();

        var references = GetDefaultReferences();
        if (additionalReferences is { Length: > 0 })
            references = [.. references, .. additionalReferences];

        var options = new CSharpCompilationOptions(outputKind)
            .WithOptimizationLevel(Microsoft.CodeAnalysis.OptimizationLevel.Release)
            .WithAllowUnsafe(true)
            .WithNullableContextOptions(NullableContextOptions.Enable);

        var compilation = CSharpCompilation.Create(
            Path.GetFileNameWithoutExtension(outputPath),
            syntaxTrees,
            references,
            options);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var stream = File.Create(outputPath);
        var result = compilation.Emit(stream);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString());
            throw new InvalidOperationException(
                $"Roslyn compilation failed:\n{string.Join('\n', errors)}");
        }
    }
}
