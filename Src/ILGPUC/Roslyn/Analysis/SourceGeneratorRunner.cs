// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SourceGeneratorRunner.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Threading;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// The compilation after running the project's source generators.
/// </summary>
/// <param name="Compilation">The compilation including the generated code.</param>
/// <param name="GeneratedTrees">The syntax trees the generators added.</param>
/// <param name="Diagnostics">Warnings about generators that failed.</param>
sealed record SourceGeneratorResult(
    CSharpCompilation Compilation,
    IReadOnlySet<SyntaxTree> GeneratedTrees,
    IReadOnlyList<BuildDiagnostic> Diagnostics);

/// <summary>
/// Runs the source generators of a project over the compilation that ILGPUC builds,
/// so that the analysis sees the same code as the C# compiler.
/// </summary>
/// <remarks>
/// Only generators run; diagnostic analyzers are ignored, and assemblies without
/// generators are never loaded. Generator assemblies are loaded into their own
/// load context in which the Roslyn assemblies resolve to the copies ILGPUC uses,
/// even if a generator was built against a newer Roslyn: generators rarely depend
/// on new compiler APIs, and one that does fails when it runs. A generator that
/// cannot be loaded or that throws is reported as a warning, and the build
/// continues without its output.
/// </remarks>
static class SourceGeneratorRunner
{
    #region Nested Types

    /// <summary>
    /// Loads generator assemblies and their dependencies from their directories,
    /// sharing the Roslyn assemblies with ILGPUC.
    /// </summary>
    private sealed class GeneratorLoadContext()
        : AssemblyLoadContext("ILGPUC source generators")
    {
        private readonly ConcurrentDictionary<string, byte> _directories = new();

        /// <summary>
        /// Registers the directory of an assembly as a place to find dependencies.
        /// </summary>
        public void AddDirectory(string assemblyPath)
        {
            if (Path.GetDirectoryName(assemblyPath) is { } directory)
                _directories.TryAdd(directory, 0);
        }

        /// <inheritdoc/>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var name = assemblyName.Name;
            if (name is null)
                return null;

            // Generators must see the Roslyn types of the driver that runs them
            if (IsShared(name))
                return Default.LoadFromAssemblyName(new AssemblyName(name));

            foreach (var directory in _directories.Keys)
            {
                var path = Path.Combine(directory, name + ".dll");
                if (File.Exists(path))
                    return LoadFromAssemblyPath(path);
            }
            return null;
        }

