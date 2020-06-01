// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: SCCs.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// An analysis to detect strongly-connected components.
    /// </summary>
    [SuppressMessage(
        "Microsoft.Naming",
        "CA1710: IdentifiersShouldHaveCorrectSuffix",
        Justification = "This is the correct name of this program analysis")]
    public sealed class SCCs<TOrder, TDirection> :
        IReadOnlyList<SCCs<TOrder, TDirection>.SCC>
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
            Justification = "This is a single SCC object; adding a collection suffix " +
            "would be misleading")]
        public readonly struct SCC : IReadOnlyList<BasicBlock>, IEquatable<SCC>
        {
            #region Nested Types

            /// <summary>
            /// An enumerator to iterate over all nodes in the current SCC.
            /// </summary>
            public struct Enumerator : IEnumerator<BasicBlock>
            {
                private List<BasicBlock>.Enumerator enumerator;

                /// <summary>
                /// Constructs a new node enumerator.
                /// </summary>
                /// <param name="nodes">The nodes to iterate over.</param>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal Enumerator(List<BasicBlock> nodes)
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
            /// A value enumerator to iterate over all values in the current SCC.
            /// </summary>
            public struct ValueEnumerator : IEnumerator<Value>
            {
                private Enumerator enumerator;
                private BasicBlock.Enumerator valueEnumerator;

                /// <summary>
                /// Constructs a new value enumerator.
                /// </summary>
                /// <param name="iterator">The SCC enumerator.</param>
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
            /// that have a dependency on outer and inner values of this SCC.
            /// </summary>
            private struct PhiValueEnumerator : IEnumerator<Value>
            {
                private ValueEnumerator enumerator;

                /// <summary>
                /// Constructs a new value enumerator.
                /// </summary>
                /// <param name="parent">The parent SCC.</param>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal PhiValueEnumerator(SCC parent)
                {
                    Parent = parent;
                    enumerator = parent.GetValueEnumerator();
                }

                /// <summary>
                /// The parent SCCs.
                /// </summary>
                public SCC Parent { get; }

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

            private readonly List<BasicBlock> nodes;

            /// <summary>
            /// Constructs a new SCC.
            /// </summary>
            /// <param name="parent">The parent SCC.</param>
            /// <param name="index">The SCC index.</param>
            /// <param name="sccMembers">All SCC members.</param>
            internal SCC(
                SCCs<TOrder, TDirection> parent,
                int index,
                List<BasicBlock> sccMembers)
            {
                Debug.Assert(index >= 0, "Invalid SCC index");
                Debug.Assert(sccMembers != null, "Invalid SCC members");

                Parent = parent;
                Index = index;
                nodes = sccMembers;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent SCCs.
            /// </summary>
            public SCCs<TOrder, TDirection> Parent { get; }

            /// <summary>
            /// Returns the SCC index.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// Returns the number of members.
            /// </summary>
            public int Count => nodes.Count;

            /// <summary>
            /// Returns the i-th SCC member.
            /// </summary>
            /// <param name="index">The index of the i-th SCC member.</param>
            /// <returns>The resolved SCC member.</returns>
            public BasicBlock this[int index] => nodes[index];

            #endregion

            #region Methods

            /// <summary>
            /// Checks whether the given block belongs to this SCC.
            /// </summary>
            /// <param name="block">The block to map to an SCC.</param>
            /// <returns>True, if the node belongs to this SCC.</returns>
            public bool Contains(BasicBlock block) =>
                Parent.TryGetSCC(block, out SCC scc) && scc == this;

            /// <summary>
            /// Resolves all <see cref="PhiValue"/>s that are contained
            /// in this SCC which reference at least one operand that is not
            /// defined in this SCC.
            /// </summary>
            /// <returns>The list of resolved phi values.</returns>
            public Phis ResolvePhis() => Phis.Create(new PhiValueEnumerator(this));

            /// <summary>
            /// Resolves all blocks that can leave this SCC.
            /// </summary>
            /// <returns>An array of all blocks that can leave this SCC.</returns>
            public ImmutableArray<BasicBlock> ResolveBreakingBlocks()
            {
                var result = ImmutableArray.CreateBuilder<BasicBlock>(nodes.Count);
                var visited = new HashSet<BasicBlock>();
                foreach (var node in nodes)
                {
                    if (node.GetSuccessors<TDirection>().Length < 2)
                        continue;

                    // Check for exit targets
                    foreach (var succ in node.Successors)
                    {
                        if (!Contains(succ) && !visited.Add(succ))
                            result.Add(succ);
                    }
                }
                return result.ToImmutable();
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
            /// of this SCC.
            /// </summary>
            /// <returns>The resolved enumerator.</returns>
            public Enumerator GetEnumerator() => new Enumerator(nodes);

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            IEnumerator<BasicBlock> IEnumerable<BasicBlock>.GetEnumerator() =>
                GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion

            #region IEquatable

            /// <summary>
            /// Returns true if the other SCC refers to the same SCC.
            /// </summary>
            /// <param name="other">The other SCC.</param>
            /// <returns>True, if the other SCC refers to the same SCC.</returns>
            public bool Equals(SCC other) =>
                other.Parent == Parent && other.Index == Index;

            #endregion

            #region Object

            /// <summary>
            /// Returns true if the other object refers to the same SCC.
            /// </summary>
            /// <param name="obj">The other object.</param>
            /// <returns>True, if the other object refers to the same SCC.</returns>
            public override bool Equals(object obj) =>
                obj is SCC scc && Equals(scc);

            /// <summary>
            /// Returns the hash code of this SCC.
            /// </summary>
            /// <returns>The hash code of this SCC.</returns>
            public override int GetHashCode() => Index;

            /// <summary>
            /// Returns the light-weight string representation of this SCC.
            /// </summary>
            /// <returns>The light-weight string representation of this SCC.</returns>
            public override string ToString() => Index.ToString();

            #endregion

            #region Operators

            /// <summary>
            /// Returns true if the first and the second SCCs refer to the same SCC.
            /// </summary>
            /// <param name="first">The first SCC.</param>
            /// <param name="second">The second SCC.</param>
            /// <returns>True, if both SCCs refer to the same SCC.</returns>
            public static bool operator ==(SCC first, SCC second) =>
                first.Equals(second);

            /// <summary>
            /// Returns true if the first and the second SCCs do not refer to the same
            /// SCC.
            /// </summary>
            /// <param name="first">The first SCC.</param>
            /// <param name="second">The second SCC.</param>
            /// <returns>True, if both SCCs do not refer to the same SCC.</returns>
            public static bool operator !=(SCC first, SCC second) =>
                !(first == second);

            #endregion
        }

        /// <summary>
        /// An enumerator to iterate over all SCCs.
        /// </summary>
        public struct Enumerator : IEnumerator<SCC>
        {
            private List<SCC>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new node enumerator.
            /// </summary>
            /// <param name="nodes">The nodes to iterate over.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(List<SCC> nodes)
            {
                enumerator = nodes.GetEnumerator();
            }

            /// <summary>
            /// Returns the current node.
            /// </summary>
            public SCC Current => enumerator.Current;

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
            public static NodeData Pop(Stack<NodeData> stack)
            {
                Debug.Assert(stack.Count > 0, "Cannot pop stack");
                var result = stack.Pop();
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
                Index = -1;
                LowLink = -1;
                OnStack = false;
            }

            /// <summary>
            /// Returns the associated node.
            /// </summary>
            public CFG<TOrder, TDirection>.Node Node { get; }

            /// <summary>
            /// The associated SCC index.
            /// </summary>
            public int Index { get; set; }

            /// <summary>
            /// The associated SCC low link.
            /// </summary>
            public int LowLink { get; set; }

            /// <summary>
            /// Return true if the associated node is on the stack.
            /// </summary>
            public bool OnStack { get; private set; }

            /// <summary>
            /// Returns true if the index has been initialized.
            /// </summary>
            public bool HasIndex => Index >= 0;

            /// <summary>
            /// Pushes the current node onto the processing stack.
            /// </summary>
            /// <param name="stack">The processing stack.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Push(Stack<NodeData> stack)
            {
                OnStack = true;
                stack.Push(this);
            }
        }

        /// <summary>
        /// Provides new intermediate <see cref="NodeData"/> instances.
        /// </summary>
        private readonly struct NodeDataProvider :
            IBasicBlockMapValueProvider<NodeData>
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
            public readonly NodeData GetValue(
                BasicBlock block,
                int traversalIndex) =>
                new NodeData(CFG[block]);
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new SCC analysis.
        /// </summary>
        /// <param name="cfg">The underlying source CFG.</param>
        /// <returns>The created SCC analysis.</returns>
        public static SCCs<TOrder, TDirection> Create(CFG<TOrder, TDirection> cfg) =>
            new SCCs<TOrder, TDirection>(cfg);

        #endregion

        #region Instance

        private readonly List<SCC> sccs = new List<SCC>();
        private readonly BasicBlockMap<SCC> sccMapping;

        /// <summary>
        /// Constructs a new collection of SCCs.
        /// </summary>
        /// <param name="cfg">The source CFG.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SCCs(CFG<TOrder, TDirection> cfg)
        {
            CFG = cfg;
            var mapping = cfg.Blocks.CreateMap(new NodeDataProvider(cfg));
            int index = 0;
            var stack = new Stack<NodeData>();

            foreach (var node in cfg)
            {
                var nodeData = mapping[node];
                if (!nodeData.HasIndex)
                {
                    StrongConnect(
                        stack,
                        ref mapping,
                        nodeData,
                        ref index);
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying CFG.
        /// </summary>
        public CFG<TOrder, TDirection> CFG { get; }

        /// <summary>
        /// Returns the number of SCCs.
        /// </summary>
        public int Count => sccs.Count;

        /// <summary>
        /// Returns the i-th SCC.
        /// </summary>
        /// <param name="index">The index of the i-th SCC.</param>
        /// <returns>The resolved SCC.</returns>
        public SCC this[int index] => sccs[index];

        #endregion

        #region Methods

        /// <summary>
        /// The heart of Tarjan's SCC algorithm.
        /// </summary>
        /// <param name="stack">The current processing stack.</param>
        /// <param name="nodeMapping">The current node mapping.</param>
        /// <param name="v">The current node.</param>
        /// <param name="index">The current index value.</param>
        private void StrongConnect(
            Stack<NodeData> stack,
            ref BasicBlockMap<NodeData> nodeMapping,
            NodeData v,
            ref int index)
        {
            Debug.Assert(!v.HasIndex, "Index already defined");
            v.Index = index;
            v.LowLink = index;
            ++index;

            v.Push(stack);
            foreach (var wNode in v.Node.Successors)
            {
                var w = nodeMapping[wNode];
                if (!w.HasIndex)
                {
                    StrongConnect(
                        stack,
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

            if (v.LowLink == v.Index)
            {
                // Optimize for the trivial case
                var w = NodeData.Pop(stack);
                if (w != v)
                {
                    var members = new List<BasicBlock>(stack.Count + 1);
                    for (; ; w = NodeData.Pop(stack))
                    {
                        members.Add(w.Node);
                        if (w == v)
                            break;
                    }

                    var scc = new SCC(this, sccs.Count, members);
                    sccs.Add(scc);

                    foreach (var member in members)
                        sccMapping.Add(member, scc);
                }
            }
        }

        /// <summary>
        /// Tries to resolve the given block to an associated SCC.
        /// </summary>
        /// <param name="block">The block to map to an SCC.</param>
        /// <param name="scc">The resulting SCC.</param>
        /// <returns>True, if the node could be resolved to an SCC.</returns>
        public bool TryGetSCC(BasicBlock block, out SCC scc) =>
            sccMapping.TryGetValue(block, out scc);

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that iterates over all SCCs.
        /// </summary>
        /// <returns>The resolved enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(sccs);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<SCC> IEnumerable<SCC>.GetEnumerator() => GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
