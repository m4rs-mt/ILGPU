// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompilationRewriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Roslyn.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

namespace ILGPUC.Roslyn.Generation;

/// <summary>
/// Provides CompiledKernel C# source for a given kernel descriptor.
/// Implementations can generate stubs or return real ILGPUC-generated code.
/// </summary>
interface ICompiledKernelProvider
{
    /// <summary>
    /// Returns C# source for the CompiledKernel class for the given kernel.
    /// </summary>
    string GetCompiledKernelSource(KernelDescriptor kernel);
}

/// <summary>
/// Default provider that generates stub CompiledKernel classes.
/// Used when ILGPUC is not available.
/// </summary>
sealed class StubCompiledKernelProvider : ICompiledKernelProvider
{
    public string GetCompiledKernelSource(KernelDescriptor kernel) =>
        LauncherStubGenerator.GenerateStub(kernel);
}

/// <summary>
/// Provider that uses pre-generated C# source strings keyed by kernel name.
/// Used when ILGPUC has already compiled the kernels and returned C# classes.
/// Falls back to stubs for any kernel not in the map.
/// </summary>
/// <param name="sourceByKernelName">
/// Map from kernel name to the pre-compiled C# source for its
/// <c>*_CompiledKernel</c> class.
/// </param>
sealed class PrecompiledKernelProvider(
    Dictionary<string, string> sourceByKernelName) : ICompiledKernelProvider
{
    private readonly StubCompiledKernelProvider _fallback = new();

    public string GetCompiledKernelSource(KernelDescriptor kernel)
    {
        if (sourceByKernelName.TryGetValue(kernel.KernelName, out var source))
            return source;
        return _fallback.GetCompiledKernelSource(kernel);
    }
}

/// <summary>
/// Orchestrates the full pipeline: analyze -> extract -> generate/inject -> rewrite.
/// </summary>
sealed class CompilationRewriter(ICompiledKernelProvider? kernelProvider = null)
{
    private readonly ICompiledKernelProvider _kernelProvider =
        kernelProvider ?? new StubCompiledKernelProvider();

    /// <summary>
    /// Phase 1: Analyze the compilation to find launch sites and extract
    /// kernel descriptors. No modifications are made to the compilation.
    /// </summary>
    public static AnalysisResult Analyze(CSharpCompilation compilation)
    {
        var analyzer = new LaunchSiteAnalyzer(compilation);
        var launchSites = analyzer.FindAllLaunchSites();

        var extractor = new KernelExtractor(compilation);
        var kernels = launchSites.Select(extractor.Extract).ToList();

        // Monomorphize generic kernels with open type parameters.
        // For each kernel whose method has ITypeParameterSymbol type args,
        // discover concrete specializations from callers and expand into
        // per-specialization descriptors.
        var expandedKernels = new List<KernelDescriptor>();
        var dispatchKernels = new List<KernelDescriptor>();

        foreach (var kernel in kernels)
        {
            if (kernel.KernelMethod is { IsGenericMethod: true } gm
                && gm.TypeArguments.Any(t => t is ITypeParameterSymbol))
            {
                var specs = SpecializationDiscoverer.DiscoverSpecializations(
                    compilation, kernel);

                if (specs.Count > 0)
                {
                    dispatchKernels.Add(kernel);
                    foreach (var spec in specs)
                    {
                        var concrete = CreateConcreteDescriptor(kernel, spec);
                        expandedKernels.Add(concrete);
                    }
                }
                else
                {
                    // No concrete specializations found — keep as-is (stub)
                    expandedKernels.Add(kernel);
                }
            }
            else
            {
                expandedKernels.Add(kernel);
            }
        }

        var uniqueKernels = DeduplicateKernels(expandedKernels);

        return new AnalysisResult(kernels, uniqueKernels, dispatchKernels);
    }

