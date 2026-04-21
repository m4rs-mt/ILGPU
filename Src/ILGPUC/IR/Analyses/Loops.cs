// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Loops.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Util;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

//
// Based on an adapted version of the paper: Identifying Loops In Almost Linear Time
//

namespace ILGPUC.IR.Analyses;

/// <summary>
/// An analysis to detect strongly-connected components (loops) and provide
/// structured access to the loop tree.
/// </summary>
/// <typeparam name="TOrder">The current order.</typeparam>
/// <typeparam name="TDirection">The control-flow direction.</typeparam>
sealed class Loops<TOrder, TDirection>
    where TOrder : struct, ITraversalOrder<BasicBlock>
    where TDirection : struct, IControlFlowDirection
{
    #region Nested Types

    /// <summary>
    /// A specialized successor provider for loop members that exclude all exit
    /// blocks of an associated loop.
    /// </summary>
    /// <typeparam name="TOtherDirection">The target direction.</typeparam>
    readonly struct MembersSuccessorProvider<TOtherDirection> :
        ITraversalSuccessorsProvider<BasicBlock, TOtherDirection>
        where TOtherDirection : struct, IControlFlowDirection
    {
        /// <summary>
        /// Constructs a new successor provider.
        /// </summary>
        /// <param name="node">The loop node.</param>
        public MembersSuccessorProvider(Node node)
        {
            Node = node;
        }

        /// <summary>
        /// Returns the associated loop node.
        /// </summary>
        public Node Node { get; }

        /// <summary>
        /// Returns the successors of the given basic block that do not contain any
        /// of the associated loop exit blocks.
        /// </summary>
        public static ReadOnlySpan<BasicBlock> GetSuccessors(BasicBlock basicBlock)
        {
            var node = default(MembersSuccessorProvider<TOtherDirection>).Node;
            var successors = basicBlock.GetSuccessors<TOtherDirection>();

            // Check for an entry block
            if (node.Entries.Contains(basicBlock))
                return AdjustEntrySuccessors(node, successors);

            // Check for exit blocks
            foreach (var exit in node.Exits)
            {
                if (successors.Contains(exit, new BasicBlock.BlockComparer()))
                    return AdjustExitSuccessors(node, successors);
            }

            // Use the original successors
            return successors;
        }

        /// <summary>
        /// Helper function to adjust the header span of the current successors.
        /// </summary>
        private static ReadOnlySpan<BasicBlock> AdjustEntrySuccessors(
            Node node,
            ReadOnlySpan<BasicBlock> currentSuccessors)
        {
            var successors = InlineList<BasicBlock>.Create(currentSuccessors.Length);
            foreach (var successor in currentSuccessors)
            {
                if (node.Headers.Contains(successor, new BasicBlock.BlockComparer()))
                    successors.Add(successor);
            }
            return successors;
        }

        /// <summary>
        /// Helper function to adjust the exit span of the current successors.
        /// </summary>
        private static ReadOnlySpan<BasicBlock> AdjustExitSuccessors(
            Node node,
            ReadOnlySpan<BasicBlock> currentSuccessors)
        {
            var successors = currentSuccessors.ToInlineList();
            foreach (var exit in node.Exits)
                successors.RemoveAll(exit, new BasicBlock.BlockComparer());
            return successors;
        }
    }

    /// <summary>
    /// Represents a single loop in the loop tree (a strongly-connected component).
    /// </summary>
    /// <remarks>
    /// Each Node represents one loop in the program, containing information about:
    /// - Loop structure (headers, entries, exits, breakers, back edges)
    /// - Loop members (all blocks belonging to this loop)
    /// - Loop hierarchy (parent, children, nesting level)
    /// - Query methods for block membership and traversal
    /// </remarks>
    internal sealed class Node
    {
        private InlineList<BasicBlock> headers;
        private InlineList<BasicBlock> breakers;
        private InlineList<BasicBlock> backEdges;
        private InlineList<Node> children;

        /// <summary>
        /// Constructs a new loop node.
        /// </summary>
        /// <param name="parent">The parent loop.</param>
        /// <param name="headerBlocks">All loop headers.</param>
        /// <param name="breakerBlocks">All blocks that can break the loop.</param>
        /// <param name="backEdgeBlocks">All blocks with back edges.</param>
        /// <param name="members">All blocks in the scope of this loop.</param>
        /// <param name="entries">All entry block that jump into this loop.</param>
        /// <param name="exits">All exit block that this loop can jump to.</param>
        internal Node(
            Node? parent,
            ref InlineList<BasicBlock> headerBlocks,
            ref InlineList<BasicBlock> breakerBlocks,
            ref InlineList<BasicBlock> backEdgeBlocks,
            in ValueSetList<Method, BasicBlock> members,
            HashSet<BasicBlock> entries,
            HashSet<BasicBlock> exits)
        {
            Parent = parent;
            parent?.AddChild(this);
            Level = (parent?.Level ?? -1) + 1;

            headerBlocks.MoveTo(ref headers);
            breakerBlocks.MoveTo(ref breakers);
            backEdgeBlocks.MoveTo(ref backEdges);
            AllMembers = members;
            children = InlineList<Node>.Create(8);
            Entries = [.. entries];
            Exits = [.. exits];
        }

        #region Structure Properties

        /// <summary>
        /// Returns all entry blocks that jump into the loop (from outside).
        /// </summary>
        /// <remarks>
        /// Entry blocks are predecessors of loop headers that are not part of the loop.
        /// </remarks>
        public ImmutableArray<BasicBlock> Entries { get; }

        /// <summary>
        /// Returns all loop header blocks.
        /// </summary>
        /// <remarks>
        /// Header blocks are the first blocks of the loop that have predecessors outside
        /// the loop (entry blocks).
        /// </remarks>
        public ReadOnlySpan<BasicBlock> Headers => headers;

        /// <summary>
        /// Returns all blocks that can break out of the loop.
        /// </summary>
        /// <remarks>
        /// Breaker blocks have successors that are exit blocks (outside the loop).
        /// </remarks>
        public ReadOnlySpan<BasicBlock> Breakers => breakers;

        /// <summary>
        /// Returns all exit blocks that are reachable from the loop.
        /// </summary>
        /// <remarks>
        /// Exit blocks are successors of breaker blocks that are not part of the loop.
        /// </remarks>
        public ImmutableArray<BasicBlock> Exits { get; }

        /// <summary>
        /// Returns all blocks with back edges to the loop header.
        /// </summary>
        /// <remarks>
        /// Back edge blocks are blocks inside the loop that jump back to a header block,
        /// forming the actual loop cycle.
        /// </remarks>
        public ReadOnlySpan<BasicBlock> BackEdges => backEdges;

        /// <summary>
        /// Returns all blocks that are part of this loop.
        /// </summary>
        /// <remarks>
        /// This includes all blocks in the loop body, headers, breakers, and back edges.
        /// For nested loops, this includes blocks from child loops.
        /// </remarks>
        public ValueSetList<Method, BasicBlock> AllMembers { get; }

        #endregion

        #region Hierarchy Properties

        /// <summary>
        /// Returns the nesting level of this loop (0 for top-level loops).
        /// </summary>
        public int Level { get; }

        /// <summary>
        /// Returns the number of blocks in this loop.
        /// </summary>
        public int Count => AllMembers.Count;

        /// <summary>
        /// Returns the parent loop.
        /// </summary>
        public Node? Parent { get; }

        /// <summary>
        /// Returns all child loops nested inside this loop.
        /// </summary>
        public ReadOnlySpan<Node> Children => children;

        /// <summary>
        /// Returns true if this is a nested loop.
        /// </summary>
        public bool IsNested => Parent != null;

        /// <summary>
        /// Returns true if this is an innermost loop (has no child loops).
        /// </summary>
        public bool IsInnermost => children.Count < 1;

        #endregion

        #region Methods

        /// <summary>
        /// Checks whether the given block belongs to this loop.
        /// </summary>
        /// <param name="block">The block to check.</param>
        /// <returns>True if the block is part of this loop.</returns>
        public bool Contains(BasicBlock? block) =>
            block != null && AllMembers.Contains(block);

        /// <summary>
        /// Checks whether the given block belongs exclusively to this loop
        /// (not to a nested child loop).
        /// </summary>
        /// <param name="block">The block to check.</param>
        /// <returns>
        /// True if the block is part of this loop but not part of any child loop.
        /// </returns>
        public bool ContainsExclusively(BasicBlock block)
        {
            // The given block can be null when querying the block of a parameter
            if (!Contains(block))
                return false;

            // Exclude nested blocks of nested loops
            if (IsInnermost)
                return true;
            foreach (var child in Children)
            {
                if (child.Contains(block))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Adds the given child node.
        /// </summary>
        /// <param name="child">The child node to add.</param>
        private void AddChild(Node child)
        {
            Debug.Assert(child.Parent == this, "Invalid child");
            children.Add(child);
        }

        /// <summary>
        /// Computes a block ordering of all blocks in this loop using the current
        /// order and control-flow direction.
        /// </summary>
        /// <param name="entryIndex">
        /// The index of the entry block to use as starting point (default: 0).
        /// </param>
        /// <returns>The computed block ordering.</returns>
        public BasicBlockCollection<TOrder, TDirection> ComputeOrderedBlocks(
            int entryIndex = 0) =>
            ComputeOrderedBlocks<TOrder, TDirection>(entryIndex);

        /// <summary>
        /// Computes a block ordering of all blocks in this loop.
        /// </summary>
        /// <typeparam name="TOtherOrder">The other order.</typeparam>
        /// <typeparam name="TOtherDirection">The target direction.</typeparam>
        /// <param name="entryIndex">
        /// The index of the entry block to use as starting point.
        /// </param>
        /// <returns>The computed block ordering.</returns>
        public BasicBlockCollection<TOtherOrder, TOtherDirection>
            ComputeOrderedBlocks<TOtherOrder, TOtherDirection>(
            int entryIndex)
            where TOtherOrder : struct, ITraversalOrder<BasicBlock>
            where TOtherDirection : struct, IControlFlowDirection =>
            Entries[entryIndex].TraverseToCollection<
                TOtherOrder,
                MembersSuccessorProvider<TOtherDirection>,
                TOtherDirection>(Count);

        #endregion
    }

    /// <summary>
    /// An enumerator to iterate over all loops.
    /// </summary>
    internal struct Enumerator : IEnumerator<Node>
    {
        private List<Node>.Enumerator enumerator;

        /// <summary>
        /// Constructs a new node enumerator.
        /// </summary>
        /// <param name="nodes">The nodes to iterate over.</param>
        internal Enumerator(List<Node> nodes)
        {
            enumerator = nodes.GetEnumerator();
        }

        /// <summary>
        /// Returns the current node.
        /// </summary>
        public Node Current => enumerator.Current;

        /// <summary cref="IEnumerator.Current"/>
        object IEnumerator.Current => Current;

        /// <summary cref="IDisposable.Dispose"/>
        public void Dispose() => enumerator.Dispose();

        /// <summary cref="IEnumerator.MoveNext"/>
        public bool MoveNext() => enumerator.MoveNext();

        /// <summary cref="IEnumerator.Reset"/>
        void IEnumerator.Reset() => throw new InvalidOperationException();
    }

    /// <summary>
    /// Represents node data that is required for Tarjan's algorithm.
    /// </summary>
    sealed class NodeData
    {
        /// <summary>
        /// Pops a new data element.
        /// </summary>
        /// <param name="stack">The source stack to pop from.</param>
        /// <returns>The popped node data.</returns>
        public static NodeData Pop(List<NodeData> stack)
        {
            Debug.Assert(stack.Count > 0, "Cannot pop stack");
            var result = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            result.OnStack = false;
            return result;
        }

        /// <summary>
        /// Constructs a new data instance.
        /// </summary>
        /// <param name="node">The CFG node.</param>
        public NodeData(in CFG<TOrder, TDirection>.Node node)
        {
            Node = node;
            Clear();
        }

        /// <summary>
        /// Returns the associated node.
        /// </summary>
        public CFG<TOrder, TDirection>.Node Node { get; }

        /// <summary>
        /// The associated loop index.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// The associated loop low link.
        /// </summary>
        public int LowLink { get; set; }

        /// <summary>
        /// Return true if the associated node is on the stack.
        /// </summary>
        public bool OnStack { get; private set; }

        /// <summary>
        /// Returns true if the current block is a header.
        /// </summary>
        public bool IsInSCC { get; set; }

        /// <summary>
        /// Returns true if the current block is a header.
        /// </summary>
        public bool IsHeader { get; set; }

        /// <summary>
        /// Returns true if the index has been initialized.
        /// </summary>
        public bool HasIndex => Index >= 0;

        /// <summary>
        /// Clears all internal links.
        /// </summary>
        public void Clear()
        {
            Index = -1;
            LowLink = -1;
            OnStack = false;
        }

        /// <summary>
        /// Pushes the current node onto the processing stack.
        /// </summary>
        /// <param name="stack">The processing stack.</param>
        /// <param name="index">The current traversal index.</param>
        public void Push(List<NodeData> stack, ref int index)
        {
            Index = index;
            LowLink = index;
            OnStack = true;
            stack.Add(this);

            ++index;
        }
    }

    #endregion

    #region Static

    /// <summary>
    /// Returns true if this is a loop.
    /// </summary>
    private static bool IsLoop(
        List<NodeData> stack,
        ValueMap<Method, BasicBlock, NodeData> nodeMapping,
        NodeData v,
        out int baseIndex)
    {
        // Determine all nodes that belong to the SCC
        int lastIndex = stack.Count - 1;
        baseIndex = lastIndex;
        for (; baseIndex >= 0; --baseIndex)
        {
            var w = stack[baseIndex];
            w.IsInSCC = true;
            if (w == v)
                break;
        }

        // Check for a real loop
        foreach (var predecessor in stack[lastIndex].Node.Predecessors)
        {
            var data = nodeMapping[predecessor];
            if (data.IsInSCC && !data.IsHeader)
                return true;
        }

        // Remove from current SCC
        for (int index = baseIndex; index <= lastIndex; ++index)
        {
            var data = NodeData.Pop(stack);
            data.IsInSCC = false;
        }

        return false;
    }

    /// <summary>
    /// Creates a new loop analysis.
    /// </summary>
    /// <param name="cfg">The underlying source CFG.</param>
    /// <returns>The created loop analysis.</returns>
    public static Loops<TOrder, TDirection> Create(CFG<TOrder, TDirection> cfg) =>
        new Loops<TOrder, TDirection>(cfg);

    #endregion

    #region Instance

    private InlineList<Node> loops;
    private InlineList<Node> headers;
    private readonly ValueMap<Method, BasicBlock, Node> loopMapping;

    /// <summary>
    /// Constructs a new collection of loops.
    /// </summary>
    /// <param name="cfg">The source CFG.</param>
    private Loops(CFG<TOrder, TDirection> cfg)
    {
        loops = InlineList<Node>.Create(4);
        loopMapping = cfg.Blocks.CreateMap<Node>();

        CFG = cfg;

        var mapping = cfg.Blocks.CreateMap((block, _) =>
            new NodeData(CFG[block]));
        int index = 0;
        var stack = new List<NodeData>(cfg.Count);

        // Start with the entry point
        StrongConnect(
            stack,
            null,
            ref mapping,
            mapping[cfg.Root],
            ref index);

        // Continue the search for each nested loop
        for (int loopIndex = 0; loopIndex < loops.Count; ++loopIndex)
        {
            var loop = loops[loopIndex];

            // Skip loop consisting of a single block
            if (loop.AllMembers.Count == 1)
                continue;

            foreach (var member in loop.AllMembers)
                mapping[member].Clear();

            foreach (var header in loops[loopIndex].Headers)
            {
                var headerData = mapping[header];
                StrongConnect(
                    stack,
                    loop,
                    ref mapping,
                    headerData,
                    ref index);
            }
        }

        // Search for all headers
        headers = InlineList<Node>.Create(loops.Count);
        foreach (var loop in loops)
        {
            if (!loop.IsNested)
                headers.Add(loop);
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the underlying CFG.
    /// </summary>
    public CFG<TOrder, TDirection> CFG { get; }

    /// <summary>
    /// Returns all underlying blocks.
    /// </summary>
    public BasicBlockCollection<TOrder, TDirection> Blocks => CFG.Blocks;

    /// <summary>
    /// Returns the number of loops.
    /// </summary>
    public int Count => loops.Count;

    /// <summary>
    /// Returns true if there are no loops in this analysis.
    /// </summary>
    public bool IsEmpty => loops.Count == 0;

    /// <summary>
    /// Returns the i-th loop.
    /// </summary>
    /// <param name="index">The index of the i-th loop.</param>
    /// <returns>The resolved loop.</returns>
    public Node this[int index] => loops[index];

    /// <summary>
    /// Returns all top-level loop nodes (non-nested loops).
    /// </summary>
    public ReadOnlySpan<Node> TopLevelLoops => headers;

    #endregion

    #region Methods

    /// <summary>
    /// The modified heart of Tarjan's SCC algorithm.
    /// </summary>
    /// <param name="stack">The current processing stack.</param>
    /// <param name="parent">The parent loop.</param>
    /// <param name="nodeMapping">The current node mapping.</param>
    /// <param name="v">The current node.</param>
    /// <param name="index">The current index value.</param>
    private void StrongConnect(
        List<NodeData> stack,
        Node? parent,
        ref ValueMap<Method, BasicBlock, NodeData> nodeMapping,
        NodeData v,
        ref int index)
    {
        Debug.Assert(!v.HasIndex);
        v.Push(stack, ref index);
        foreach (var wNode in v.Node.Successors)
        {
            var w = nodeMapping[wNode];
            if (w.IsHeader)
                continue;

            if (!w.HasIndex)
            {
                StrongConnect(
                    stack,
                    parent,
                    ref nodeMapping,
                    w,
                    ref index);
                v.LowLink = XMath.Min(v.LowLink, w.LowLink);
            }
            else if (w.OnStack)
            {
                v.LowLink = XMath.Min(v.LowLink, w.Index);
            }
        }

        // We have found a new SCC root node (Tarjan's SCC algorithm)
        if (v.LowLink == v.Index)
        {
            RegisterLoop(
                stack,
                parent,
                ref nodeMapping,
                v);
        }
    }

    /// <summary>
    /// Registers a new loop entry (if possible).
    /// </summary>
    /// <param name="stack">The current processing stack.</param>
    /// <param name="parent">The parent loop.</param>
    /// <param name="nodeMapping">The current node mapping.</param>
    /// <param name="v">The current node.</param>
    private void RegisterLoop(
        List<NodeData> stack,
        Node? parent,
        ref ValueMap<Method, BasicBlock, NodeData> nodeMapping,
        NodeData v)
    {
        // Check for a real loop
        if (!IsLoop(stack, nodeMapping, v, out int baseIndex))
            return;

        // Gather all nodes contained in this SCC
        var members = CFG.Blocks.Method.CreateSetList<BasicBlock>();

        for (int i = baseIndex, e = stack.Count; i < e; ++i)
        {
            var w = NodeData.Pop(stack);
            members.Add(w.Node);
        }

        // Initialize all lists and sets
        var headers = InlineList<BasicBlock>.Create(2);
        var breakers = InlineList<BasicBlock>.Create(2);
        var entryBlocks = new HashSet<BasicBlock>(new BasicBlock.BlockComparer());
        var exitBlocks = new HashSet<BasicBlock>(new BasicBlock.BlockComparer());

        // Gather all loop entries and exists
        foreach (var member in members)
        {
            foreach (var predecessor in member.GetPredecessors<TDirection>())
            {
                if (nodeMapping[predecessor].IsInSCC)
                    continue;
                entryBlocks.Add(predecessor);

                if (headers.Contains(member, new BasicBlock.BlockComparer()))
                    continue;
                headers.Add(member);
                nodeMapping[member].IsHeader = true;
            }

            foreach (var successor in member.GetSuccessors<TDirection>())
            {
                if (nodeMapping[successor].IsInSCC)
                    continue;
                exitBlocks.Add(successor);

                if (breakers.Contains(member, new BasicBlock.BlockComparer()))
                    continue;
                breakers.Add(member);
            }
        }

        // Note that we do not have to worry about loops without a header block since
        // our IL frontend normalizes all blocks to have a unique header block
        // without any predecessors. Therefore, each loop will have at least one
        // header and at least one entry block.
        v.Node.Assert(headers.Count > 0 && entryBlocks.Count > 0);

        // Compute all back edges
        var backEdges = InlineList<BasicBlock>.Create(2);
        foreach (var member in members)
        {
            foreach (var successor in member.GetSuccessors<TDirection>())
            {
                if (headers.Contains(successor, new BasicBlock.BlockComparer()))
                {
                    backEdges.Add(member);
                    break;
                }
            }
        }
        v.Node.Assert(backEdges.Count > 0);

        var loop = new Node(
            parent,
            ref headers,
            ref breakers,
            ref backEdges,
            members,
            entryBlocks,
            exitBlocks);
        loops.Add(loop);

        // Map members to their associated inner-most loop
        foreach (var member in members)
        {
            nodeMapping[member].IsInSCC = false;
            loopMapping[member] = loop;
        }
    }

    /// <summary>
    /// Tries to resolve the given block to its associated innermost loop.
    /// </summary>
    /// <param name="block">The block to map to a loop.</param>
    /// <param name="loop">The resulting loop.</param>
    /// <returns>True, if the node could be resolved to a loop.</returns>
    public bool TryGetLoop(BasicBlock block, [NotNullWhen(true)] out Node? loop) =>
        loopMapping.TryGetValue(block, out loop);

    /// <summary>
    /// Gets the loop containing the given block, or null if the block is not in any loop.
    /// </summary>
    /// <param name="block">The block to query.</param>
    /// <returns>The innermost loop containing the block, or null.</returns>
    public Node? GetLoop(BasicBlock block) =>
        TryGetLoop(block, out var loop) ? loop : null;

    /// <summary>
    /// Processes all loops starting with the innermost loops.
    /// </summary>
    /// <param name="processor">The loop processor action.</param>
    public void ProcessLoops(Action<Node> processor)
    {
        foreach (var header in TopLevelLoops)
            ProcessLoopsRecursive(header, processor);
    }

    /// <summary>
    /// Processes loops in a recursive way by processing the innermost loops first.
    /// </summary>
    /// <param name="loop">The current loop node.</param>
    /// <param name="processor">The loop processor action.</param>
    private static void ProcessLoopsRecursive(
        Node loop,
        Action<Node> processor)
    {
        foreach (var child in loop.Children)
            ProcessLoopsRecursive(child, processor);

        processor(loop);
    }

    /// <summary>
    /// Enumerates all loops in this analysis.
    /// </summary>
    /// <returns>An enumerable of all loops.</returns>
    public IEnumerable<Node> GetAllLoops()
    {
        for (int i = 0; i < loops.Count; i++)
            yield return loops[i];
    }

    #endregion

    #region IEnumerable

    /// <summary>
    /// Returns an enumerator that iterates over all loops.
    /// </summary>
    /// <returns>The resolved enumerator.</returns>
    public ReadOnlySpan<Node>.Enumerator GetEnumerator() => loops.GetEnumerator();

    #endregion
}

/// <summary>
/// Utility methods for the <see cref="Loops{TOrder, TDirection}"/> analysis.
/// </summary>
static class Loops
{
    /// <summary>
    /// Creates a new loops analysis instance based on the given CFG.
    /// </summary>
    /// <typeparam name="TOrder">The underlying block order.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    /// <param name="cfg">The underlying CFG.</param>
    /// <returns>The created loops analysis.</returns>
    public static Loops<TOrder, TDirection> CreateLoops<TOrder, TDirection>(
        this CFG<TOrder, TDirection> cfg)
        where TOrder : struct, ITraversalOrder<BasicBlock>
        where TDirection : struct, IControlFlowDirection =>
        Loops<TOrder, TDirection>.Create(cfg);

    /// <summary>
    /// Creates a new loops analysis from a method's basic blocks.
    /// </summary>
    /// <param name="blocks">The basic block collection.</param>
    /// <returns>The created loops analysis.</returns>
    public static Loops<ReversePostOrder<BasicBlock>, Forwards> CreateLoops(
        this BasicBlockCollection<ReversePostOrder<BasicBlock>, Forwards> blocks)
    {
        var cfg = blocks.CreateCFG();
        return cfg.CreateLoops();
    }

    /// <summary>
    /// Creates a new loops analysis from a method.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <returns>The created loops analysis.</returns>
    public static Loops<ReversePostOrder<BasicBlock>, Forwards> CreateLoops(
        this Method method) =>
        method.Blocks.CreateLoops();
}
