// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CallGraph.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Call?graph wrapper that enables fast traversal over methods.
/// </summary>
/// <typeparam name="TDirection">
/// Control?flow direction (always <see cref="Forwards"/> for call graphs).
/// </typeparam>
sealed class CallGraph<TDirection>
    where TDirection : struct, IControlFlowDirection
{
    #region Nested Types

    /// <summary>
    /// A node in the call-graph.
    /// </summary>
    internal readonly struct Node(
        CallGraph<TDirection> graph,
        Method method,
        int traversalIndex) : ILocation
    {
        /// <summary>Method represented by this node.</summary>
        public Method Method { get; } = method;

        /// <summary>
        /// Zero-based index that was assigned during the traversal that built the graph.
        /// </summary>
        public int TraversalIndex { get; } = traversalIndex;

        /// <summary>
        /// All callee methods (forward edges).
        /// </summary>
        public ReadOnlySpan<Method> Callees => Method.CalledMethods;

        /// <summary>
        /// All caller methods (backward edges).
        /// </summary>
        public ReadOnlySpan<Method> Callers =>
            graph._callersMap.TryGetValue(Method, out var list)
                ? CollectionsMarshal.AsSpan(list) : [];

        /// <inheritdoc/>
        string ILocation.FormatErrorMessage(string message) =>
            Method.FormatErrorMessage(message);
    }

    /// <summary>
    /// Collection of nodes, provides count, indexer and enumerator.
    /// </summary>
    internal readonly ref struct NodeCollection(
        CallGraph<TDirection> graph,
        ReadOnlySpan<Method> methods)
    {
        private readonly ReadOnlySpan<Method> _methods = methods;

        public int Count => _methods.Length;

        public Node this[int index] => new(
            graph,
            _methods[index],
            graph._numbering[_methods[index]]);

        public Enumerator GetEnumerator() => new(graph, _methods);
    }

    /// <summary>
    /// Enumerator for <see cref="NodeCollection"/>.
    /// </summary>
    internal ref struct Enumerator(
        CallGraph<TDirection> graph,
        ReadOnlySpan<Method> methods)
    {
        private ReadOnlySpan<Method>.Enumerator _enumerator =
            methods.GetEnumerator();

        public readonly Node Current => new(
            graph,
            _enumerator.Current,
            graph._numbering[_enumerator.Current]);

        public bool MoveNext() => _enumerator.MoveNext();
    }

    #endregion

    #region Call Graph

    private readonly InlineList<Method> _methods;
    private readonly ValueMap<Module, Method, int> _numbering;
    private readonly ValueMap<Module, Method, List<Method>> _callersMap;

    /// <summary>
    /// Creates a new call-graph for the supplied <paramref name="module"/>.
    /// The graph is built once and then can be traversed repeatedly without
    /// recomputation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public CallGraph(Module module)
    {
        _methods = TDirection.IsForwards
            ? module.MethodsInReversePostOrder.ToInlineList()
            : module.MethodsInPostOrder.ToInlineList();

        _numbering = module.CreateMap<Method, int>();
        int index = 0;
        foreach (var method in _methods)
            _numbering.Add(method, index++);

        _callersMap = module.CreateMap<Method, List<Method>>(static _ => new(8));
        foreach (var method in _methods)
        {
            foreach (var callee in method.CalledMethods)
            {
                // External methods may not be in the module's method ordering
                if (_callersMap.TryGetValue(callee, out var callers))
                    callers.Add(method);
            }
        }
    }

    /// <summary>
    /// All methods in the order defined by <typeparamref name="TDirection"/>.
    /// </summary>
    public NodeCollection Methods => new(this, _methods);

    /// <summary>
    /// Retrieves the node for a specific method (O(1) via the numbering map).
    /// </summary>
    public Node this[Method method] => new(
        this,
        method,
        _numbering[method]);

    #endregion
}

/// <summary>
/// Helper utility for the class <see cref="CallGraph{TDirection}"/>
/// </summary>
static class CallGraph
{
    /// <summary>
    /// Creates a new call graph based on the given blocks.
    /// </summary>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="module">The module.</param>
    /// <returns>The created call graph.</returns>
    public static CallGraph<TDirection> CreateCallGraph<TDirection>(
        this Module module)
        where TDirection : struct, IControlFlowDirection =>
        new(module);
}
