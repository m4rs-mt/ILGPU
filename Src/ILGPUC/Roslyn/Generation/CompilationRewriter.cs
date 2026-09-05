// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
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
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    /// kernel descriptors. Generic kernels are expanded into one kernel per
    /// concrete specialization, and inline kernels capturing delegate
    /// parameters into one kernel per delegate target. No modifications are
    /// made to the compilation.
    /// </summary>
    public static AnalysisResult Analyze(CSharpCompilation compilation)
    {
        var analyzer = new LaunchSiteAnalyzer(compilation);
        var launchSites = analyzer.FindAllLaunchSites();

        var extractor = new KernelExtractor(compilation);
        var kernels = MakeInlineKernelNamesUnique(
            launchSites
                .Select(extractor.Extract)
                .OfType<KernelDescriptor>()
                .ToList());

        // Monomorphize generic kernels with open type parameters.
        // For each kernel whose method has ITypeParameterSymbol type args,
        // discover concrete specializations from callers and expand into
        // per-specialization descriptors.
        var expandedKernels = new List<KernelDescriptor>();
        var dispatchKernels = new List<KernelDescriptor>();
        var delegateDiscoverer = new DelegateTargetDiscoverer(compilation);

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
            else if (kernel is
                {
                    DelegateCaptures: { } captures,
                    LiftedKernel: not null,
                })
            {
                // Bind captured delegate parameters to the targets passed by
                // the callers of the launching method, one kernel for each
                // combination of targets
                var keys = captures
                    .Select(delegateDiscoverer.DiscoverTargets)
                    .ToArray();
                if (keys.All(k => k is not null))
                {
                    dispatchKernels.Add(kernel);
                    var combinations = GetCombinations(
                        [.. keys.Select(k => k!.Value)]);
                    for (int i = 0; i < combinations.Count; i++)
                    {
                        expandedKernels.Add(
                            CreateBoundDescriptor(kernel, combinations[i], i));
                    }
                }
                else
                {
                    expandedKernels.Add(kernel);
                }
            }
            else
            {
                expandedKernels.Add(kernel);
            }
        }

        var uniqueKernels = DeduplicateKernels(expandedKernels);

        // A local function launched from several sites is reported once
        return new AnalysisResult(
            kernels,
            uniqueKernels,
            dispatchKernels,
            analyzer.Diagnostics
                .Concat(extractor.Diagnostics)
                .Concat(delegateDiscoverer.Diagnostics)
                .Distinct()
                .ToList(),
            delegateDiscoverer.Registrations,
            delegateDiscoverer.Warnings.Distinct().ToList());
    }

    /// <summary>
    /// Returns all combinations that pick one key of each delegate capture.
    /// </summary>
    private static List<ImmutableArray<string>> GetCombinations(
        ImmutableArray<string>[] keys)
    {
        var combinations = new List<ImmutableArray<string>>
        {
            ImmutableArray<string>.Empty,
        };
        foreach (var captureKeys in keys)
        {
            combinations =
            [
                .. combinations.SelectMany(
                    combination => captureKeys.Select(key => combination.Add(key))),
            ];
        }
        return combinations;
    }

    /// <summary>
    /// Creates a <see cref="KernelDescriptor"/> that binds the delegate
    /// captures of an inline kernel to the targets registered under the given
    /// keys. Its lifted kernel takes the remaining captures and calls the
    /// lifted kernel of the original with the bound delegates:
    /// <code>
    /// static void InlineKernel_Host_L12_C31_D0(
    ///     global::ILGPU.Index1D index, ArrayView&lt;int&gt; view) =>
    ///     InlineKernel_Host_L12_C31(
    ///         index,
    ///         view,
    ///         KernelDelegates.Bind&lt;Func&lt;int, int&gt;&gt;(
    ///             "Program:Host.UnaryOp.op#0"));
    /// </code>
    /// </summary>
    private static KernelDescriptor CreateBoundDescriptor(
        KernelDescriptor original,
        ImmutableArray<string> keys,
        int index)
    {
        var captures = original.DelegateCaptures!.Value;
        var lifted = original.LiftedKernel!;
        var name = $"{original.KernelName}_D{index}";

        // The first parameter of the lifted kernel is the index
        var parameters = lifted.ParameterList.Parameters;
        var wrapperParameters = new List<ParameterSyntax> { parameters[0] };
        var arguments = new List<ArgumentSyntax>
        {
            SyntaxFactory.Argument(
                SyntaxFactory.IdentifierName(parameters[0].Identifier)),
        };
        var boundParameters = ImmutableArray.CreateBuilder<CapturedParameterInfo>();
        var captureIndices = Enumerable.Range(0, captures.Length)
            .ToDictionary(i => captures[i].ParameterIndex);
        for (int i = 0; i < original.Parameters.Length; i++)
        {
            if (!captureIndices.TryGetValue(i, out int captureIndex))
            {
                var parameter = parameters[i + 1];
                wrapperParameters.Add(parameter);
                arguments.Add(SyntaxFactory.Argument(
                    SyntaxFactory.IdentifierName(parameter.Identifier)));
                boundParameters.Add(original.Parameters[i]);
                continue;
            }

            // KernelDelegates.Bind<TDelegate>("key")
            var delegateType = captures[captureIndex].Parameter.Type
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var bind = SyntaxFactory.InvocationExpression(
                SyntaxFactory.ParseExpression(
                    $"global::ILGPU.Runtime.KernelDelegates.Bind<{delegateType}>"),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(keys[captureIndex]))))));
            arguments.Add(SyntaxFactory.Argument(bind));
        }

        var wrapper = SyntaxFactory.LocalFunctionStatement(
                lifted.ReturnType,
                SyntaxFactory.Identifier(name))
            .WithModifiers(lifted.Modifiers)
            .WithParameterList(SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(wrapperParameters)))
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName(lifted.Identifier),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(arguments)))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .NormalizeWhitespace();

        return original with
        {
            KernelName = name,
            Parameters = boundParameters.ToImmutable(),
            GenericOriginName = original.KernelName,
            LiftedKernel = wrapper,
            DelegateCaptures = null,
            DelegateKeys = keys,
        };
    }

    /// <summary>
    /// Appends a counter to inline kernels whose position-based names collide
    /// (e.g. lambdas at the same position of equally named files), so every
    /// inline lambda is compiled into its own kernel.
    /// </summary>
    private static List<KernelDescriptor> MakeInlineKernelNamesUnique(
        List<KernelDescriptor> kernels)
    {
        var names = new HashSet<string>(
            kernels.Where(k => k.KernelBody is null).Select(k => k.KernelName));
        for (int i = 0; i < kernels.Count; i++)
        {
            var kernel = kernels[i];
            if (kernel.KernelBody is null || names.Add(kernel.KernelName))
                continue;

            int counter = 2;
            while (!names.Add($"{kernel.KernelName}_{counter}"))
                ++counter;
            var name = $"{kernel.KernelName}_{counter}";
            kernels[i] = kernel with
            {
                KernelName = name,
                LiftedKernel = kernel.LiftedKernel?.WithIdentifier(
                    SyntaxFactory.Identifier(name)),
            };
        }
        return kernels;
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
    /// <param name="compilation">The compilation to rewrite.</param>
    /// <param name="analysis">The analysis results of the compilation.</param>
    /// <param name="liftInlineKernels">
    /// When <see langword="true"/>, the bodies of inline lambda kernels are
    /// lifted into static local functions next to their launch sites, so the
    /// emitted assembly contains a method for each of them that can be
    /// resolved and compiled. Compiled kernels do not need these methods, so
    /// the final rewrite leaves them out.
    /// </param>
    public RewriteResult Rewrite(
        CSharpCompilation compilation,
        AnalysisResult analysis,
        bool liftInlineKernels = false)
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
        var rewriter = new CallSiteRewriter(
            analysis,
            liftInlineKernels: liftInlineKernels);
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
            analysis,
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
/// <param name="Diagnostics">
/// Errors found at launch sites, such as inline kernels capturing values that
/// cannot be passed to a kernel. Kernels must not be compiled if any exist.
/// </param>
/// <param name="DelegateRegistrations">
/// The delegates passed to delegate parameters captured by inline kernels,
/// which are registered under the keys of their targets at their call sites.
/// </param>
/// <param name="Warnings">
/// Warnings found at launch sites, which do not prevent compiling kernels.
/// </param>
record AnalysisResult(
    List<KernelDescriptor> AllKernels,
    List<KernelDescriptor> UniqueKernels,
    List<KernelDescriptor> DispatchKernels,
    List<KernelDiagnostic> Diagnostics,
    Dictionary<ArgumentSyntax, DelegateRegistration> DelegateRegistrations,
    List<KernelDiagnostic> Warnings);

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
