// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericInstantiationClosure.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Limits that stop the instantiation closure from growing without bound, for
/// example through polymorphic recursion such as <c>Run&lt;T&gt;</c> calling
/// <c>Run&lt;Box&lt;T&gt;&gt;</c>. They bound the size of type arguments and the
/// number of instantiations, not the depth of call chains.
/// </summary>
/// <param name="MaxTypeDepth">
/// The maximum nesting depth of a type argument; <c>Box&lt;Box&lt;int&gt;&gt;</c>
/// has depth 3.
/// </param>
/// <param name="MaxInstantiationsPerDefinition">
/// The maximum number of distinct instantiations of a single generic definition.
/// </param>
/// <param name="MaxInstantiations">
/// The maximum number of instantiations across the whole compilation.
/// </param>
sealed record GenericInstantiationLimits(
    int MaxTypeDepth = GenericInstantiationLimits.DefaultMaxTypeDepth,
    int MaxInstantiationsPerDefinition =
        GenericInstantiationLimits.DefaultMaxInstantiationsPerDefinition,
    int MaxInstantiations = GenericInstantiationLimits.DefaultMaxInstantiations)
{
    /// <summary>The default maximum type nesting depth.</summary>
    public const int DefaultMaxTypeDepth = 8;

    /// <summary>The default maximum number of instantiations per definition.</summary>
    public const int DefaultMaxInstantiationsPerDefinition = 256;

    /// <summary>The default maximum number of instantiations in total.</summary>
    public const int DefaultMaxInstantiations = 16384;

    /// <summary>The default limits.</summary>
    public static GenericInstantiationLimits Default { get; } = new();
}

/// <summary>
/// The result of <see cref="GenericInstantiationClosure.Compute"/>.
/// </summary>
/// <param name="Instantiations">
/// For each launch site with open type arguments, the closed type arguments of
/// the kernel that reach it, ordered as by
/// <see cref="GenericContext.GetTypeParameters"/> on the kernel definition.
/// </param>
/// <param name="Diagnostics">Warnings and errors found by the closure.</param>
/// <param name="InstantiationCount">
/// The number of generic instantiations the closure visited.
/// </param>
sealed record GenericInstantiationResult(
    IReadOnlyDictionary<KernelDescriptor, IReadOnlyList<ImmutableArray<ITypeSymbol>>>
        Instantiations,
    IReadOnlyList<BuildDiagnostic> Diagnostics,
    int InstantiationCount);

/// <summary>
/// Finds the closed type arguments that reach kernel launch sites whose kernel
/// still has open type arguments, such as <c>Launch(Kernel&lt;T&gt;)</c> inside a
/// generic wrapper <c>Run&lt;T&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// The closure is computed once per compilation. Every member body is walked with
/// <see cref="IOperation"/>s, and each reference to a generic definition becomes an
/// edge labelled with the type arguments it supplies, expressed in the caller's
/// type parameters. Permutations (<c>Run&lt;U, T&gt;</c>), partial application
/// (<c>Run&lt;int, T&gt;</c>) and constructed arguments (<c>Run&lt;Pair&lt;T&gt;&gt;</c>)
/// are plain substitutions. There are three kinds of edges:
/// </para>
/// <list type="bullet">
/// <item>direct calls, method groups, property accessors and operators;</item>
/// <item>dispatched calls: virtual and interface calls, and constrained calls on a
/// type parameter (static abstract members, struct instance members);</item>
/// <item>instantiations: object creation, <c>new T()</c> and boxing.</item>
/// </list>
/// <para>
/// Dispatched calls are resolved with rapid type analysis. Every virtual or
/// interface slot that is called and every type that is instantiated is recorded
/// with closed type arguments; each pair whose type implements or overrides the
/// slot instantiates the implementing method. Constrained calls resolve directly
/// on the closed type argument.
/// </para>
/// <para>
/// Only type parameters that can flow into the type arguments of an open launch
/// site are tracked; the others are kept open, so a type parameter that grows
/// without bound but never reaches a kernel does not multiply instantiations.
/// A visited set terminates ordinary recursion; <see cref="GenericInstantiationLimits"/>
/// stops polymorphic recursion with a warning, and instantiations beyond a limit
/// are not compiled.
/// </para>
/// </remarks>
sealed class GenericInstantiationClosure
{
    #region Nested Types

    /// <summary>
    /// The kinds of edges.
    /// </summary>
    private enum EdgeKind
    {
        /// <summary>A call of a known method.</summary>
        Direct,

        /// <summary>A virtual, interface or constrained call of a slot.</summary>
        Dispatch,

        /// <summary>The creation of an object or a box of a type.</summary>
        Instantiate,
    }

    /// <summary>
    /// An edge from a definition to code it may run.
    /// </summary>
    /// <param name="Kind">The kind of edge.</param>
    /// <param name="Callee">The called definition, for direct edges.</param>
    /// <param name="Arguments">
    /// The type arguments of the callee, in the caller's context, for direct edges.
    /// </param>
    /// <param name="Slot">The called slot, for dispatch edges.</param>
    /// <param name="Type">
    /// The constrained type of a dispatch edge (null for a virtual call), or the
    /// instantiated type.
    /// </param>
    /// <param name="IsDefaultConstruction">
    /// True for <c>new T()</c>, which also runs the parameterless constructor.
    /// </param>
    /// <param name="Location">The location of the reference.</param>
    private sealed record Edge(
        EdgeKind Kind,
        int Callee,
        ImmutableArray<ITypeSymbol> Arguments,
        IMethodSymbol? Slot,
        ITypeSymbol? Type,
        bool IsDefaultConstruction,
        Location Location);

    /// <summary>
    /// A launch site whose kernel has open type arguments.
    /// </summary>
    /// <param name="Kernel">The kernel descriptor of the launch site.</param>
    /// <param name="Arguments">
    /// The kernel's type arguments, in the context of the enclosing definition.
    /// </param>
    private sealed record OpenSite(
        KernelDescriptor Kernel,
        ImmutableArray<ITypeSymbol> Arguments);

    /// <summary>
    /// One instantiation of a definition.
    /// </summary>
    /// <param name="Definition">The index of the definition.</param>
    /// <param name="Arguments">
    /// The type arguments: closed at tracked positions, the definition's own type
    /// parameters elsewhere.
    /// </param>
    /// <param name="Parent">The instantiation that led here, if any.</param>
    private sealed record Item(
        int Definition,
        ImmutableArray<ITypeSymbol> Arguments,
        Item? Parent);

    #endregion

    #region Instance