    /// <summary>
    /// Creates a concrete <see cref="KernelDescriptor"/> by substituting
    /// type arguments from a discovered specialization into the original
    /// open-generic kernel.
    /// </summary>
    private static KernelDescriptor CreateConcreteDescriptor(
        KernelDescriptor original,
        KernelSpecialization spec)
    {
        var gm = original.KernelMethod!;

        // Construct the concrete method symbol by substituting type args
        var concreteMethod = gm.OriginalDefinition.Construct(
            [.. spec.TypeArguments]);

        // Rebuild parameters with concrete types from the constructed method.
        // The original parameters map to method params after skipping the
        // index parameter (param 0).
        var parameters = ImmutableArray.CreateBuilder<CapturedParameterInfo>(
            original.Parameters.Length);
        int startParam = original.Parameters.Length ==
            concreteMethod.Parameters.Length - 1 ? 1 : 0;

        for (int i = 0; i < original.Parameters.Length; i++)
        {
            var origParam = original.Parameters[i];
            var methodParamIdx = i + startParam;
            var concreteType = methodParamIdx < concreteMethod.Parameters.Length
                ? concreteMethod.Parameters[methodParamIdx].Type
                : origParam.Type;
            var kind = KernelExtractor.Classify(concreteType);
            parameters.Add(new CapturedParameterInfo(
                Name: origParam.Name,
                SourceExpression: origParam.SourceExpression,
                Type: concreteType,
                Kind: kind));
        }

        return new KernelDescriptor(
            KernelName: $"{original.KernelName}{spec.MangledSuffix}",
            KernelMethod: concreteMethod,
            KernelBody: original.KernelBody,
            Parameters: parameters.ToImmutable(),
            Variant: original.Variant,
            LaunchInfo: original.LaunchInfo,
            GenericOriginName: original.KernelName,
            ConcreteTypeArgs: spec.TypeArguments);
    }

    /// <summary>
    /// Phase 2: Given analysis results + CompiledKernel source (from ILGPUC
    /// or stubs), inject generated code and rewrite call sites.
    /// </summary>
    public RewriteResult Rewrite(
        CSharpCompilation compilation,
        AnalysisResult analysis)
    {
        var generatedSources = new List<string>();

        // Get CompiledKernel source for each unique kernel
        foreach (var kernel in analysis.UniqueKernels)
        {
            var source = _kernelProvider.GetCompiledKernelSource(kernel);
            generatedSources.Add(source);
        }

        // Generate dispatch stubs for monomorphized generic kernels
        foreach (var dispatch in analysis.DispatchKernels)
        {
            var concreteSpecs = analysis.UniqueKernels
                .Where(k => k.GenericOriginName == dispatch.KernelName)
                .ToList();
            generatedSources.Add(
                DispatchStubGenerator.GenerateDispatchStub(
                    dispatch, concreteSpecs));
        }

        // Inject generated sources into compilation
        var updatedCompilation = compilation;
        foreach (var src in generatedSources)
        {
            var text = SourceText.From(src, Encoding.UTF8);
            var tree = CSharpSyntaxTree.ParseText(text);
            updatedCompilation = updatedCompilation.AddSyntaxTrees(tree);
        }

        // Rewrite call sites in original trees
        var rewriter = new CallSiteRewriter(analysis.AllKernels);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var newRoot = rewriter.Visit(tree.GetRoot());
            if (newRoot != tree.GetRoot())
            {
                var newTree = tree.WithRootAndOptions(newRoot, tree.Options);
                updatedCompilation = updatedCompilation
                    .ReplaceSyntaxTree(tree, newTree);
            }
        }

