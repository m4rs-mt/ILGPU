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
    [SuppressMessage(
        "Microsoft.Naming",
        "CA1710: IdentifiersShouldHaveCorrectSuffix",
        Justification = "This is the correct name of this program analysis")]
    public sealed class Loops<TOrder, TDirection>
        where TOrder : struct, ITraversalOrder
        where TDirection : struct, IControlFlowDirection
    {
        #region Nested Types

        /// <summary>
        /// Represents a single strongly-connected component.
        /// </summary>
        [SuppressMessage(
            "Microsoft.Naming",
            "CA1710: IdentifiersShouldHaveCorrectSuffix",
            Justification = "This is a single Loop object; adding a collection suffix " +
            "would be misleading")]
        public sealed class Node : IReadOnlyCollection<BasicBlock>
        {
            #region Nested Types

            /// <summary>
            /// An enumerator to iterate over all nodes in the current loop.
            /// </summary>
            public struct Enumerator : IEnumerator<BasicBlock>
            {
                private HashSet<BasicBlock>.Enumerator enumerator;

                /// <summary>
                /// Constructs a new node enumerator.
                /// </summary>
                /// <param name="nodes">The nodes to iterate over.</param>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal Enumerator(HashSet<BasicBlock> nodes)
                {
                    enumerator = nodes.GetEnumerator();
                }

                /// <summary>
                /// Returns the current node.
                /// </summary>
                public BasicBlock Current => enumerator.Current;

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
            /// A value enumerator to iterate over all values in the current loop.
            /// </summary>
            public struct ValueEnumerator : IEnumerator<Value>
            {
                private Enumerator enumerator;
                private BasicBlock.Enumerator valueEnumerator;

                /// <summary>
                /// Constructs a new value enumerator.
                /// </summary>
                /// <param name="iterator">The loop enumerator.</param>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal ValueEnumerator(Enumerator iterator)
                {
                    enumerator = iterator;
                    // There must be at least a single node
                    enumerator.MoveNext();
                    valueEnumerator = enumerator.Current.GetEnumerator();
                }

                /// <summary>
                /// Returns the current value.
                /// </summary>
                public Value Current => valueEnumerator.Current;

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                /// <summary cref="IDisposable.Dispose"/>
                public void Dispose() => enumerator.Dispose();

                /// <summary cref="IEnumerator.MoveNext"/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext()
                {
                    while (true)
                    {
                        if (valueEnumerator.MoveNext())
                            return true;

                        // Try to move to the next node
                        if (!enumerator.MoveNext())
                            return false;

                        valueEnumerator = enumerator.Current.GetEnumerator();
                    }
                }

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();
            }

            /// <summary>
            /// A value enumerator to iterate over all phi values
            /// that have a dependency on outer and inner values of this loop.
            /// </summary>
            private struct PhiValueEnumerator : IEnumerator<Value>
            {
                private ValueEnumerator enumerator;

                /// <summary>
                /// Constructs a new value enumerator.
                /// </summary>
                /// <param name="parent">The parent loop.</param>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal PhiValueEnumerator(Node parent)
                {
                    Parent = parent;
                    enumerator = parent.GetValueEnumerator();
                }

                /// <summary>
                /// The parent loop.
                /// </summary>
                public Node Parent { get; }

                /// <summary>
                /// Returns the current value.
                /// </summary>
                public Value Current => enumerator.Current;

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                /// <summary cref="IDisposable.Dispose"/>
                public void Dispose() => enumerator.Dispose();

                /// <summary cref="IEnumerator.MoveNext"/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext()
                {
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current is PhiValue phiValue)
                        {
                            foreach (Value operand in phiValue.Nodes)
                            {
                                if (!Parent.Contains(operand.BasicBlock))
                                    return true;
                            }
                        }
                    }
                    return false;
                }

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();
            }

            #endregion

            #region Instance

            private InlineList<BasicBlock> headers;
            private InlineList<BasicBlock> breakers;
            private InlineList<BasicBlock> backEdges;
            private readonly HashSet<BasicBlock> nodes;

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
                HashSet<BasicBlock> members,
                HashSet<BasicBlock> entries,
                HashSet<BasicBlock> exits)
            {
                Parent = parent;
                parent?.AddChild(this);

                headerBlocks.MoveTo(ref headers);
                breakerBlocks.MoveTo(ref breakers);
                backEdgeBlocks.MoveTo(ref backEdges);
                nodes = members;
                children = InlineList<Node>.Create(1);
                Entries = entries.ToImmutableArray();
                Exits = exits.ToImmutableArray();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the number of members.
            /// </summary>
            public int Count => nodes.Count;

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

            #endregion

            #region Methods

            /// <summary>
            /// Checks whether the given block belongs to this loop.
            /// </summary>
            /// <param name="block">The block to map to an loop.</param>
            /// <returns>True, if the node belongs to this loop.</returns>
            public bool Contains(BasicBlock block) =>
                nodes.Contains(block);

            /// <summary>
            /// Resolves all <see cref="PhiValue"/>s that are contained
            /// in this loop which reference at least one operand that is not
            /// defined in this loop.
            /// </summary>
            /// <returns>The list of resolved phi values.</returns>
            public Phis ResolvePhis() => Phis.Create(new PhiValueEnumerator(this));

            /// <summary>
            /// Adds the given child node.
            /// </summary>
            /// <param name="child">The child node to add.</param>
            private void AddChild(Node child)
            {
                Debug.Assert(child.Parent == this, "Invalid child");
                children.Add(child);
            }

            #endregion

            #region IEnumerable

            /// <summary>
            /// Returns a new value enumerator.
            /// </summary>
            /// <returns>The resolved value enumerator.</returns>
            public ValueEnumerator GetValueEnumerator() =>
                new ValueEnumerator(GetEnumerator());

            /// <summary>
            /// Returns an enumerator that iterates over all members
            /// of this loop.
            /// </summary>
            /// <returns>The resolved enumerator.</returns>
            public Enumerator GetEnumerator() => new Enumerator(nodes);

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            IEnumerator<BasicBlock> IEnumerable<BasicBlock>.GetEnumerator() =>
                GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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

                foreach (var member in loop)
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
            var memberList = InlineList<BasicBlock>.Create(stack.Count - baseIndex);
            var memberSet = new HashSet<BasicBlock>();

            for (int i = baseIndex, e = stack.Count; i < e; ++i)
            {
                var w = NodeData.Pop(stack);
                memberList.Add(w.Node);
                memberSet.Add(w.Node);
            }

            // Initialize all lists and sets
            var headers = InlineList<BasicBlock>.Create(2);
            var breakers = InlineList<BasicBlock>.Create(2);
            var entryBlocks = new HashSet<BasicBlock>();
            var exitBlocks = new HashSet<BasicBlock>();

            // Gather all loop entries and exists
            foreach (var member in memberList)
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
            foreach (var member in memberList)
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
                memberSet,
                entryBlocks,
                exitBlocks);
            loops.Add(loop);

            // Map members to their associated inner-most loop
            foreach (var member in memberList)
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