    private readonly CSharpCompilation _compilation;
    private readonly SemanticModelCache _models;
    private readonly GenericInstantiationLimits _limits;
    private readonly bool _warnOnOpenPublicEntryPoints;
    private readonly Dictionary<ISymbol, int> _definitionIndices =
        new(GenericContext.DefinitionComparer);
    private readonly List<ISymbol> _definitions = [];
    private readonly List<List<Edge>> _edges = [];
    private readonly List<HashSet<string>> _edgeKeys = [];
    private readonly List<HashSet<int>> _callers = [];
    private readonly List<List<OpenSite>> _openSites = [];
    private readonly List<HashSet<int>> _trackedPositions = [];
    private bool[] _relevant = [];
    private readonly Dictionary<string, List<int>> _potentialImplementations = [];
    private readonly Dictionary<ISymbol, bool> _staticInitialization =
        new(SymbolEqualityComparer.Default);
    private readonly List<BuildDiagnostic> _diagnostics = [];

    // Worklist state
    private readonly Queue<Item> _worklist = new();
    private readonly HashSet<string> _visited = [];
    private readonly Dictionary<int, int> _perDefinition = [];
    private readonly HashSet<int> _reportedDepth = [];
    private readonly HashSet<int> _reportedCount = [];
    private bool _reportedTotal;
    private readonly Dictionary<string, IMethodSymbol> _calledSlots = [];
    private readonly Dictionary<string, INamedTypeSymbol> _instantiatedTypes = [];
    private readonly HashSet<string> _defaultConstructedTypes = [];