        /// <summary>
        /// Returns true for assemblies whose types generators exchange with the
        /// driver.
        /// </summary>
        private static bool IsShared(string name) =>
            name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) ||
            name is "System.Collections.Immutable" or "System.Reflection.Metadata";
    }

    /// <summary>
    /// Analyzer config options backed by a dictionary.
    /// </summary>
    private sealed class DictionaryOptions(ImmutableDictionary<string, string> options)
        : AnalyzerConfigOptions
    {
        /// <inheritdoc/>
        public override bool TryGetValue(string key, out string value) =>
            options.TryGetValue(key, out value!);

        /// <inheritdoc/>
        public override IEnumerable<string> Keys => options.Keys;
    }

    /// <summary>
    /// Provides the options of <c>.editorconfig</c> and <c>.globalconfig</c> files,
    /// including the MSBuild properties that the SDK exposes to generators.
    /// </summary>
    private sealed class ConfigOptionsProvider(AnalyzerConfigSet configs)
        : AnalyzerConfigOptionsProvider
    {
        private readonly ConcurrentDictionary<string, AnalyzerConfigOptions> _cache =
            new(StringComparer.Ordinal);

        /// <inheritdoc/>
        public override AnalyzerConfigOptions GlobalOptions { get; } =
            new DictionaryOptions(configs.GlobalConfigOptions.AnalyzerOptions);

        /// <inheritdoc/>
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
            GetOptions(tree.FilePath);

        /// <inheritdoc/>
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
            GetOptions(textFile.Path);

        /// <summary>
        /// Returns the options that apply to the file at <paramref name="path"/>.
        /// </summary>
        private AnalyzerConfigOptions GetOptions(string path) =>
            _cache.GetOrAdd(path, p => new DictionaryOptions(
                configs.GetOptionsForSourcePath(p).AnalyzerOptions));
    }

    /// <summary>
    /// An additional file that a generator may read.
    /// </summary>
    private sealed class FileText(string path) : AdditionalText
    {
        /// <inheritdoc/>
        public override string Path => path;

        /// <inheritdoc/>
        public override SourceText? GetText(
            CancellationToken cancellationToken = default) =>
            File.Exists(path) ? SourceText.From(File.ReadAllText(path)) : null;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Runs the source generators found in <paramref name="analyzerPaths"/>.
    /// </summary>
    /// <param name="compilation">The compilation of the project's sources.</param>
    /// <param name="parseOptions">The parse options of the project.</param>
    /// <param name="analyzerPaths">The analyzer assemblies of the project.</param>
    /// <param name="additionalFiles">The additional files of the project.</param>
    /// <param name="analyzerConfigFiles">
    /// The <c>.editorconfig</c> and <c>.globalconfig</c> files of the project.
    /// </param>
    /// <param name="cancellationToken">Cancels the generators.</param>
    public static SourceGeneratorResult Run(
        CSharpCompilation compilation,
        CSharpParseOptions parseOptions,
        IReadOnlyList<string> analyzerPaths,
        IReadOnlyList<string> additionalFiles,
        IReadOnlyList<string> analyzerConfigFiles,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<BuildDiagnostic>();
        var paths = analyzerPaths
            .Distinct(StringComparer.Ordinal)
            .Where(p => File.Exists(p) && ContainsGenerators(p))
            .ToList();
        var context = new GeneratorLoadContext();
        foreach (var path in analyzerPaths)
            context.AddDirectory(path);

        var generators = new List<ISourceGenerator>();
        foreach (var path in paths)
        {
            try
            {
                generators.AddRange(LoadGenerators(context.LoadFromAssemblyPath(path)));
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031
            {
                // A generator that cannot be loaded must not fail the build
                diagnostics.Add(CreateFailure(path, e.Message));
            }
        }
        if (generators.Count == 0)
            return new(compilation, new HashSet<SyntaxTree>(), diagnostics);

        var configs = analyzerConfigFiles
            .Where(File.Exists)
            .Select(p => AnalyzerConfig.Parse(SourceText.From(File.ReadAllText(p)), p))
            .ToList();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators,
            additionalFiles.Select(p => (AdditionalText)new FileText(p)),
            parseOptions,
            new ConfigOptionsProvider(AnalyzerConfigSet.Create(configs)));
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var output,
            out _,
            cancellationToken);

        foreach (var result in driver.GetRunResult().Results)
        {
            if (result.Exception is { } exception)
            {
                var name = result.Generator.GetGeneratorType().FullName ?? "generator";
                diagnostics.Add(CreateFailure(name, exception.Message));
            }
        }

        var original = compilation.SyntaxTrees.ToHashSet();
        var generated = output.SyntaxTrees.Where(t => !original.Contains(t)).ToHashSet();
        return new((CSharpCompilation)output, generated, diagnostics);
    }

    /// <summary>
    /// Returns true if the assembly at <paramref name="path"/> declares a type with
    /// a <c>[Generator]</c> attribute, reading only its metadata.
    /// </summary>
    private static bool ContainsGenerators(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
                return false;
            var reader = peReader.GetMetadataReader();
            foreach (var handle in reader.CustomAttributes)
            {
                var attribute = reader.GetCustomAttribute(handle);
                if (attribute.Parent.Kind != HandleKind.TypeDefinition ||
                    attribute.Constructor.Kind != HandleKind.MemberReference)
                {
                    continue;
                }
                var constructor = reader.GetMemberReference(
                    (MemberReferenceHandle)attribute.Constructor);
                if (constructor.Parent.Kind != HandleKind.TypeReference)
                    continue;
                var type = reader.GetTypeReference(
                    (TypeReferenceHandle)constructor.Parent);
                if (reader.StringComparer.Equals(type.Name, "GeneratorAttribute") &&
                    reader.StringComparer.Equals(
                        type.Namespace,
                        "Microsoft.CodeAnalysis"))
                {
                    return true;
                }
            }
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates the C# generators declared in <paramref name="assembly"/>.
    /// </summary>
    private static IEnumerable<ISourceGenerator> LoadGenerators(Assembly assembly)
    {
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types;
        }

        foreach (var type in types)
        {
            if (type is null || type.IsAbstract || !IsCSharpGenerator(type))
                continue;
            var instance = Activator.CreateInstance(type);
            if (instance is IIncrementalGenerator incremental)
                yield return incremental.AsSourceGenerator();
            else if (instance is ISourceGenerator generator)
                yield return generator;
        }
    }

    /// <summary>
    /// Returns true if <paramref name="type"/> has a <c>[Generator]</c> attribute
    /// that includes C#.
    /// </summary>
    private static bool IsCSharpGenerator(Type type)
    {
        IList<CustomAttributeData> attributes;
        try
        {
            attributes = type.GetCustomAttributesData();
        }
        catch (Exception e) when (e is FileNotFoundException or FileLoadException or
            TypeLoadException)
        {
            // Code fix providers next to generators carry attributes of assemblies
            // that only IDEs provide; they are not generators
            return false;
        }

        foreach (var attribute in attributes)
        {
            if (attribute.AttributeType.FullName != typeof(GeneratorAttribute).FullName)
                continue;

            // Without arguments, a generator targets C#
            var languages = attribute.ConstructorArguments
                .SelectMany(a =>
                    a.Value is IEnumerable<CustomAttributeTypedArgument> values
                        ? values.Select(v => v.Value)
                        : [a.Value])
                .OfType<string>()
                .ToList();
            return languages.Count == 0 || languages.Contains(LanguageNames.CSharp);
        }
        return false;
    }

    /// <summary>
    /// Creates the warning for a generator that could not be loaded or failed.
    /// </summary>
    private static BuildDiagnostic CreateFailure(string generator, string reason) =>
        BuildDiagnostic.Warning(
            BuildDiagnosticCodes.SourceGeneratorFailed,
            null,
            $"The source generator '{generator}' could not be run while finding " +
            $"kernel launches: {reason} Code it generates is not visible to ILGPU, " +
            $"which may cause errors if kernel launches depend on it.");

    #endregion
}