        return new RewriteResult(
            updatedCompilation, analysis.AllKernels, generatedSources);
    }

    /// <summary>
    /// Convenience: runs both phases in sequence.
    /// </summary>
    public RewriteResult Rewrite(CSharpCompilation compilation)
    {
        var analysis = Analyze(compilation);
        return Rewrite(compilation, analysis);
    }

    /// <summary>
    /// Build-tool mode: analyze, rewrite, and write output files to disk.
    /// Does not modify the in-memory compilation — instead writes rewritten
    /// source files (with #line directives) and generated files to outputDir.
    /// </summary>
    /// <param name="compilation">The compilation to analyze and rewrite.</param>
    /// <param name="outputDir">Directory to write output files to.</param>
    /// <param name="projectDir">
    /// Project root for computing relative paths. If null, file names are used.
    /// </param>
    /// <returns>The number of rewritten files (0 if no launch sites found).</returns>
    public int RewriteToDisk(
        CSharpCompilation compilation,
        string outputDir,
        string? projectDir = null)
    {
        var analysis = Analyze(compilation);

        if (analysis.AllKernels.Count == 0)
        {
            // No launch sites — write empty manifest
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "manifest.txt"), "");
            return 0;
        }

        // Collect generated sources with filenames
        var generatedSources = new List<(string FileName, string Source)>();

        foreach (var kernel in analysis.UniqueKernels)
        {
            var source = _kernelProvider.GetCompiledKernelSource(kernel);
            generatedSources.Add(($"{kernel.KernelName}_CompiledKernel.cs", source));
        }

        // Generate dispatch stubs for monomorphized generic kernels
        foreach (var dispatch in analysis.DispatchKernels)
        {
            var concreteSpecs = analysis.UniqueKernels
                .Where(k => k.GenericOriginName == dispatch.KernelName)
                .ToList();
            generatedSources.Add((
                $"{dispatch.KernelName}_CompiledKernel.cs",
                DispatchStubGenerator.GenerateDispatchStub(
                    dispatch, concreteSpecs)));
        }

        // Rewrite call sites with #line directives enabled
        var rewriter = new CallSiteRewriter(
            analysis.AllKernels,
            emitLineDirectives: true);
        var rewrittenFiles = new Dictionary<string, SyntaxTree>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            var newRoot = rewriter.Visit(root);
            if (newRoot != root)
            {
                var newTree = tree.WithRootAndOptions(newRoot, tree.Options);

                // Add #line 1 "original_path" at the top so the whole file
                // maps back to the original source for debugging
                var originalPath = tree.FilePath;
                if (!string.IsNullOrEmpty(originalPath))
                {
                    newTree = CallSiteRewriter.AddTopOfFileLineDirective(
                        newTree, Path.GetFullPath(originalPath));
                }

                rewrittenFiles[originalPath ?? "unknown.cs"] = newTree;
            }
        }

        // Write everything to disk
        var writer = new ManifestWriter(outputDir);
        writer.WriteAll(rewrittenFiles, generatedSources, projectDir);

        return rewrittenFiles.Count;
    }

    /// <summary>
    /// Returns a deduplicated list of kernel descriptors. Two descriptors are
    /// considered the same if they refer to the same method (by Roslyn display
    /// string) or, for inline lambdas, by kernel name.
    /// </summary>
    private static List<KernelDescriptor> DeduplicateKernels(
        List<KernelDescriptor> kernels) =>
        kernels
            .DistinctBy(k => k.KernelMethod is { } m
                ? SymbolDisplay.ToDisplayString(m)
                : k.KernelName)
            .ToList();
}

/// <summary>
/// Holds the results of the analysis phase: all kernel descriptors found at
/// launch sites, and the deduplicated set for compilation.
/// </summary>
/// <param name="AllKernels">
/// All kernel descriptors, including duplicates (one per launch site).
/// </param>
/// <param name="UniqueKernels">
/// Deduplicated kernel descriptors — one per distinct method or inline body.
/// For monomorphized generic kernels, these are the concrete specializations.
/// </param>
/// <param name="DispatchKernels">
/// Original open-generic kernel descriptors that were expanded into concrete
/// specializations. Used to generate dispatch stubs and skip registration.
/// </param>
record AnalysisResult(
    List<KernelDescriptor> AllKernels,
    List<KernelDescriptor> UniqueKernels,
    List<KernelDescriptor> DispatchKernels);

/// <summary>
/// Holds the results of the rewrite phase.
/// </summary>
/// <param name="Compilation">
/// The updated compilation with injected CompiledKernel trees and rewritten call sites.
/// </param>
/// <param name="Kernels">All kernel descriptors from the analysis phase.</param>
/// <param name="GeneratedSources">
/// The raw C# source strings for all generated CompiledKernel classes.
/// </param>
record RewriteResult(
    CSharpCompilation Compilation,
    List<KernelDescriptor> Kernels,
    List<string> GeneratedSources);