    /// <summary>
    /// Creates a closure over <paramref name="compilation"/>.
    /// </summary>
    private GenericInstantiationClosure(
        CSharpCompilation compilation,
        SemanticModelCache models,
        GenericInstantiationLimits limits,
        bool warnOnOpenPublicEntryPoints)
    {
        _compilation = compilation;
        _models = models;
        _limits = limits;
        _warnOnOpenPublicEntryPoints = warnOnOpenPublicEntryPoints;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Computes the closed instantiations that reach the given launch sites.
    /// </summary>
    /// <param name="compilation">The compilation.</param>
    /// <param name="openSites">
    /// Launch sites whose kernel method still has open type arguments.
    /// </param>
    /// <param name="models">The semantic models of the compilation.</param>
    /// <param name="limits">The limits against unbounded growth.</param>
    /// <param name="warnOnOpenPublicEntryPoints">
    /// True to warn for externally visible methods whose type arguments reach a
    /// launch site, even if the compilation also has closed callers.
    /// </param>
    public static GenericInstantiationResult Compute(
        CSharpCompilation compilation,
        IReadOnlyList<KernelDescriptor> openSites,
        SemanticModelCache models,
        GenericInstantiationLimits limits,
        bool warnOnOpenPublicEntryPoints = false)
    {
        var closure = new GenericInstantiationClosure(
            compilation,
            models,
            limits,
            warnOnOpenPublicEntryPoints);
        return closure.Run(openSites);
    }

    /// <summary>
    /// Runs the closure.
    /// </summary>
    private GenericInstantiationResult Run(IReadOnlyList<KernelDescriptor> kernels)
    {
        var instantiations =
            new Dictionary<
                KernelDescriptor,
                IReadOnlyList<ImmutableArray<ITypeSymbol>>>();
        if (kernels.Count == 0)
            return new(instantiations, _diagnostics, 0);

        // Register the open launch sites with their enclosing definitions
        var results =
            new Dictionary<KernelDescriptor, List<ImmutableArray<ITypeSymbol>>>();
        var resultKeys = new Dictionary<KernelDescriptor, HashSet<string>>();
        foreach (var kernel in kernels)
        {
            results[kernel] = [];
            resultKeys[kernel] = [];

            var invocation = kernel.LaunchInfo.Invocation;
            var model = _models[invocation.SyntaxTree];
            var enclosing = Normalize(
                model.GetEnclosingSymbol(invocation.SpanStart));
            if (enclosing is null)
                continue;

            int index = GetDefinitionIndex(enclosing);
            _openSites[index].Add(new OpenSite(kernel, kernel.GetTypeArguments()));
        }

        CollectEdges();
        ComputeTrackedPositions();
        ComputeRelevantDefinitions();

        // Seed with every definition that has no type parameters in scope, and
        // with every edge that is already closed, from any caller
        for (int definition = 0; definition < _definitions.Count; ++definition)
        {
            if (GenericContext.GetTypeParameters(_definitions[definition]).Length == 0)
            {
                TryEnqueue(new Item(definition, [], null), null);
                continue;
            }
            var identity = Canonicalize(definition, []);
            ProcessEdges(
                new Item(definition, identity, null),
                GenericContext.CreateSubstitution(_definitions[definition], identity),
                closedOnly: true);
        }

        // Propagate closed type arguments
        while (_worklist.Count > 0)
        {
            var item = _worklist.Dequeue();
            var substitution = GenericContext.CreateSubstitution(
                _definitions[item.Definition],
                item.Arguments);

            foreach (var site in _openSites[item.Definition])
            {
                var arguments = Substitute(site.Arguments, substitution);
                if (GenericContext.ContainsTypeParameter(arguments))
                    continue;
                if (resultKeys[site.Kernel].Add(GenericContext.GetKey(arguments)))
                    results[site.Kernel].Add(arguments);
            }

            ProcessEdges(item, substitution, closedOnly: false);
        }

        var reportedEntryPoints = new HashSet<int>();
        foreach (var (kernel, list) in results)
        {
            instantiations[kernel] = list;
            if (list.Count == 0)
                ReportOpenLaunchSite(kernel);
            else if (_warnOnOpenPublicEntryPoints)
                ReportOpenPublicEntryPoints(kernel, reportedEntryPoints);
        }

        return new(instantiations, _diagnostics, _visited.Count);
    }

    /// <summary>
    /// Follows the edges of an instantiation.
    /// </summary>
    /// <param name="item">The instantiation.</param>
    /// <param name="substitution">Its type arguments as a substitution.</param>
    /// <param name="closedOnly">
    /// True while seeding: only follow edges that do not depend on the
    /// definition's own (still open) type parameters.
    /// </param>
    private void ProcessEdges(
        Item item,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> substitution,
        bool closedOnly)
    {
        foreach (var edge in _edges[item.Definition])
        {
            switch (edge.Kind)
            {
                case EdgeKind.Direct:
                    {
                        var arguments = Canonicalize(
                            edge.Callee,
                            Substitute(edge.Arguments, substitution));
                        if (IsClosed(edge.Callee, arguments))
                        {
                            TryEnqueue(
                                new Item(edge.Callee, arguments, item),
                                edge.Location);
                        }
                    }
                    break;
                case EdgeKind.Dispatch:
                    {
                        var slot = SubstituteMethod(edge.Slot!, substitution);
                        if (slot is null)
                            break;
                        if (edge.Type is { } constrainedType)
                        {
                            // Resolve on the closed type argument
                            if (GenericContext.Substitute(
                                    _compilation,
                                    constrainedType,
                                    substitution) is INamedTypeSymbol receiver &&
                                !GenericContext.ContainsTypeParameter(receiver))
                            {
                                EnqueueImplementation(
                                    receiver,
                                    slot,
                                    item,
                                    edge.Location);
                            }
                        }
                        else if (!closedOnly || !GenericContext.IsOpen(slot))
                        {
                            AddCalledSlot(slot, item, edge.Location);
                        }
                    }
                    break;
                case EdgeKind.Instantiate:
                    {
                        if (GenericContext.Substitute(
                                _compilation,
                                edge.Type!,
                                substitution) is INamedTypeSymbol type &&
                            (!closedOnly || !GenericContext.ContainsTypeParameter(type)))
                        {
                            AddInstantiatedType(
                                type,
                                edge.IsDefaultConstruction,
                                item,
                                edge.Location);
                        }
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Records a called virtual or interface slot and resolves it on every
    /// instantiated type.
    /// </summary>
    private void AddCalledSlot(IMethodSymbol slot, Item item, Location location)
    {
        var key = slot.ToDisplayString(GenericContext.KeyFormat);
        if (!_calledSlots.TryAdd(key, slot))
            return;
        foreach (var type in _instantiatedTypes.Values.ToArray())
            EnqueueImplementation(type, slot, item, location);
    }

    /// <summary>
    /// Records an instantiated type and resolves every called slot on it.
    /// </summary>
    private void AddInstantiatedType(
        INamedTypeSymbol type,
        bool isDefaultConstruction,
        Item item,
        Location location)
    {
        var key = type.ToDisplayString(GenericContext.KeyFormat);
        if (isDefaultConstruction && _defaultConstructedTypes.Add(key))
        {
            // new T() runs the parameterless constructor of the type argument
            var constructor = type.InstanceConstructors.FirstOrDefault(
                c => c.Parameters.Length == 0);
            if (constructor is not null)
                EnqueueMethod(constructor, item, location);
        }
        if (!_instantiatedTypes.TryAdd(key, type))
            return;
        foreach (var slot in _calledSlots.Values.ToArray())
            EnqueueImplementation(type, slot, item, location);
    }

    /// <summary>
    /// Instantiates the method that implements or overrides <paramref name="slot"/>
    /// in <paramref name="type"/>, if any.
    /// </summary>
    private void EnqueueImplementation(
        INamedTypeSymbol type,
        IMethodSymbol slot,
        Item item,
        Location location)
    {
        if (ResolveImplementation(type, slot) is { } implementation)
            EnqueueMethod(implementation, item, location);
    }

    /// <summary>
    /// Instantiates a (constructed) method if its definition is tracked.
    /// </summary>
    private void EnqueueMethod(IMethodSymbol method, Item item, Location location)
    {
        var definition = Normalize(method);
        if (definition is null ||
            !_definitionIndices.TryGetValue(definition, out int index))
        {
            return;
        }
        var arguments = Canonicalize(index, GenericContext.GetTypeArguments(method));
        if (IsClosed(index, arguments))
            TryEnqueue(new Item(index, arguments, item), location);
    }

    /// <summary>
    /// Returns the method that runs for a call of <paramref name="slot"/> on an
    /// object of type <paramref name="type"/>: its interface implementation
    /// (possibly a default interface method), or its most derived override.
    /// </summary>
    private static IMethodSymbol? ResolveImplementation(
        INamedTypeSymbol type,
        IMethodSymbol slot)
    {
        var implementation = slot.ContainingType.TypeKind == TypeKind.Interface
            ? type.FindImplementationForInterfaceMember(slot.ConstructedFrom)
                as IMethodSymbol
            : FindOverride(type, slot);

        if (implementation is { IsGenericMethod: true } &&
            implementation.Arity == slot.Arity &&
            SymbolEqualityComparer.Default.Equals(
                implementation,
                implementation.ConstructedFrom))
        {
            implementation = implementation.Construct([.. slot.TypeArguments]);
        }
        return implementation;
    }

    /// <summary>
    /// Returns the most derived method of <paramref name="type"/> or its base
    /// types that overrides <paramref name="slot"/> (or is the slot itself).
    /// </summary>
    private static IMethodSymbol? FindOverride(INamedTypeSymbol type, IMethodSymbol slot)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(slot.Name))
            {
                for (var overridden = member as IMethodSymbol;
                     overridden is not null;
                     overridden = overridden.OverriddenMethod)
                {
                    if (SymbolEqualityComparer.Default.Equals(
                        overridden.OriginalDefinition,
                        slot.OriginalDefinition))
                    {
                        return (IMethodSymbol)member;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Enqueues an instantiation unless it was visited before or exceeds a limit.
    /// Limits are warnings: the instantiation is not compiled, and launching a
    /// kernel with its type arguments throws at run time.
    /// </summary>
    private bool TryEnqueue(Item item, Location? location)
    {
        if (!_relevant[item.Definition])
            return false;

        var key = $"{item.Definition}|{GenericContext.GetKey(item.Arguments)}";
        if (_visited.Contains(key))
            return false;

        var tracked = _trackedPositions[item.Definition];
        int depth = item.Arguments
            .Where((_, i) => tracked.Contains(i))
            .Select(GenericContext.GetNestingDepth)
            .DefaultIfEmpty(0)
            .Max();
        if (depth > _limits.MaxTypeDepth)
        {
            if (_reportedDepth.Add(item.Definition))
            {
                _diagnostics.Add(BuildDiagnostic.Warning(
                    BuildDiagnosticCodes.TypeDepthExceeded,
                    location,
                    $"The generic instantiation " +
                    $"'{Describe(item.Definition, item.Arguments)}' nests type " +
                    $"arguments {depth} levels deep, more than the limit of " +
                    $"{_limits.MaxTypeDepth} (ILGPUMaxGenericTypeDepth), so deeper " +
                    $"instantiations are not compiled and launching a kernel with " +
                    $"them throws a NotSupportedException. The instantiation chain " +
                    $"grows without bound: {DescribeChain(item)}."));
            }
            return false;
        }

        int count = _perDefinition.GetValueOrDefault(item.Definition);
        if (count >= _limits.MaxInstantiationsPerDefinition)
        {
            if (_reportedCount.Add(item.Definition))
            {
                _diagnostics.Add(BuildDiagnostic.Warning(
                    BuildDiagnosticCodes.DefinitionInstantiationsExceeded,
                    location,
                    $"'{Describe(item.Definition)}' is instantiated with more " +
                    $"than {_limits.MaxInstantiationsPerDefinition} distinct type " +
                    $"arguments (ILGPUMaxGenericInstantiationsPerDefinition), so " +
                    $"further instantiations are not compiled and launching a " +
                    $"kernel with them throws a NotSupportedException. Last " +
                    $"instantiation chain: {DescribeChain(item)}."));
            }
            return false;
        }

        if (_visited.Count >= _limits.MaxInstantiations)
        {
            if (!_reportedTotal)
            {
                _reportedTotal = true;
                _diagnostics.Add(BuildDiagnostic.Warning(
                    BuildDiagnosticCodes.TotalInstantiationsExceeded,
                    location,
                    $"The compilation needs more than " +
                    $"{_limits.MaxInstantiations} generic instantiations to find " +
                    $"the type arguments of its kernels " +
                    $"(ILGPUMaxGenericInstantiations), so further instantiations " +
                    $"are not compiled and launching a kernel with them throws a " +
                    $"NotSupportedException. Last instantiation chain: " +
                    $"{DescribeChain(item)}."));
            }
            return false;
        }

        _visited.Add(key);
        _perDefinition[item.Definition] = count + 1;
        _worklist.Enqueue(item);
        return true;
    }

    /// <summary>
    /// Replaces the type arguments at positions that are not tracked by the
    /// definition's own type parameters, so that instantiations differing only in
    /// untracked type arguments are one instantiation.
    /// </summary>
    private ImmutableArray<ITypeSymbol> Canonicalize(
        int definition,
        ImmutableArray<ITypeSymbol> arguments)
    {
        var parameters = GenericContext.GetTypeParameters(_definitions[definition]);
        var tracked = _trackedPositions[definition];
        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>(parameters.Length);
        for (int i = 0; i < parameters.Length; ++i)
        {
            builder.Add(tracked.Contains(i) && i < arguments.Length
                ? arguments[i]
                : parameters[i]);
        }
        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Returns true if all tracked type arguments are closed.
    /// </summary>
    private bool IsClosed(int definition, ImmutableArray<ITypeSymbol> arguments)
    {
        var tracked = _trackedPositions[definition];
        for (int i = 0; i < arguments.Length; ++i)
        {
            if (tracked.Contains(i) &&
                GenericContext.ContainsTypeParameter(arguments[i]))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Substitutes closed type arguments into a list of types.
    /// </summary>
    private ImmutableArray<ITypeSymbol> Substitute(
        ImmutableArray<ITypeSymbol> types,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> substitution)
    {
        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>(types.Length);
        foreach (var type in types)
            builder.Add(GenericContext.Substitute(_compilation, type, substitution));
        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Substitutes closed type arguments into a (constructed) method.
    /// </summary>
    private IMethodSymbol? SubstituteMethod(
        IMethodSymbol method,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> substitution)
    {
        var arguments = Substitute(GenericContext.GetTypeArguments(method), substitution);
        try
        {
            return GenericContext.ConstructMethod(
                _compilation,
                method.OriginalDefinition,
                arguments);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the index of <paramref name="definition"/>, registering it first if
    /// necessary.
    /// </summary>
    private int GetDefinitionIndex(ISymbol definition)
    {
        if (_definitionIndices.TryGetValue(definition, out int index))
            return index;

        index = _definitions.Count;
        _definitionIndices.Add(definition, index);
        _definitions.Add(definition);
        _edges.Add([]);
        _edgeKeys.Add([]);
        _callers.Add([]);
        _openSites.Add([]);
        _trackedPositions.Add([]);
        return index;
    }

    /// <summary>
    /// Maps a symbol enclosing some code, or a called method, to its definition:
    /// lambdas belong to the method that contains them, and both parts of a
    /// partial method map to the defining part.
    /// </summary>
    private static ISymbol? Normalize(ISymbol? symbol)
    {
        while (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction })
            symbol = symbol.ContainingSymbol;
        symbol = symbol?.OriginalDefinition;
        if (symbol is IMethodSymbol { PartialDefinitionPart: { } definitionPart })
            symbol = definitionPart;
        return symbol;
    }

    #endregion

    #region Edges

    /// <summary>
    /// Walks every member body in the compilation and records its edges.
    /// </summary>
    private void CollectEdges()
    {
        foreach (var tree in _compilation.SyntaxTrees)
        {
            var model = _models[tree];
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var operation = node switch
                {
                    BaseMethodDeclarationSyntax or AccessorDeclarationSyntax =>
                        model.GetOperation(node),
                    ArrowExpressionClauseSyntax
                    {
                        Parent: PropertyDeclarationSyntax or IndexerDeclarationSyntax
                    } => model.GetOperation(node),
                    EqualsValueClauseSyntax
                    {
                        Parent: VariableDeclaratorSyntax
                        {
                            Parent.Parent: FieldDeclarationSyntax
                        } or PropertyDeclarationSyntax
                    } => model.GetOperation(node),
                    PrimaryConstructorBaseTypeSyntax => model.GetOperation(node),
                    GlobalStatementSyntax global => model.GetOperation(global.Statement),
                    _ => null,
                };
                if (operation is null)
                    continue;

                foreach (var current in operation.DescendantsAndSelf())
                    VisitOperation(model, current);
            }
        }

        // Running any member of a generic type may run its static initialization
        // first; it is cheaper and still sound to assume that it always does
        for (int definition = 0; definition < _definitions.Count; ++definition)
        {
            if (_definitions[definition] is not IMethodSymbol method)
                continue;
            for (var type = method.ContainingType;
                 type is not null;
                 type = type.ContainingType)
            {
                if (GetTypeInitialization(type) is { } initialization)
                {
                    AddEdge(
                        definition,
                        new Edge(
                            EdgeKind.Direct,
                            initialization,
                            GenericContext.GetTypeArguments(type),
                            null,
                            null,
                            false,
                            method.Locations.FirstOrDefault() ?? Location.None));
                }
            }
        }
    }

    /// <summary>
    /// Records the edges contributed by a single operation.
    /// </summary>
    private void VisitOperation(SemanticModel model, IOperation operation)
    {
        switch (operation)
        {
            case IInvocationOperation invocation:
                AddCall(
                    model,
                    operation,
                    invocation.TargetMethod,
                    invocation.IsVirtual,
                    invocation.ConstrainedToType ??
                        invocation.Instance?.Type as ITypeParameterSymbol);
                break;
            case IMethodReferenceOperation reference:
                AddCall(
                    model,
                    operation,
                    reference.Method,
                    reference.IsVirtual,
                    reference.ConstrainedToType);
                break;
            case IObjectCreationOperation creation:
                if (creation.Constructor is { } constructor)
                    AddCall(model, operation, constructor, false, null);
                if (creation.Type is { } createdType)
                    AddInstantiation(model, operation, createdType, false);
                break;
            case ITypeParameterObjectCreationOperation typeParameterCreation:
                if (typeParameterCreation.Type is { } typeParameter)
                    AddInstantiation(model, operation, typeParameter, true);
                break;
            case IPropertyReferenceOperation property:
                bool isVirtual =
                    property.Instance is not IInstanceReferenceOperation &&
                    (property.Property.IsVirtual ||
                     property.Property.IsAbstract ||
                     property.Property.IsOverride);
                var propertyConstraint = property.ConstrainedToType ??
                    property.Instance?.Type as ITypeParameterSymbol;
                if (property.Property.GetMethod is { } getter)
                    AddCall(model, operation, getter, isVirtual, propertyConstraint);
                if (property.Property.SetMethod is { } setter)
                    AddCall(model, operation, setter, isVirtual, propertyConstraint);
                break;
            case IBinaryOperation { OperatorMethod: { } binary } binaryOperation:
                AddCall(
                    model,
                    operation,
                    binary,
                    false,
                    binaryOperation.ConstrainedToType);
                break;
            case IUnaryOperation { OperatorMethod: { } unary } unaryOperation:
                AddCall(model, operation, unary, false, unaryOperation.ConstrainedToType);
                break;
            case IConversionOperation conversion:
                if (conversion.OperatorMethod is { } conversionMethod)
                {
                    AddCall(
                        model,
                        operation,
                        conversionMethod,
                        false,
                        conversion.ConstrainedToType);
                }
                else if (conversion.Operand.Type is { } operandType &&
                    (operandType.IsValueType || operandType is ITypeParameterSymbol) &&
                    conversion.Type is { IsReferenceType: true })
                {
                    // Boxing makes the value's type a receiver of virtual calls
                    AddInstantiation(model, operation, operandType, false);
                }
                break;
            case ICompoundAssignmentOperation { OperatorMethod: { } compound } assignment:
                AddCall(model, operation, compound, false, assignment.ConstrainedToType);
                break;
            case IIncrementOrDecrementOperation
            {
                OperatorMethod: { } increment
            } incrementOperation:
                AddCall(
                    model,
                    operation,
                    increment,
                    false,
                    incrementOperation.ConstrainedToType);
                break;
            case IFieldReferenceOperation { Field: { IsStatic: true } field }:
                AddTypeInitialization(model, operation, field.ContainingType);
                break;
            case IEventAssignmentOperation
            {
                EventReference: IEventReferenceOperation eventReference
            } eventAssignment:
                AddImplicitCall(
                    model,
                    operation,
                    eventAssignment.Adds
                        ? eventReference.Event.AddMethod
                        : eventReference.Event.RemoveMethod,
                    eventReference.Instance?.Type);
                break;
            case IForEachLoopOperation forEach:
                VisitForEach(model, forEach);
                break;
            case IUsingOperation usingOperation:
                VisitUsing(
                    model,
                    operation,
                    usingOperation.Resources,
                    usingOperation.IsAsynchronous);
                break;
            case IUsingDeclarationOperation usingDeclaration:
                VisitUsing(
                    model,
                    operation,
                    usingDeclaration.DeclarationGroup,
                    usingDeclaration.IsAsynchronous);
                break;
            case IDeconstructionAssignmentOperation
            {
                Syntax: AssignmentExpressionSyntax assignmentSyntax
            }:
                VisitDeconstruction(
                    model,
                    operation,
                    model.GetDeconstructionInfo(assignmentSyntax));
                break;
            case IRecursivePatternOperation
            {
                DeconstructSymbol: IMethodSymbol deconstruct
            }:
                AddImplicitCall(
                    model,
                    operation,
                    deconstruct,
                    deconstruct.ContainingType);
                break;
            case IAwaitOperation { Syntax: AwaitExpressionSyntax awaitSyntax }:
                {
                    var info = model.GetAwaitExpressionInfo(awaitSyntax);
                    AddImplicitCall(
                        model,
                        operation,
                        info.GetAwaiterMethod,
                        (operation as IAwaitOperation)!.Operation.Type);
                    var awaiterType = info.GetAwaiterMethod?.ReturnType;
                    AddImplicitCall(
                        model,
                        operation,
                        info.IsCompletedProperty?.GetMethod,
                        awaiterType);
                    AddImplicitCall(model, operation, info.GetResultMethod, awaiterType);
                }
                break;
            case IImplicitIndexerReferenceOperation implicitIndexer:
                {
                    var receiverType = implicitIndexer.Instance.Type;
                    AddImplicitCall(
                        model,
                        operation,
                        (implicitIndexer.LengthSymbol as IPropertySymbol)?.GetMethod,
                        receiverType);
                    AddImplicitCall(
                        model,
                        operation,
                        implicitIndexer.IndexerSymbol as IMethodSymbol ??
                            (implicitIndexer.IndexerSymbol as IPropertySymbol)?.GetMethod,
                        receiverType);
                }
                break;
            case ICollectionExpressionOperation { ConstructMethod: { } construct }:
                AddImplicitCall(model, operation, construct, null);
                break;
        }
    }

    /// <summary>
    /// Records the calls of a <c>foreach</c> loop: <c>GetEnumerator</c>,
    /// <c>MoveNext</c>, <c>Current</c>, <c>Dispose</c>, a user-defined element
    /// conversion and the deconstruction of the iteration variable.
    /// </summary>
    private void VisitForEach(SemanticModel model, IForEachLoopOperation forEach)
    {
        if (forEach.Syntax is not CommonForEachStatementSyntax syntax)
            return;
        var info = model.GetForEachStatementInfo(syntax);
        var collectionType = forEach.Collection is IConversionOperation
        {
            IsImplicit: true
        } conversion
            ? conversion.Operand.Type
            : forEach.Collection.Type;
        AddImplicitCall(model, forEach, info.GetEnumeratorMethod, collectionType);

        var enumeratorType = info.GetEnumeratorMethod?.ReturnType;
        AddImplicitCall(model, forEach, info.MoveNextMethod, enumeratorType);
        AddImplicitCall(model, forEach, info.CurrentProperty?.GetMethod, enumeratorType);
        AddImplicitCall(model, forEach, info.DisposeMethod, enumeratorType);
        AddImplicitCall(model, forEach, info.ElementConversion.MethodSymbol, null);

        if (syntax is ForEachVariableStatementSyntax variableSyntax)
        {
            VisitDeconstruction(
                model,
                forEach,
                model.GetDeconstructionInfo(variableSyntax));
        }
    }

    /// <summary>
    /// Records the <c>Dispose</c> or <c>DisposeAsync</c> calls of a <c>using</c>
    /// statement or declaration.
    /// </summary>
    private void VisitUsing(
        SemanticModel model,
        IOperation operation,
        IOperation resources,
        bool isAsynchronous)
    {
        var disposable = isAsynchronous
            ? _compilation.GetTypeByMetadataName("System.IAsyncDisposable")
            : _compilation.GetSpecialType(SpecialType.System_IDisposable);
        var slot = disposable
            ?.GetMembers(isAsynchronous ? "DisposeAsync" : "Dispose")
            .OfType<IMethodSymbol>()
            .FirstOrDefault();
        if (slot is null)
            return;

        IEnumerable<ITypeSymbol?> types = resources switch
        {
            IVariableDeclarationGroupOperation group => group.Declarations
                .SelectMany(d => d.Declarators)
                .Select(d => (ITypeSymbol?)d.Symbol.Type),
            _ => [resources.Type],
        };
        foreach (var type in types)
        {
            if (type is null)
                continue;
            if (type is ITypeParameterSymbol typeParameter)
                AddCall(model, operation, slot, false, typeParameter);
            else if (type is INamedTypeSymbol named &&
                ResolveImplementation(named, slot) is { } implementation)
                AddImplicitCall(model, operation, implementation, type);
        }
    }

    /// <summary>
    /// Records the <c>Deconstruct</c> calls of a deconstruction, including nested
    /// ones.
    /// </summary>
    private void VisitDeconstruction(
        SemanticModel model,
        IOperation operation,
        DeconstructionInfo info)
    {
        AddImplicitCall(model, operation, info.Method, info.Method?.ContainingType);
        AddImplicitCall(model, operation, info.Conversion?.MethodSymbol, null);
        foreach (var nested in info.Nested)
            VisitDeconstruction(model, operation, nested);
    }

    /// <summary>
    /// Records a call that C# makes without an invocation in the operation tree,
    /// such as <c>GetEnumerator</c> in a <c>foreach</c> loop.
    /// </summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="operation">The operation that makes the call.</param>
    /// <param name="method">The called method, if any.</param>
    /// <param name="receiverType">The type of the receiver, if known.</param>
    private void AddImplicitCall(
        SemanticModel model,
        IOperation operation,
        IMethodSymbol? method,
        ITypeSymbol? receiverType)
    {
        if (method is null)
            return;
        if (receiverType is ITypeParameterSymbol typeParameter)
        {
            AddCall(model, operation, method, false, typeParameter);
            return;
        }
        bool isVirtual = !method.IsStatic &&
            (method.ContainingType.TypeKind == TypeKind.Interface ||
             method.IsVirtual ||
             method.IsAbstract ||
             method.IsOverride);
        AddCall(model, operation, method, isVirtual, null);
    }

    /// <summary>
    /// Records that code at <paramref name="operation"/> may run the static
    /// initialization of <paramref name="type"/> and of its containing types.
    /// </summary>
    private void AddTypeInitialization(
        SemanticModel model,
        IOperation operation,
        INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (GetTypeInitialization(current) is not { } initialization)
                continue;
            foreach (int caller in GetCallers(model, operation))
            {
                AddEdge(
                    caller,
                    new Edge(
                        EdgeKind.Direct,
                        initialization,
                        GenericContext.GetTypeArguments(current),
                        null,
                        null,
                        false,
                        operation.Syntax.GetLocation()));
            }
        }
    }

    /// <summary>
    /// Returns the index of the static initialization of a generic source type:
    /// its static constructor and static field and property initializers, or
    /// <see langword="null"/> if it has none or is not generic.
    /// </summary>
    private int? GetTypeInitialization(INamedTypeSymbol type)
    {
        var definition = type.OriginalDefinition;
        if (!_staticInitialization.TryGetValue(definition, out bool hasInitialization))
        {
            hasInitialization =
                GenericContext.GetTypeParameters(definition).Length > 0 &&
                definition.GetMembers().Any(member => member switch
                {
                    IMethodSymbol { MethodKind: MethodKind.StaticConstructor } =>
                        !member.IsImplicitlyDeclared,
                    IFieldSymbol { IsStatic: true } or
                    IPropertySymbol { IsStatic: true } =>
                        member.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is
                            VariableDeclaratorSyntax { Initializer: not null } or
                            PropertyDeclarationSyntax { Initializer: not null }),
                    _ => false,
                });
            _staticInitialization[definition] = hasInitialization;
        }
        return hasInitialization ? GetDefinitionIndex(definition) : null;
    }

    /// <summary>
    /// Returns the definitions that code at <paramref name="operation"/> belongs
    /// to. Static initializers and static constructors belong to the static
    /// initialization of their type; instance initializers belong to every
    /// constructor that does not chain to another constructor of the same type.
    /// </summary>
    private List<int> GetCallers(SemanticModel model, IOperation operation)
    {
        var symbol = Normalize(model.GetEnclosingSymbol(operation.Syntax.SpanStart));
        switch (symbol)
        {
            case null:
                return [];
            case IMethodSymbol { MethodKind: MethodKind.StaticConstructor }:
            case IFieldSymbol { IsStatic: true }:
            case IPropertySymbol { IsStatic: true }:
            case IEventSymbol { IsStatic: true }:
                return [GetDefinitionIndex(symbol.ContainingType.OriginalDefinition)];
            case IFieldSymbol or IPropertySymbol or IEventSymbol:
                return [.. symbol.ContainingType.OriginalDefinition.InstanceConstructors
                    .Where(c => !ChainsToOwnConstructor(c))
                    .Select(c => GetDefinitionIndex(Normalize(c)!))];
            default:
                return [GetDefinitionIndex(symbol)];
        }
    }

    /// <summary>
    /// Returns true if <paramref name="constructor"/> calls another constructor of
    /// its type with <c>this(...)</c>, which runs the instance initializers instead.
    /// </summary>
    private static bool ChainsToOwnConstructor(IMethodSymbol constructor) =>
        constructor.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is
            ConstructorDeclarationSyntax
            {
                Initializer.ThisOrBaseKeyword.RawKind: (int)SyntaxKind.ThisKeyword
            });

    /// <summary>
    /// Records an edge for a call of <paramref name="target"/>.
    /// </summary>
    private void AddCall(
        SemanticModel model,
        IOperation operation,
        IMethodSymbol target,
        bool isVirtual,
        ITypeSymbol? constrainedType)
    {
        if (isVirtual || constrainedType is not null)
        {
            foreach (int dispatchCaller in GetCallers(model, operation))
            {
                AddEdge(
                    dispatchCaller,
                    new Edge(
                        EdgeKind.Dispatch,
                        -1,
                        [],
                        target,
                        constrainedType,
                        false,
                        operation.Syntax.GetLocation()));
            }
            RegisterPotentialImplementations(target);
            return;
        }

        var definition = Normalize(target);
        if (definition is null ||
            !IsInSource(definition) ||
            GenericContext.GetTypeParameters(definition).Length == 0)
        {
            return;
        }

        foreach (int caller in GetCallers(model, operation))
        {
            AddEdge(
                caller,
                new Edge(
                    EdgeKind.Direct,
                    GetDefinitionIndex(definition),
                    GenericContext.GetTypeArguments(target),
                    null,
                    null,
                    false,
                    operation.Syntax.GetLocation()));
        }
    }

    /// <summary>
    /// Records an edge for the instantiation of <paramref name="type"/>.
    /// </summary>
    private void AddInstantiation(
        SemanticModel model,
        IOperation operation,
        ITypeSymbol type,
        bool isDefaultConstruction)
    {
        if (type is INamedTypeSymbol named && !IsInSource(named.OriginalDefinition))
            return;
        foreach (int caller in GetCallers(model, operation))
        {
            AddEdge(
                caller,
                new Edge(
                    EdgeKind.Instantiate,
                    -1,
                    [],
                    null,
                    type,
                    isDefaultConstruction,
                    operation.Syntax.GetLocation()));
        }
    }

    /// <summary>
    /// Registers every method in source that may implement or override
    /// <paramref name="slot"/>, matched by name and arity, so that relevance and
    /// tracked type parameters can flow through dispatched calls.
    /// </summary>
    private void RegisterPotentialImplementations(IMethodSymbol slot)
    {
        var key = $"{slot.Name}`{slot.Arity}";
        if (_potentialImplementations.ContainsKey(key))
            return;
        var list = new List<int>();
        _potentialImplementations[key] = list;

        // Explicit interface implementations have qualified names
        foreach (var symbol in _compilation.GetSymbolsWithName(
            name => name == slot.Name ||
                name.EndsWith("." + slot.Name, StringComparison.Ordinal),
            SymbolFilter.Member))
        {
            if (symbol is IMethodSymbol method &&
                method.Arity == slot.Arity &&
                Normalize(method) is { } definition &&
                GenericContext.GetTypeParameters(definition).Length > 0)
            {
                list.Add(GetDefinitionIndex(definition));
            }
        }
    }

    /// <summary>
    /// Records an edge unless the caller already has an equal one.
    /// </summary>
    private void AddEdge(int caller, Edge edge)
    {
        var key = edge.Kind switch
        {
            EdgeKind.Direct => $"d|{edge.Callee}|{GenericContext.GetKey(edge.Arguments)}",
            EdgeKind.Dispatch =>
                $"v|{edge.Slot!.ToDisplayString(GenericContext.KeyFormat)}|" +
                $"{edge.Type?.ToDisplayString(GenericContext.KeyFormat)}",
            _ => $"i|{edge.Type!.ToDisplayString(GenericContext.KeyFormat)}|" +
                $"{edge.IsDefaultConstruction}",
        };
        if (!_edgeKeys[caller].Add(key))
            return;
        _edges[caller].Add(edge);
        foreach (int callee in GetPotentialCallees(edge))
            _callers[callee].Add(caller);
    }

    /// <summary>
    /// Returns the definitions an edge may run.
    /// </summary>
    private List<int> GetPotentialCallees(Edge edge) =>
        edge.Kind switch
        {
            EdgeKind.Direct => [edge.Callee],
            EdgeKind.Dispatch => _potentialImplementations.GetValueOrDefault(
                $"{edge.Slot!.Name}`{edge.Slot.Arity}") ?? [],
            _ => [],
        };

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="symbol"/> is declared in
    /// source code of this compilation.
    /// </summary>
    private static bool IsInSource(ISymbol symbol) =>
        symbol.Locations.Any(l => l.IsInSource);

    /// <summary>
    /// Computes, for every definition, the positions of its type parameters that
    /// can flow into the type arguments of an open launch site. Direct calls map
    /// positions exactly; dispatched calls and instantiations are followed
    /// conservatively: every type parameter they mention is tracked if a
    /// potential callee, or a method of the instantiated type, tracks any.
    /// </summary>
    private void ComputeTrackedPositions()
    {
        // Potential callees of dispatch edges may have been registered after the
        // edge, so rebuild the reverse edges now that all definitions are known
        for (int caller = 0; caller < _definitions.Count; ++caller)
        {
            foreach (var edge in _edges[caller])
            {
                foreach (int callee in GetPotentialCallees(edge))
                    _callers[callee].Add(caller);
            }
        }

        for (int definition = 0; definition < _definitions.Count; ++definition)
        {
            foreach (var site in _openSites[definition])
            {
                foreach (var argument in site.Arguments)
                    Track(definition, argument);
            }
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int caller = 0; caller < _definitions.Count; ++caller)
            {
                foreach (var edge in _edges[caller])
                {
                    switch (edge.Kind)
                    {
                        case EdgeKind.Direct:
                            foreach (int position in _trackedPositions[edge.Callee])
                            {
                                if (position < edge.Arguments.Length)
                                    changed |= Track(caller, edge.Arguments[position]);
                            }
                            break;
                        case EdgeKind.Dispatch:
                            if (!GetPotentialCallees(edge).Any(IsTracking))
                                break;
                            foreach (var argument in GenericContext.GetTypeArguments(
                                edge.Slot!))
                            {
                                changed |= Track(caller, argument);
                            }
                            if (edge.Type is { } receiver)
                                changed |= Track(caller, receiver);
                            break;
                        case EdgeKind.Instantiate:
                            if (HasTrackingMembers(edge.Type!))
                                changed |= Track(caller, edge.Type!);
                            break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Marks the definitions worth instantiating: those that can reach an open
    /// launch site, or that create objects whose methods can, through direct
    /// calls or potential dispatch targets. Others are never instantiated.
    /// </summary>
    private void ComputeRelevantDefinitions()
    {
        _relevant = new bool[_definitions.Count];
        var worklist = new Stack<int>();
        for (int definition = 0; definition < _definitions.Count; ++definition)
        {
            bool createsTrackingObjects = _edges[definition].Any(edge =>
                edge.Kind == EdgeKind.Instantiate && HasTrackingMembers(edge.Type!));
            if (_openSites[definition].Count > 0 || createsTrackingObjects)
            {
                _relevant[definition] = true;
                worklist.Push(definition);
            }
        }

        while (worklist.Count > 0)
        {
            foreach (int caller in _callers[worklist.Pop()])
            {
                if (!_relevant[caller])
                {
                    _relevant[caller] = true;
                    worklist.Push(caller);
                }
            }
        }
    }

    /// <summary>
    /// Tracks every type parameter of <paramref name="definition"/> that
    /// <paramref name="type"/> mentions.
    /// </summary>
    /// <returns>True if a position was newly tracked.</returns>
    private bool Track(int definition, ITypeSymbol type)
    {
        bool changed = false;
        var parameters = GenericContext.GetTypeParameters(_definitions[definition]);
        for (int i = 0; i < parameters.Length; ++i)
        {
            if (Mentions(type, parameters[i]))
                changed |= _trackedPositions[definition].Add(i);
        }
        return changed;
    }

    /// <summary>
    /// Returns true if a definition tracks a type parameter or has an open site.
    /// </summary>
    private bool IsTracking(int definition) =>
        _trackedPositions[definition].Count > 0 || _openSites[definition].Count > 0;

    /// <summary>
    /// Returns true if a method of <paramref name="type"/>'s definition (or a base
    /// type's) has tracked type parameters or open launch sites.
    /// </summary>
    private bool HasTrackingMembers(ITypeSymbol type)
    {
        for (var current = (type as INamedTypeSymbol)?.OriginalDefinition;
             current is not null;
             current = current.BaseType?.OriginalDefinition)
        {
            foreach (var member in current.GetMembers().OfType<IMethodSymbol>())
            {
                if (Normalize(member) is { } definition &&
                    _definitionIndices.TryGetValue(definition, out int index) &&
                    IsTracking(index))
                {
                    return true;
                }
            }
        }
        return type is ITypeParameterSymbol;
    }

    /// <summary>
    /// Returns true if <paramref name="type"/> mentions <paramref name="parameter"/>.
    /// </summary>
    private static bool Mentions(ITypeSymbol type, ITypeParameterSymbol parameter) =>
        type switch
        {
            ITypeParameterSymbol other =>
                GenericContext.DefinitionComparer.Equals(other, parameter),
            IArrayTypeSymbol array => Mentions(array.ElementType, parameter),
            IPointerTypeSymbol pointer => Mentions(pointer.PointedAtType, parameter),
            INamedTypeSymbol named =>
                named.TypeArguments.Any(t => Mentions(t, parameter)) ||
                named.ContainingType is { } container && Mentions(container, parameter),
            _ => false,
        };

    #endregion

    #region Diagnostics

    /// <summary>
    /// Reports a launch site that no closed instantiation reaches.
    /// </summary>
    private void ReportOpenLaunchSite(KernelDescriptor kernel)
    {
        var invocation = kernel.LaunchInfo.Invocation;
        var model = _models[invocation.SyntaxTree];
        var enclosing = Normalize(model.GetEnclosingSymbol(invocation.SpanStart));

        var entryPoints = new List<string>();
        if (enclosing is not null
            && _definitionIndices.TryGetValue(enclosing, out int start))
        {
            var seen = new HashSet<int> { start };
            var worklist = new Stack<int>();
            worklist.Push(start);
            while (worklist.Count > 0)
            {
                int current = worklist.Pop();
                if (_callers[current].Count == 0)
                    entryPoints.Add(Describe(current));
                foreach (int caller in _callers[current])
                {
                    if (seen.Add(caller))
                        worklist.Push(caller);
                }
            }
        }

        var entryPointText = entryPoints.Count > 0
            ? " It is only reachable from " +
              string.Join(", ", entryPoints.Take(5).Select(e => $"'{e}'")) +
              (entryPoints.Count > 5 ? ", ..." : "") + "."
            : "";
        _diagnostics.Add(BuildDiagnostic.Warning(
            BuildDiagnosticCodes.OpenLaunchSite,
            invocation.GetLocation(),
            $"Kernel '{kernel.DisplayName}' is launched with " +
            $"open type arguments, and no code in this compilation calls it with " +
            $"closed type arguments, so no kernel is compiled for this launch site " +
            $"and launching it throws a NotSupportedException.{entryPointText}"));
    }

    /// <summary>
    /// Reports every externally visible method whose type arguments reach the
    /// launch site of <paramref name="kernel"/>: other assemblies may call it with
    /// type arguments that have no compiled kernel.
    /// </summary>
    private void ReportOpenPublicEntryPoints(
        KernelDescriptor kernel,
        HashSet<int> reported)
    {
        var invocation = kernel.LaunchInfo.Invocation;
        var model = _models[invocation.SyntaxTree];
        var enclosing = Normalize(model.GetEnclosingSymbol(invocation.SpanStart));
        if (enclosing is null ||
            !_definitionIndices.TryGetValue(enclosing, out int start))
        {
            return;
        }

        var seen = new HashSet<int> { start };
        var worklist = new Stack<int>();
        worklist.Push(start);
        while (worklist.Count > 0)
        {
            int current = worklist.Pop();
            if (_definitions[current] is IMethodSymbol method &&
                _trackedPositions[current].Count > 0 &&
                IsExternallyVisible(method) &&
                reported.Add(current))
            {
                _diagnostics.Add(BuildDiagnostic.Warning(
                    BuildDiagnosticCodes.OpenPublicEntryPoint,
                    method.Locations.FirstOrDefault(l => l.IsInSource),
                    $"'{Describe(current)}' is visible to other assemblies and " +
                    $"passes its type arguments to a launch of kernel " +
                    $"'{kernel.DisplayName}'. Kernels are only " +
                    $"compiled for the type arguments this compilation uses; a " +
                    $"launch with other type arguments throws a " +
                    $"NotSupportedException."));
            }
            foreach (int caller in _callers[current])
            {
                if (seen.Add(caller))
                    worklist.Push(caller);
            }
        }
    }

    /// <summary>
    /// Returns true if other assemblies can call <paramref name="method"/>.
    /// </summary>
    private static bool IsExternallyVisible(IMethodSymbol method)
    {
        for (ISymbol? symbol = method; symbol is IMethodSymbol or INamedTypeSymbol;
             symbol = symbol.ContainingSymbol)
        {
            if (symbol.DeclaredAccessibility is not (
                Accessibility.Public or
                Accessibility.Protected or
                Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Describes a definition for diagnostics.
    /// </summary>
    private string Describe(int definition) =>
        _definitions[definition].ToDisplayString(
            SymbolDisplayFormat.CSharpErrorMessageFormat);

    /// <summary>
    /// Describes an instantiation for diagnostics, for example
    /// <c>Program.Run&lt;T&gt;() with T = Box&lt;int&gt;</c>.
    /// </summary>
    private string Describe(int definition, ImmutableArray<ITypeSymbol> arguments)
    {
        var parameters = GenericContext.GetTypeParameters(_definitions[definition]);
        var bindings = parameters
            .Zip(arguments, (p, a) => $"{p.Name} = {a.ToDisplayString()}");
        return $"{Describe(definition)} with {string.Join(", ", bindings)}";
    }

    /// <summary>
    /// Describes the chain of instantiations that led to <paramref name="item"/>.
    /// </summary>
    private string DescribeChain(Item item)
    {
        var chain = new List<string>();
        for (var current = item; current is not null; current = current.Parent)
            chain.Add(Describe(current.Definition, current.Arguments));
        chain.Reverse();
        if (chain.Count > 6)
            chain = [.. chain.Take(2), "...", .. chain.Skip(chain.Count - 3)];
        return string.Join(" -> ", chain);
    }

    #endregion
}
