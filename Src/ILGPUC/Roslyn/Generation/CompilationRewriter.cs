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
    /// kernel descriptors. Kernels launched with open type arguments get one
    /// kernel per closed instantiation, and inline kernels capturing delegate
    /// parameters one kernel per delegate target. No modifications are made to
    /// the compilation.
    /// </summary>
    /// <param name="compilation">The compilation to analyze.</param>
    /// <param name="options">The analysis options, or the defaults.</param>
    public static AnalysisResult Analyze(
        CSharpCompilation compilation,
        AnalysisOptions? options = null)
    {
        options ??= AnalysisOptions.Default;
        var models = new SemanticModelCache(compilation);
        var analyzer = new LaunchSiteAnalyzer(compilation, models);
        var launchSites = analyzer.FindAllLaunchSites();

        // Source generators produce their code again when csc compiles the
        // rewritten project, so launch sites in generated code stay as they are
        var diagnostics = new List<BuildDiagnostic>();
        if (options.GeneratedTrees is { Count: > 0 } generatedTrees)
        {
            foreach (var site in launchSites.Where(
                s => generatedTrees.Contains(s.Invocation.SyntaxTree)))
            {
                diagnostics.Add(BuildDiagnostic.Warning(
                    BuildDiagnosticCodes.LaunchInGeneratedCode,
                    site.Invocation.GetLocation(),
                    "This kernel launch is in source-generated code, which ILGPU " +
                    "cannot rewrite, so no kernel is compiled for it and launching " +
                    "it throws a NotSupportedException. Launch the kernel from " +
                    "hand-written code."));
            }
            launchSites = [.. launchSites.Where(
                s => !generatedTrees.Contains(s.Invocation.SyntaxTree))];
        }

        var extractor = new KernelExtractor(compilation, models);
        var kernels = MakeInlineKernelNamesUnique(
            launchSites
                .Select(extractor.Extract)
                .OfType<KernelDescriptor>()
                .ToList());

        // Bind captured delegate parameters to the targets passed by the
        // callers of the launching method
        var delegateDiscoverer = new DelegateTargetDiscoverer(
            compilation,
            options.GeneratedTrees,
            options.IsLibrary);
        var delegateKeys = DiscoverDelegateTargets(kernels, delegateDiscoverer);

        // Launches that cannot be rewritten, inline kernels and local functions
        // that cannot be passed to a kernel, and delegates that cannot be
        // bound are errors; a local function launched from several sites is
        // reported once
        foreach (var diagnostic in analyzer.Diagnostics
            .Concat(extractor.Diagnostics)
            .Concat(delegateDiscoverer.Diagnostics)
            .Distinct())
        {
            diagnostics.Add(BuildDiagnostic.Error(
                diagnostic.Id,
                diagnostic.Location,
                diagnostic.Message));
        }

        if (diagnostics.Any(d => d.IsError))
            return new AnalysisResult(kernels, [], [], diagnostics);

        // Find the closed instantiations of kernels launched with open type
        // arguments, through any depth of generic wrappers.
        var openKernels = kernels.Where(IsOpen).ToList();
        var closure = GenericInstantiationClosure.Compute(
            compilation,
            openKernels,
            models,
            options.Limits ?? GenericInstantiationLimits.Default,
            options.WarnOnOpenPublicEntryPoints);
        diagnostics.AddRange(closure.Diagnostics);

        // Assign names: one per distinct closed kernel and one per dispatch stub
        var names = new KernelNameAllocator();
        var closedKeys = new List<(KernelDescriptor Kernel, string Key)>();
        foreach (var kernel in kernels.Where(k => !IsOpen(k)))
        {
            var key = GetClosedKey(kernel);
            closedKeys.Add((kernel, key));
            names.Request(key, kernel.KernelName, GetDisambiguator(kernel));
        }

        // Launch sites with open type arguments share one dispatch stub per
        // kernel definition and launch shape
        var stubGroups = openKernels
            .GroupBy(k => GetStubKey(k, GetSkippedParameters(k)))
            .ToList();
        foreach (var group in stubGroups)
        {
            var first = group.First();
            names.Request(
                group.Key,
                first.KernelMethod?.Name ?? first.KernelName,
                $"{first.Variant}{GetSkippedParameters(first)}");
        }
        names.Resolve();

        var allKernels = new List<KernelDescriptor>(kernels.Count);
        var uniqueKernels = new List<KernelDescriptor>();
        var uniqueNames = new HashSet<string>();
        var renamed = new Dictionary<KernelDescriptor, KernelDescriptor>();
        var delegateStubs = new List<DelegateDispatchStub>();
        foreach (var (kernel, key) in closedKeys)
        {
            var name = names[key];
            var named = kernel with
            {
                KernelName = name,
                LiftedKernel = kernel.LiftedKernel?.WithIdentifier(
                    SyntaxFactory.Identifier(name)),
            };
            renamed[kernel] = named;

            // An inline kernel capturing delegates is not compiled itself: a
            // dispatch stub launches the kernel bound to the passed targets,
            // one for each combination of targets
            if (delegateKeys.TryGetValue(kernel, out var captureKeys))
            {
                var combinations = GetCombinations(captureKeys);
                var bindings = new List<KernelDescriptor>(combinations.Count);
                for (int i = 0; i < combinations.Count; i++)
                {
                    var bindingName = names.Allocate(
                        $"{key}|D{i}",
                        $"{name}_D{i}");
                    bindings.Add(CreateBoundDescriptor(
                        named,
                        combinations[i],
                        bindingName));
                }
                delegateStubs.Add(new DelegateDispatchStub(named, [.. bindings]));
                uniqueKernels.AddRange(bindings);
                continue;
            }

            if (uniqueNames.Add(named.KernelName))
                uniqueKernels.Add(named);
        }

        var dispatchStubs = new List<DispatchStub>(stubGroups.Count);
        foreach (var group in stubGroups)
        {
            var sites = group.ToList();
            var stub = CreateDispatchStub(
                compilation,
                names,
                names[group.Key],
                group.Key,
                sites,
                closure.Instantiations);
            dispatchStubs.Add(stub);
            uniqueKernels.AddRange(stub.Specializations);
            for (int i = 0; i < sites.Count; ++i)
                renamed[sites[i]] = stub.LaunchSites[i];
        }

        foreach (var kernel in kernels)
            allKernels.Add(renamed[kernel]);

        // Generated launchers are top-level internal classes that name the types
        // of the kernel parameters, and dispatch stubs name the type arguments
        foreach (var kernel in allKernels)
        {
            ReportInaccessibleTypes(
                kernel,
                kernel,
                kernel.Parameters.Select(p => p.Type),
                diagnostics);
        }
        foreach (var stub in dispatchStubs)
        {
            foreach (var specialization in stub.Specializations)
            {
                ReportInaccessibleTypes(
                    specialization,
                    stub.LaunchSites[0],
                    specialization.ConcreteTypeArgs ?? [],
                    diagnostics);
            }
        }

        return new AnalysisResult(
            allKernels,
            uniqueKernels,
            dispatchStubs,
            diagnostics,
            closure.InstantiationCount)
        {
            DelegateDispatchStubs = delegateStubs,
            DelegateRegistrations = delegateDiscoverer.Registrations,
        };
    }

    /// <summary>
    /// Discovers the targets of the delegate parameters captured by inline
    /// kernels and returns the keys of the targets of each capture, by kernel.
    /// Kernels whose targets cannot be traced are left out, which the
    /// discoverer reports.
    /// </summary>
    private static Dictionary<KernelDescriptor, ImmutableArray<string>[]>
        DiscoverDelegateTargets(
            List<KernelDescriptor> kernels,
            DelegateTargetDiscoverer discoverer)
    {
        var result = new Dictionary<KernelDescriptor, ImmutableArray<string>[]>();
        foreach (var kernel in kernels)
        {
            if (kernel is not
                {
                    DelegateCaptures: { } captures,
                    LiftedKernel: not null,
                })
            {
                continue;
            }

            var keys = captures.Select(discoverer.DiscoverTargets).ToArray();
            if (keys.All(k => k is not null))
                result.Add(kernel, [.. keys.Select(k => k!.Value)]);
        }
        return result;
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
        string name)
    {
        var captures = original.DelegateCaptures!.Value;
        var lifted = original.LiftedKernel!;

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
    /// Reports the first of <paramref name="types"/> that is private or protected,
    /// or nested in such a type, as an error at the launch site.
    /// </summary>
    private static void ReportInaccessibleTypes(
        KernelDescriptor kernel,
        KernelDescriptor launchSite,
        IEnumerable<ITypeSymbol> types,
        List<BuildDiagnostic> diagnostics)
    {
        foreach (var type in types)
        {
            if (FindInaccessibleType(type) is not { } inaccessible)
                continue;
            var instantiation = kernel.ConcreteTypeArgs is { } arguments
                ? $" with type arguments <{string.Join(", ",
                    arguments.Select(a => a.ToDisplayString()))}>"
                : "";
            diagnostics.Add(BuildDiagnostic.Error(
                BuildDiagnosticCodes.InaccessibleKernelType,
                launchSite.LaunchInfo.Invocation.GetLocation(),
                $"The launch of kernel '{kernel.KernelMethod?.Name ?? "lambda"}'" +
                $"{instantiation} uses '{inaccessible.ToDisplayString()}', which is " +
                $"{SyntaxFacts.GetText(inaccessible.DeclaredAccessibility)}. The " +
                $"generated kernel launcher cannot access it; make it internal or " +
                $"public."));
            return;
        }
    }

    /// <summary>
    /// Returns a type in <paramref name="type"/> that code outside its containing
    /// types cannot access, or null.
    /// </summary>
    private static INamedTypeSymbol? FindInaccessibleType(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return FindInaccessibleType(array.ElementType);
            case IPointerTypeSymbol pointer:
                return FindInaccessibleType(pointer.PointedAtType);
            case INamedTypeSymbol named:
                for (var current = named; current is not null;
                     current = current.ContainingType)
                {
                    if (current.DeclaredAccessibility is
                        Accessibility.Private or
                        Accessibility.Protected or
                        Accessibility.ProtectedAndInternal)
                    {
                        return current;
                    }
                }
                foreach (var argument in named.TypeArguments)
                {
                    if (FindInaccessibleType(argument) is { } inaccessible)
                        return inaccessible;
                }
                return null;
            default:
                return null;
        }
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
    /// Returns <see langword="true"/> if the kernel of a launch site still has
    /// open type arguments, in its own type arguments or in those of its
    /// containing types.
    /// </summary>
    private static bool IsOpen(KernelDescriptor kernel) =>
        GenericContext.ContainsTypeParameter(kernel.GetTypeArguments());

    /// <summary>
    /// Returns the key that identifies the compiled kernel of a launch site with
    /// closed type arguments. Launch sites with equal keys share a compiled
    /// kernel; the key includes the launch variant and the argument types,
    /// because both shape the generated <c>Launch</c> method.
    /// </summary>
    private static string GetClosedKey(KernelDescriptor kernel)
    {
        if (kernel.KernelMethod is not { } method)
        {
            var location = kernel.LaunchInfo.Invocation.GetLocation().GetLineSpan();
            return $"inline|{location.Path}|{kernel.KernelBody?.SpanStart}";
        }

        var parameterTypes = string.Join(
            ", ",
            kernel.Parameters.Select(p => p.Type.ToDisplayString(
                GenericContext.KeyFormat)));
        return $"{method.ToDisplayString(GenericContext.KeyFormat)}|" +
            $"{kernel.Variant}|{parameterTypes}";
    }

    /// <summary>
    /// Returns a readable suffix that distinguishes a closed kernel from another
    /// kernel with the same name: its type arguments, or its launch variant.
    /// </summary>
    private static string GetDisambiguator(KernelDescriptor kernel)
    {
        if (kernel.KernelMethod is { } method)
        {
            var arguments = GenericContext.GetTypeArguments(method);
            if (arguments.Length > 0)
                return string.Join("_", arguments.Select(GenericContext.Mangle));
        }
        return kernel.Variant.ToString();
    }

    /// <summary>
    /// Returns the key of the dispatch stub of a launch site with open type
    /// arguments.
    /// </summary>
    private static string GetStubKey(KernelDescriptor kernel, int skippedParameters)
    {
        // Every inline lambda is a kernel definition of its own
        var definition = kernel.KernelMethod is { } method
            ? method.OriginalDefinition.ToDisplayString(GenericContext.KeyFormat)
            : GetClosedKey(kernel);
        return $"dispatch|{definition}|{kernel.Variant}|{skippedParameters}";
    }

    /// <summary>
    /// Returns how many leading kernel parameters the runtime supplies rather
    /// than the launch site: 1 if the launch site forwards its thread index, 0
    /// otherwise.
    /// </summary>
    private static int GetSkippedParameters(KernelDescriptor kernel)
    {
        if (kernel.KernelMethod is not { } method)
            return 0;
        int skipped = method.Parameters.Length - kernel.Parameters.Length;
        return Math.Clamp(skipped, 0, 1);
    }

    /// <summary>
    /// Creates the dispatch stub for a group of launch sites that launch the same
    /// kernel definition with open type arguments, together with one concrete
    /// kernel per closed instantiation that reaches any of them.
    /// </summary>
    private static DispatchStub CreateDispatchStub(
        CSharpCompilation compilation,
        KernelNameAllocator names,
        string stubName,
        string stubKey,
        List<KernelDescriptor> sites,
        IReadOnlyDictionary<KernelDescriptor, IReadOnlyList<ImmutableArray<ITypeSymbol>>>
            instantiations)
    {
        var first = sites[0];
        var definition = first.GenericDefinition!;
        int skipped = GetSkippedParameters(first);

        // Lifted inline kernels take the stub's name in the compilation that
        // resolves them, so that all specializations find the same method
        var lifted = first.LiftedKernel?.WithIdentifier(
            SyntaxFactory.Identifier(stubName));

        // The union of the instantiations of all launch sites, in a stable order
        var seen = new HashSet<string>();
        var arguments = new List<ImmutableArray<ITypeSymbol>>();
        foreach (var site in sites)
        {
            if (!instantiations.TryGetValue(site, out var list))
                continue;
            foreach (var args in list)
            {
                if (seen.Add(GenericContext.GetKey(args)))
                    arguments.Add(args);
            }
        }

        var specializations = ImmutableArray.CreateBuilder<KernelDescriptor>();
        foreach (var args in arguments)
        {
            var name = names.Allocate(
                $"{stubKey}|{GenericContext.GetKey(args)}",
                $"{stubName}_{string.Join("_", args.Select(GenericContext.Mangle))}");

            // Local functions and lifted inline lambdas cannot be constructed
            // with the type arguments of their enclosing code, so their
            // parameter types are substituted instead
            IMethodSymbol? method;
            IEnumerable<(string Name, ITypeSymbol Type)> launchParameters;
            if (first.IsLocalKernel)
            {
                var substitution = GenericContext.CreateSubstitution(definition, args);
                method = first.KernelMethod?.OriginalDefinition;
                launchParameters = (method is null
                        ? first.Parameters.Select(p => (p.Name, p.Type))
                        : method.Parameters.Skip(skipped).Select(p => (p.Name, p.Type)))
                    .Select(p => (
                        p.Name,
                        GenericContext.Substitute(compilation, p.Type, substitution)));
            }
            else
            {
                method = GenericContext.ConstructMethod(
                    compilation,
                    (IMethodSymbol)definition,
                    args);
                launchParameters = method.Parameters
                    .Skip(skipped)
                    .Select(p => (p.Name, p.Type));
            }

            var parameters = ImmutableArray.CreateBuilder<CapturedParameterInfo>();
            foreach (var (parameterName, type) in launchParameters)
            {
                parameters.Add(new CapturedParameterInfo(
                    Name: parameterName,
                    SourceExpression: SyntaxFactory.IdentifierName(parameterName),
                    Type: type,
                    Kind: KernelExtractor.Classify(type)));
            }

            specializations.Add(new KernelDescriptor(
                KernelName: name,
                KernelMethod: method,
                KernelBody: first.KernelBody,
                Parameters: parameters.ToImmutable(),
                Variant: first.Variant,
                LaunchInfo: first.LaunchInfo,
                GenericOriginName: stubName,
                ConcreteTypeArgs: args,
                LiftedKernel: lifted,
                InlineKernelSymbol: first.InlineKernelSymbol));
        }

        var launchSites = sites
            .Select(site => site with
            {
                KernelName = stubName,
                DispatchTypeArguments = site.GetTypeArguments(),
                LiftedKernel = lifted,
            })
            .ToImmutableArray();

        return new DispatchStub(
            stubName,
            definition,
            first.Variant,
            skipped,
            specializations.ToImmutable(),
            launchSites);
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

        // Generate dispatch stubs for kernels launched with open type arguments
        // and for inline kernels capturing delegates
        foreach (var stub in analysis.DispatchStubs)
            generatedSources.Add(DispatchStubGenerator.GenerateDispatchStub(stub));
        foreach (var stub in analysis.DelegateDispatchStubs)
        {
            generatedSources.Add(
                DispatchStubGenerator.GenerateDelegateDispatchStub(stub));
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
    /// <param name="compilation">The compilation to rewrite.</param>
    /// <param name="analysis">The analysis of the compilation.</param>
    /// <param name="outputDir">Directory to write output files to.</param>
    /// <param name="projectDir">
    /// Project root for computing relative paths. If null, file names are used.
    /// </param>
    /// <returns>The number of rewritten files (0 if no launch sites found).</returns>
    public int RewriteToDisk(
        CSharpCompilation compilation,
        AnalysisResult analysis,
        string outputDir,
        string? projectDir = null)
    {

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

        // Generate dispatch stubs for kernels launched with open type arguments
        // and for inline kernels capturing delegates
        foreach (var stub in analysis.DispatchStubs)
        {
            generatedSources.Add((
                $"{stub.Name}_CompiledKernel.cs",
                DispatchStubGenerator.GenerateDispatchStub(stub)));
        }
        foreach (var stub in analysis.DelegateDispatchStubs)
        {
            generatedSources.Add((
                $"{stub.Name}_CompiledKernel.cs",
                DispatchStubGenerator.GenerateDelegateDispatchStub(stub)));
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

}

/// <summary>
/// Holds the results of the analysis phase: all kernel descriptors found at
/// launch sites, and the deduplicated set for compilation.
/// </summary>
/// <param name="AllKernels">
/// All kernel descriptors, including duplicates (one per launch site). Launch
/// sites whose kernel has open type arguments are named after their dispatch stub.
/// </param>
/// <param name="UniqueKernels">
/// The kernels to compile: one per distinct closed kernel, plus one concrete
/// specialization per closed instantiation of each kernel launched with open type
/// arguments. Every name is unique.
/// </param>
/// <param name="DispatchStubs">
/// The dispatch stubs for kernels launched with open type arguments. They are
/// plain static classes, not compiled kernels, so they are not registered.
/// </param>
/// <param name="Diagnostics">
/// Warnings and errors found by the analysis, such as inline kernels capturing
/// values that cannot be passed to a kernel. Kernels must not be compiled if
/// any error exists.
/// </param>
/// <param name="InstantiationCount">
/// The number of generic instantiations visited to find the type arguments of
/// kernels launched with open type arguments.
/// </param>
record AnalysisResult(
    List<KernelDescriptor> AllKernels,
    List<KernelDescriptor> UniqueKernels,
    List<DispatchStub> DispatchStubs,
    List<BuildDiagnostic> Diagnostics,
    int InstantiationCount = 0)
{
    /// <summary>
    /// Returns the dispatch stubs for inline kernels capturing delegate
    /// parameters, whose bound kernels are part of
    /// <see cref="UniqueKernels"/>. Like the dispatch stubs for open type
    /// arguments, they are plain static classes, not compiled kernels.
    /// </summary>
    public List<DelegateDispatchStub> DelegateDispatchStubs { get; init; } = [];

    /// <summary>
    /// Returns the delegates passed to delegate parameters captured by inline
    /// kernels, which are registered under the keys of their targets at their
    /// call sites.
    /// </summary>
    public Dictionary<ArgumentSyntax, DelegateRegistration> DelegateRegistrations
    {
        get;
        init;
    } = [];

    /// <summary>
    /// Returns <see langword="true"/> if the analysis found an error.
    /// </summary>
    public bool HasErrors => Diagnostics.Any(d => d.IsError);

    /// <summary>
    /// Returns a copy of this analysis without the given concrete kernels, which
    /// are also removed from the dispatch stubs, so launches with their type
    /// arguments throw at run time.
    /// </summary>
    /// <param name="kernelNames">The names of the kernels to remove.</param>
    public AnalysisResult WithoutKernels(IReadOnlyCollection<string> kernelNames)
    {
        if (kernelNames.Count == 0)
            return this;
        var names = new HashSet<string>(kernelNames);
        return this with
        {
            UniqueKernels = [.. UniqueKernels.Where(k => !names.Contains(k.KernelName))],
            DispatchStubs =
            [
                .. DispatchStubs.Select(s => s with
                {
                    Specializations =
                    [
                        .. s.Specializations.Where(k => !names.Contains(k.KernelName)),
                    ],
                }),
            ],
        };
    }
}

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
