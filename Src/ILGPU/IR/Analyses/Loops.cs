// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Loops.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

//
// Based on an adapted version of the paper: Identifying Loops In Almost Linear Time
//

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// An analysis to detect strongly-connected components.
    /// </summary>
    /// <typeparam name="TOrder">The current order.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public sealed class Loops<TOrder, TDirection>
        where TOrder : struct, ITraversalOrder
        where TDirection : struct, IControlFlowDirection
    {
        #region Nested Types

        /// <summary>
        /// Represents an abstract loop processor.
        /// </summary>
        public interface ILoopProcessor
        {
            /// <summary>
            /// Processes the given loop.
            /// </summary>
            /// <param name="node">The current loop node.</param>
            void Process(Node node);
        }

        /// <summary>
        /// A specialized successor provider for loop members that exclude all exit
        /// blocks of an associated loop.
        /// </summary>
        /// <typeparam name="TOtherDirection">The target direction.</typeparam>
        public readonly struct MembersSuccessorProvider<TOtherDirection> :
            ITraversalSuccessorsProvider<TOtherDirection>
            where TOtherDirection : struct, IControlFlowDirection
        {
            #region Instance

            /// <summary>
            /// Constructs a new successor provider.
            /// </summary>
            /// <param name="node">The loop node.</param>
            public MembersSuccessorProvider(Node node)
            {
                Node = node;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated loop node.
            /// </summary>
            public Node Node { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Returns the successors of the given basic block that do not contain any
            /// of the associated loop exit blocks.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ReadOnlySpan<BasicBlock> GetSuccessors(
                BasicBlock basicBlock)
            {
                var successors = basicBlock.CurrentSuccessors;

                // Check for an entry block
                if (Node.Entries.Contains(basicBlock))
                    return AdjustEntrySuccessors(successors);

                // Check for exit blocks
                foreach (var exit in Node.Exits)
                {
                    if (successors.Contains(exit, new BasicBlock.Comparer()))
                        return AdjustExitSuccessors(successors);
                }

                // Use the original successors
                return successors;
            }

            /// <summary>
            /// Helper function to adjust the header span of the current successors.
            /// </summary>
            /// <param name="currentSuccessors">The current successors.</param>
            /// <returns>The adjusted span that contains header blocks only.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private readonly ReadOnlySpan<BasicBlock> AdjustEntrySuccessors(
                ReadOnlySpan<BasicBlock> currentSuccessors)
            {
                var successors = InlineList<BasicBlock>.Create(currentSuccessors.Length);
                foreach (var successor in currentSuccessors)
                {
                    if (Node.Headers.Contains(successor, new BasicBlock.Comparer()))
                        successors.Add(successor);
                }
                return successors;
            }

            /// <summary>
            /// Helper function to adjust the exit span of the current successors.
            /// </summary>
            /// <param name="currentSuccessors">The current successors.</param>
            /// <returns>The adjusted span without any exit block.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private readonly ReadOnlySpan<BasicBlock> AdjustExitSuccessors(
                ReadOnlySpan<BasicBlock> currentSuccessors)
            {
                var successors = currentSuccessors.ToInlineList();
                foreach (var exit in Node.Exits)
                    successors.RemoveAll(exit, new BasicBlock.Comparer());
                return successors;
            }

            #endregion
        }

        /// <summary>
        /// Represents a single strongly-connected component.
        /// </summary>
        [SuppressMessage(
            "Microsoft.Naming",
            "CA1710: IdentifiersShouldHaveCorrectSuffix",
            Justification = "This is a single Loop object; adding a collection suffix " +
            "would be misleading")]
        public sealed class Node
        {
            #region Instance

            private InlineList<BasicBlock> headers;
            private InlineList<BasicBlock> breakers;
            private InlineList<BasicBlock> backEdges;

            /// <summary>
            /// All child nodes (if any).
            /// </summary>
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
                Node parent,
                ref InlineList<BasicBlock> headerBlocks,
                ref InlineList<BasicBlock> breakerBlocks,
                ref InlineList<BasicBlock> backEdgeBlocks,
                in BasicBlockSetList members,
                HashSet<BasicBlock> entries,
                HashSet<BasicBlock> exits)
            {
                Parent = parent;
                parent?.AddChild(this);

                headerBlocks.MoveTo(ref headers);
                breakerBlocks.MoveTo(ref breakers);
                backEdgeBlocks.MoveTo(ref backEdges);
                AllMembers = members;
                children = InlineList<Node>.Create(1);
                Entries = entries.ToImmutableArray();
                Exits = exits.ToImmutableArray();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the number of members.
            /// </summary>
            public int Count => AllMembers.Count;

            /// <summary>
            /// Returns the block containing the associated back edge.
            /// </summary>
            public ReadOnlySpan<BasicBlock> BackEdges => backEdges;

            /// <summary>
            /// Returns all loop headers.
            /// </summary>
            public ReadOnlySpan<BasicBlock> Headers => headers;

            /// <summary>
            /// Returns all loop breakers that contain branches to exit the loop.
            /// </summary>
            public ReadOnlySpan<BasicBlock> Breakers => breakers;

            /// <summary>
            /// All entry blocks that jump into the loop.
            /// </summary>
            public ImmutableArray<BasicBlock> Entries { get; }

            /// <summary>
            /// All exit blocks that are reachable by all breakers from the loop.
            /// </summary>
            public ImmutableArray<BasicBlock> Exits { get; }

            /// <summary>
            /// Returns the parent loop.
            /// </summary>
            public Node Parent { get; }

            /// <summary>
            /// Returns all child loops.
            /// </summary>
            public ReadOnlySpan<Node> Children => children;

            /// <summary>
            /// Returns true if this is a nested loop
            /// </summary>
            public bool IsNestedLoop => Parent != null;

            /// <summary>
            /// Returns true if this is an innermost loop.
            /// </summary>
            public bool IsInnermostLoop => children.Count < 1;

            /// <summary>
            /// Returns the set list of all members.
            /// </summary>
            public BasicBlockSetList AllMembers { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Checks whether the given block belongs to this loop.
            /// </summary>
            /// <param name="block">The block to map to an loop.</param>
            /// <returns>True, if the node belongs to this loop.</returns>
            public bool Contains(BasicBlock block) =>
                block != null && AllMembers.Contains(block);

            /// <summary>
            /// Checks whether the given block belongs to this loop and not to a
            /// (potentially) nested child loop.
            /// </summary>
            /// <param name="block">The block to map to an loop.</param>
            /// <returns>
            /// True, if the node belongs to this loop and not a potentially nested child
            /// node.
            /// </returns>
            public bool ContainsExclusively(BasicBlock block)
            {
                // The given block can be null when querying the block of a parameter
                if (!Contains(block))
                    return false;

                // Exclude nested blocks of nested loops
                if (IsInnermostLoop)
                    return true;
                foreach (var child in Children)
                {
                    if (child.Contains(block))
                        return false;
                }
                return true;
            }

            /// <summary>
            /// Resolves all <see cref="PhiValue"/>s that are contained in this loop and
            /// in no other potentially nested child loop.
            /// </summary>
            /// <returns>The list of resolved phi values.</returns>
            public Phis ComputePhis()
            {
                var builder = Phis.CreateBuilder(AllMembers[0].Method);
                foreach (var block in AllMembers)
                {
                    if (ContainsExclusively(block))
                        builder.Add(block);
                }
                return builder.Seal();
            }

            /// <summary>
            /// Returns true if the given blocks contain at least one backedge block.
            /// </summary>
            /// <param name="blocks">The blocks to test.</param>
            /// <returns>
            /// True, if the given block contain at least one backedge block.
            /// </returns>
            public bool ContainsBackedgeBlock(ReadOnlySpan<BasicBlock> blocks)
            {
                foreach (var block in blocks)
                {
                    if (BackEdges.Contains(block, new BasicBlock.Comparer()))
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Returns true if the given blocks consists of exclusive body blocks only.
            /// </summary>
            /// <param name="blocks">The blocks to test.</param>
            /// <returns>
            /// True, if the given blocks consists of exclusive body blocks only.
            /// </returns>
            public bool ConsistsOfBodyBlocks(ReadOnlySpan<BasicBlock> blocks)
            {
                foreach (var block in blocks)
                {
                    if (!ContainsExclusively(block))
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
            /// <returns>The computed block ordering.</returns>
            public BasicBlockCollection<TOrder, TDirection> ComputeOrderedBlocks(
                int entryIndex) =>
                ComputeOrderedBlocks<TOrder, TDirection>(entryIndex);

            /// <summary>
            /// Computes a block ordering of all blocks in this loop.
            /// </summary>
            /// <typeparam name="TOtherOrder">The other order.</typeparam>
            /// <typeparam name="TOtherDirection">The target direction.</typeparam>
            /// <returns>The computed block ordering.</returns>
            public BasicBlockCollection<TOtherOrder, TOtherDirection>
                ComputeOrderedBlocks<TOtherOrder, TOtherDirection>(
                int entryIndex)
                where TOtherOrder : struct, ITraversalOrder
                where TOtherDirection : struct, IControlFlowDirection =>
                new TOtherOrder().TraverseToCollection<
                    TOtherOrder,
                    MembersSuccessorProvider<TOtherDirection>,
                    TOtherDirection>(
                    Count,
                    Entries[entryIndex],
                    new MembersSuccessorProvider<TOtherDirection>(this));

            #endregion
        }

        /// <summary>
        /// An enumerator to iterate over all loops.
        /// </summary>
        public struct Enumerator : IEnumerator<Node>
        {
            private List<Node>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new node enumerator.
            /// </summary>
            /// <param name="nodes">The nodes to iterate over.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => enumerator.MoveNext();

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        /// <summary>
        /// Represents node data that is required for Tarjan's algorithm.
        /// </summary>
        private sealed class NodeData
        {
            /// <summary>
            /// Pops a new data element.
            /// </summary>
            /// <param name="stack">The source stack to pop from.</param>
            /// <returns>The popped node data.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Push(List<NodeData> stack, ref int index)
            {
                Index = index;
                LowLink = index;
                OnStack = true;
                stack.Add(this);

                ++index;
            }
        }

        /// <summary>
        /// Provides new intermediate <see cref="NodeData"/> list instances.
        /// </summary>
        private readonly struct NodeListProvider : IBasicBlockMapValueProvider<List<Node>>
        {
            /// <summary>
            /// Creates a new <see cref="NodeData"/> instance.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly List<Node> GetValue(BasicBlock block, int _) =>
                new List<Node>(2);
        }

        /// <summary>
        /// Provides new intermediate <see cref="NodeData"/> instances.
        /// </summary>
        private readonly struct NodeDataProvider : IBasicBlockMapValueProvider<NodeData>
        {
            /// <summary>
            /// Constructs a new data provider.
            /// </summary>
            /// <param name="cfg">The underlying CFG.</param>
            public NodeDataProvider(CFG<TOrder, TDirection> cfg)
            {
                CFG = cfg;
            }

            /// <summary>
            /// Returns the underlying CFG.
            /// </summary>
            public CFG<TOrder, TDirection> CFG { get; }

            /// <summary>
            /// Creates a new <see cref="NodeData"/> instance.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly NodeData GetValue(BasicBlock block, int traversalIndex) =>
                new NodeData(CFG[block]);
        }

        #endregion

        #region Static

        /// <summary>
        /// Returns true if this is a loop.
        /// </summary>
        private static bool IsLoop(
            List<NodeData> stack,
            BasicBlockMap<NodeData> nodeMapping,
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
        private BasicBlockMap<Node> loopMapping;

        /// <summary>
        /// Constructs a new collection of loops.
        /// </summary>
        /// <param name="cfg">The source CFG.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Loops(CFG<TOrder, TDirection> cfg)
        {
            loops = InlineList<Node>.Create(4);
            loopMapping = cfg.Blocks.CreateMap<Node>();

            CFG = cfg;

            var mapping = cfg.Blocks.CreateMap(new NodeDataProvider(cfg));
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

                foreach (var member in loop.AllMembers)
                    mapping[member].Clear();

                foreach (var header in loops[loopIndex].Headers)
                {
                    StrongConnect(
                        stack,
                        loop,
                        ref mapping,
                        mapping[header],
                        ref index);
                }
            }

            // Search for all headers
            headers = InlineList<Node>.Create(loops.Count);
            foreach (var loop in loops)
            {
                if (!loop.IsNestedLoop)
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
        /// Returns the i-th loop.
        /// </summary>
        /// <param name="index">The index of the i-th loop.</param>
        /// <returns>The resolved loop.</returns>
        public Node this[int index] => loops[index];

        /// <summary>
        /// Returns all loop headers.
        /// </summary>
        public ReadOnlySpan<Node> Headers => headers;

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
            Node parent,
            ref BasicBlockMap<NodeData> nodeMapping,
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
                    v.LowLink = IntrinsicMath.Min(v.LowLink, w.LowLink);
                }
                else if (w.OnStack)
                {
                    v.LowLink = IntrinsicMath.Min(v.LowLink, w.Index);
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
            Node parent,
            ref BasicBlockMap<NodeData> nodeMapping,
            NodeData v)
        {
            // Check for a real loop
            if (!IsLoop(stack, nodeMapping, v, out int baseIndex))
                return;

            // Gather all nodes contained in this SCC
            var members = BasicBlockSetList.Create(CFG.Blocks);

            for (int i = baseIndex, e = stack.Count; i < e; ++i)
            {
                var w = NodeData.Pop(stack);
                members.Add(w.Node);
            }

            // Initialize all lists and sets
            var headers = InlineList<BasicBlock>.Create(2);
            var breakers = InlineList<BasicBlock>.Create(2);
            var entryBlocks = new HashSet<BasicBlock>();
            var exitBlocks = new HashSet<BasicBlock>();

            // Gather all loop entries and exists
            foreach (var member in members)
            {
                foreach (var predecessor in member.GetPredecessors<TDirection>())
                {
                    if (nodeMapping[predecessor].IsInSCC)
                        continue;
                    entryBlocks.Add(predecessor);

                    if (headers.Contains(member, new BasicBlock.Comparer()))
                        continue;
                    headers.Add(member);
                    nodeMapping[member].IsHeader = true;
                }

                foreach (var successor in member.GetSuccessors<TDirection>())
                {
                    if (nodeMapping[successor].IsInSCC)
                        continue;
                    exitBlocks.Add(successor);

                    if (breakers.Contains(member, new BasicBlock.Comparer()))
                        continue;
                    breakers.Add(member);
                }
            }
            v.Node.Assert(headers.Count > 0);

            // Compute all back edges
            var backEdges = InlineList<BasicBlock>.Create(2);
            foreach (var member in members)
            {
                foreach (var successor in member.GetSuccessors<TDirection>())
                {
                    if (headers.Contains(successor, new BasicBlock.Comparer()))
                    {
                        backEdges.Add(member);
                        break;
                    }
                }
            }
            v.Node.Assert(backEdges.Count > 0);

            // Create a new loop
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
        /// Tries to resolve the given block to an associated innermost loop.
        /// </summary>
        /// <param name="block">The block to map to a loop.</param>
        /// <param name="loop">The resulting loop.</param>
        /// <returns>True, if the node could be resolved to a loop.</returns>
        public bool TryGetLoops(BasicBlock block, out Node loop) =>
            loopMapping.TryGetValue(block, out loop);

        /// <summary>
        /// Processes all loops starting with the innermost loops.
        /// </summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <param name="processor">The processor instance.</param>
        /// <returns>The resulting processor instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TProcessor ProcessLoops<TProcessor>(TProcessor processor)
            where TProcessor : struct, ILoopProcessor
        {
            foreach (var header in Headers)
                ProcessLoopsRecursive(header, ref processor);
            return processor;
        }

        /// <summary>
        /// Unrolls loops in a recursive way by unrolling the innermost loops first.
        /// </summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <param name="loop">The current loop node.</param>
        /// <param name="processor">The processor instance.</param>
        private static void ProcessLoopsRecursive<TProcessor>(
            Node loop,
            ref TProcessor processor)
            where TProcessor : struct, ILoopProcessor
        {
            foreach (var child in loop.Children)
                ProcessLoopsRecursive(child, ref processor);

            processor.Process(loop);
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
    public static class Loops
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
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection =>
            Loops<TOrder, TDirection>.Create(cfg);
    }
}
