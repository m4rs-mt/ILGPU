// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CFG.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Represents a control-flow graph (CFG).
/// </summary>
/// <typeparam name="TOrder">The underlying block order.</typeparam>
/// <typeparam name="TDirection">The control-flow direction.</typeparam>
sealed class CFG<TOrder, TDirection>
    where TOrder : struct, ITraversalOrder<BasicBlock>
    where TDirection : struct, IControlFlowDirection
{
    #region Nested Types and Helpers

    /// <summary>
    /// Represents a single node in the scope of a control-flow graph.
    /// </summary>
    /// <remarks>
    /// Constructs a new node.
    /// </remarks>
    /// <param name="cfg">The parent graph.</param>
    /// <param name="block">The associated block.</param>
    /// <param name="traversalIndex">The traversal index.</param>
    internal readonly struct Node(
        CFG<TOrder, TDirection> cfg,
        BasicBlock block,
        int traversalIndex) : ILocation
    {
        /// <summary>
        /// Returns the associated block.
        /// </summary>
        public BasicBlock Block { get; } = block;

        /// <summary>
        /// Returns the zero-based traversal index that has been assigned during
        /// traversal of all input blocks.
        /// </summary>
        public int TraversalIndex { get; } = traversalIndex;

        /// <summary>
        /// Returns the predecessors of this node.
        /// </summary>
        public NodeCollection Predecessors =>
            new(cfg, Block.GetPredecessors<TDirection>());

        /// <summary>
        /// Returns the successors of this node.
        /// </summary>
        public NodeCollection Successors =>
            new(cfg, Block.GetSuccessors<TDirection>());

        /// <summary>
        /// Returns the number of predecessors.
        /// </summary>
        public int NumPredecessors => Predecessors.Count;

        /// <summary>
        /// Returns the number of successors.
        /// </summary>
        public int NumSuccessors => Successors.Count;

        /// <inheritdoc cref="ILocation.FormatErrorMessage(string)"/>
        string ILocation.FormatErrorMessage(string message) =>
            Block.FormatErrorMessage(message);

        public static implicit operator BasicBlock(Node node) => node.Block;
    }

    /// <summary>
    /// Represents a node collection of attached nodes.
    /// </summary>
    /// <param name="cfg">The parent graph.</param>
    /// <param name="links">The block links.</param>
    internal readonly ref struct NodeCollection(
        CFG<TOrder, TDirection> cfg,
        ReadOnlySpan<BasicBlock> links)
    {
        private readonly ReadOnlySpan<BasicBlock> _links = links;

        /// <summary>
        /// Returns the number of nodes.
        /// </summary>
        public int Count => _links.Length;

        /// <summary>
        /// Returns the i-th node.
        /// </summary>
        /// <param name="index">The relative node index.</param>
        /// <returns>The resolved node.</returns>
        public Node this[int index] =>
            new(cfg, _links[index], cfg._numbering[_links[index]]);

        /// <summary>
        /// Returns an enumerator to iterate over all attached nodes.
        /// </summary>
        /// <returns>The resulting node enumerator.</returns>
        public Enumerator GetEnumerator() => new(cfg, _links);
    }

    /// <summary>
    /// Enumerates all CFG nodes.
    /// </summary>
    /// <param name="cfg">The parent graph.</param>
    /// <param name="links">The node links.</param>
    internal ref struct Enumerator(
        CFG<TOrder, TDirection> cfg,
        ReadOnlySpan<BasicBlock> links)
    {
        private ReadOnlySpan<BasicBlock>.Enumerator _enumerator = links.GetEnumerator();

        /// <summary>
        /// Returns the current CFG node.
        /// </summary>
        public Node Current => new(
            cfg,
            _enumerator.Current,
            cfg._numbering[_enumerator.Current]);

        /// <summary cref="IEnumerator.MoveNext"/>
        public bool MoveNext() => _enumerator.MoveNext();
    }

    /// <summary>
    /// Creates a new CFG.
    /// </summary>
    /// <param name="blocks">The block collection.</param>
    public static CFG<TOrder, TDirection> Create(
        in BasicBlockCollection<TOrder, TDirection> blocks) => new(blocks);

    #endregion

    #region CFG

    private readonly ValueMap<Method, BasicBlock, int> _numbering;
    private InlineList<BasicBlock> _blocks;

    /// <summary>
    /// Constructs a new CFG.
    /// </summary>
    /// <param name="blocks">The block collection.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal CFG(in BasicBlockCollection<TOrder, TDirection> blocks)
    {
        _numbering = blocks.CreateMap((_, index) => index);
        _blocks = blocks.ToList();
        Blocks = blocks;
    }

    /// <summary>
    /// Returns the underlying blocks.
    /// </summary>
    public BasicBlockCollection<TOrder, TDirection> Blocks { get; }

    /// <summary>
    /// Returns the underlying blocks as materialized span.
    /// </summary>
    public ReadOnlySpan<BasicBlock> BlockSpan => _blocks;

    /// <summary>
    /// Returns the number of nodes in the graph.
    /// </summary>
    public int Count => _numbering.Count;

    /// <summary>
    /// Returns the root node.
    /// </summary>
    public Node Root => new(this, Blocks.EntryBlock, _numbering[Blocks.EntryBlock]);

    /// <summary>
    /// Resolves the CFG node for the given basic block.
    /// </summary>
    /// <param name="block">The basic block to resolve.</param>
    /// <returns>The resolved basic block.</returns>
    public Node this[BasicBlock block] => new(this, block, _numbering[block]);

    /// <summary>
    /// Returns an enumerator to iterate over all nodes stored in this graph
    /// using the current order.
    /// </summary>
    /// <returns>The resulting node enumerator.</returns>
    public Enumerator GetEnumerator() => new(this, _blocks);

    #endregion
}

/// <summary>
/// Helper utility for the class <see cref="CFG{TOrder, TDirection}"/>.
/// </summary>
static class CFG
{
    /// <summary>
    /// Creates a new CFG based on the given blocks.
    /// </summary>
    /// <typeparam name="TOrder">The underlying block order.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="blocks">The block collection.</param>
    /// <returns>The created CFG.</returns>
    public static CFG<TOrder, TDirection> CreateCFG<TOrder, TDirection>(
        this BasicBlockCollection<TOrder, TDirection> blocks)
        where TOrder : struct, ITraversalOrder<BasicBlock>
        where TDirection : struct, IControlFlowDirection =>
        CFG<TOrder, TDirection>.Create(blocks);

    /// <summary>
    /// Creates a new CFG based on the given module.
    /// </summary>
    /// <typeparam name="TOrder">The underlying block order.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="module">The source module.</param>
    /// <returns>The created CFGs.</returns>
    public static ValueMap<Module, Method, CFG<TOrder, TDirection>> CreateCFGs<
        TOrder, TDirection>(this Module module)
        where TOrder : struct, ITraversalOrder<BasicBlock>
        where TDirection : struct, IControlFlowDirection =>
        module.CreateMap<Method, CFG<TOrder, TDirection>>(method =>
            CFG<TOrder, TDirection>.Create(
                method.Blocks.ChangeOrder<TOrder, TDirection>()));
}
