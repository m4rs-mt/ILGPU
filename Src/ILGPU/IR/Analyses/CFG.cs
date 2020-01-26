// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: CFG.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// Represents a control flow graph.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix",
        Justification = "CFG stands for Control Flow Graph and not CFGCollection or something else")]
    public sealed class CFG : IReadOnlyCollection<CFG.Node>
    {
        #region Nested Types

        /// <summary>
        /// A provider for node mapping values.
        /// </summary>
        /// <typeparam name="T">The mapping element type.</typeparam>
        public interface INodeMappingValueProvider<T>
        {
            /// <summary>
            /// Resolves a mapping value for the given node.
            /// </summary>
            /// <param name="node">The graph node.</param>
            /// <returns>The resolved value.</returns>
            T GetValue(Node node);
        }

        /// <summary>
        /// Represents a mapping thats maps CFG nodes to values.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        public readonly struct NodeMapping<T>
        {
            #region Static

            /// <summary>
            /// Creates a new node mapping
            /// </summary>
            /// <typeparam name="TProvider">The value provider type.</typeparam>
            /// <param name="cfg">The source graph.</param>
            /// <param name="provider">The value provider.</param>
            /// <returns>The resolved node mapping.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static NodeMapping<T> Create<TProvider>(
                CFG cfg,
                in TProvider provider)
                where TProvider : INodeMappingValueProvider<T>
            {
                var mapping = new NodeMapping<T>(cfg);
                foreach (var node in cfg)
                    mapping[node] = provider.GetValue(node);
                return mapping;
            }

            #endregion

            #region Instance

            private readonly T[] mapping;

            /// <summary>
            /// Constructs a new node mapping.
            /// </summary>
            /// <param name="cfg">The parent cfg.</param>
            private NodeMapping(CFG cfg)
            {
                CFG = cfg ?? throw new ArgumentNullException(nameof(cfg));
                mapping = new T[cfg.Count];
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent graph.
            /// </summary>
            public CFG CFG { get; }

            /// <summary>
            /// Returns the associated scope.
            /// </summary>
            public Scope Scope => CFG.Scope;

            /// <summary>
            /// Resolves the associated value.
            /// </summary>
            /// <param name="node">The node.</param>
            /// <returns>The associated value.</returns>
            public T this[Node node]
            {
                get => mapping[node.NodeIndex];
                private set => mapping[node.NodeIndex] = value;
            }

            #endregion
        }

        /// <summary>
        /// Represents an abstract interface for all nodes.
        /// </summary>
        public interface INode
        {
            /// <summary>
            /// Returns the associated function value.
            /// </summary>
            BasicBlock Block { get; }

            /// <summary>
            /// Returns the zero-based node index that can be used
            /// for fast lookups using arrays.
            /// </summary>
            int NodeIndex { get; }
        }

        /// <summary>
        /// Represents a single node in the scope of a control flow graph.
        /// </summary>
        public sealed class Node : INode
        {
            #region Nested Types

            /// <summary>
            /// Represents a node collection of attached nodes.
            /// </summary>
            public readonly struct NodeCollection : IReadOnlyList<Node>
            {
                #region Instance

                internal NodeCollection(List<Node> nodes)
                {
                    Nodes = nodes;
                }


                #endregion

                #region Properties

                /// <summary>
                /// Returns the associated node set.
                /// </summary>
                private List<Node> Nodes { get; }

                /// <summary cref="IReadOnlyCollection{T}.Count"/>
                public int Count => Nodes.Count;

                /// <summary>
                /// Returns the i-th node.
                /// </summary>
                /// <param name="index">The relative node index.</param>
                /// <returns>The resolved node.</returns>
                public Node this[int index] => Nodes[index];

                #endregion

                #region Methods

                /// <summary>
                /// Returns a node enumerator to iterate over all attached nodes.
                /// </summary>
                /// <returns>The resulting node enumerator.</returns>
                public Enumerator GetEnumerator() => new Enumerator(Nodes);

                #endregion

                #region IEnumerable

                /// <summary cref="IEnumerable{T}.GetEnumerator"/>
                IEnumerator<Node> IEnumerable<Node>.GetEnumerator() => GetEnumerator();

                /// <summary cref="IEnumerable.GetEnumerator"/>
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

                #endregion
            }

            /// <summary>
            /// Represents a child enumerator.
            /// </summary>
            public struct Enumerator : IEnumerator<Node>
            {
                #region Instance

                private List<Node>.Enumerator enumerator;

                /// <summary>
                /// Constructs a new child enumerator.
                /// </summary>
                /// <param name="valueSet">The nodes to iterate over.</param>
                internal Enumerator(List<Node> valueSet)
                {
                    enumerator = valueSet.GetEnumerator();
                }

                #endregion

                #region Properties

                /// <summary>
                /// Returns the current node.
                /// </summary>
                public Node Current => enumerator.Current;

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                #endregion

                #region Methods

                /// <summary cref="IDisposable.Dispose"/>
                public void Dispose()
                {
                    enumerator.Dispose();
                }

                /// <summary cref="IEnumerator.MoveNext"/>
                public bool MoveNext()
                {
                    return enumerator.MoveNext();
                }

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();

                #endregion
            }

            #endregion

            #region Instance

            private readonly List<Node> successors = new List<Node>();
            private readonly List<Node> predecessors = new List<Node>();

            /// <summary>
            /// Constructs a new node.
            /// </summary>
            /// <param name="parent">The parent graph.</param>
            /// <param name="block">The associated block.</param>
            /// <param name="nodeIndex">The unique node index.</param>
            internal Node(CFG parent, BasicBlock block, int nodeIndex)
            {
                Parent = parent;
                Block = block;
                NodeIndex = nodeIndex;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent cfg.
            /// </summary>
            public CFG Parent { get; }

            /// <summary>
            /// Returns the associated block.
            /// </summary>
            public BasicBlock Block { get; }

            /// <summary>
            /// Returns the zero-based node index that can be used
            /// for fast lookups using arrays.
            /// </summary>
            public int NodeIndex { get; }

            /// <summary>
            /// Returns the predecessors of this node.
            /// </summary>
            public NodeCollection Predecessors => new NodeCollection(predecessors);

            /// <summary>
            /// Returns the successors of this node.
            /// </summary>
            public NodeCollection Successors => new NodeCollection(successors);

            /// <summary>
            /// Returns the number of predecessors.
            /// </summary>
            public int NumPredecessors => predecessors.Count;

            /// <summary>
            /// Returns the number of successors.
            /// </summary>
            public int NumSuccessors => successors.Count;

            #endregion

            #region Methods

            /// <summary>
            /// Adds the given block as successor.
            /// </summary>
            /// <param name="successor">The successor to add.</param>
            internal void AddSuccessor(Node successor)
            {
                Debug.Assert(successor != null, "Invalid successor");
                successors.Add(successor);
                successor.predecessors.Add(this);
            }

            /// <summary>
            /// Returns the successors of this node.
            /// </summary>
            public Enumerator GetEnumerator() => new Enumerator(successors);

            #endregion

            #region Object

            /// <summary>
            /// Returns the string representation of this CFG node.
            /// </summary>
            /// <returns>The string representation of this CFG node.</returns>
            public override string ToString() => Block.ToString();

            #endregion
        }

        /// <summary>
        /// Represents a node enumerator to iterate over all nodes
        /// in a control flow graph.
        /// </summary>
        public struct Enumerator : IEnumerator<Node>
        {
            #region Instance

            private Scope.Enumerator functionEnumerator;

            internal Enumerator(CFG parent)
            {
                Parent = parent;
                functionEnumerator = parent.Scope.GetEnumerator();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent graph.
            /// </summary>
            public CFG Parent { get; }

            /// <summary>
            /// Returns the current node.
            /// </summary>
            public Node Current => Parent.blockMapping[functionEnumerator.Current];

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose()
            {
                functionEnumerator.Dispose();
            }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext()
            {
                return functionEnumerator.MoveNext();
            }

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new cfg.
        /// </summary>
        /// <param name="scope">The parent scope.</param>
        public static CFG Create(Scope scope)
        {
            Debug.Assert(scope != null, "Invalid scope");
            return new CFG(scope);
        }

        #endregion

        #region Instance

        private readonly Dictionary<BasicBlock, Node> blockMapping =
            new Dictionary<BasicBlock, Node>();

        /// <summary>
        /// Constructs a new CFG.
        /// </summary>
        /// <param name="scope">The current scope.</param>
        private CFG(Scope scope)
        {
            Scope = scope;

            // Construct nodes
            int nodeIndex = 0;
            if ((scope.ScopeFlags & ScopeFlags.AddAlreadyVisitedNodes) != ScopeFlags.None)
            {
                foreach (var block in scope)
                {
                    if (blockMapping.ContainsKey(block))
                        continue;

                    blockMapping.Add(block, new Node(this, block, nodeIndex++));
                }
            }
            else
            {
                foreach (var block in scope)
                    blockMapping.Add(block, new Node(this, block, nodeIndex++));
            }

            // Build node mapping
            foreach (var entry in blockMapping)
            {
                var node = entry.Value;
                foreach (var successor in entry.Key.Successors)
                    node.AddSuccessor(this[successor]);
            }

            EntryNode = this[Scope.EntryBlock];
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent context.
        /// </summary>
        public IRContext Context => Scope.Context;

        /// <summary>
        /// Returns the parent method.
        /// </summary>
        public Method Method => Scope.Method;

        /// <summary>
        /// Return the associated scope.
        /// </summary>
        public Scope Scope { get; }

        /// <summary>
        /// Returns the entry point.
        /// </summary>
        public Node EntryNode { get; }

        /// <summary>
        /// Returns the number of nodes in the graph.
        /// </summary>
        public int Count => Scope.Count;

        /// <summary>
        /// Resolves the cfg node for the given basic block.
        /// </summary>
        /// <param name="block">The basic block to resolve.</param>
        /// <returns>The resolved basic block.</returns>
        public Node this[BasicBlock block]
        {
            get
            {
                Debug.Assert(block != null, "Invalid function value");
                return blockMapping[block];
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new SCC analysis.
        /// </summary>
        /// <returns>The created SCC analysis.</returns>
        public SCCs CreateSCCs() => SCCs.Create(this);

        /// <summary>
        /// Creates a new node mapping to associated with the current graph.
        /// </summary>
        /// <typeparam name="T">The target mapping type.</typeparam>
        /// <typeparam name="TProvider">The value provider for each node.</typeparam>
        /// <returns>The created mapping.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeMapping<T> CreateNodeMapping<T, TProvider>(in TProvider provider)
            where TProvider : INodeMappingValueProvider<T> =>
            NodeMapping<T>.Create(this, provider);

        /// <summary>
        /// Returns a node enumerator to iterate over all nodes
        /// stored in this graph in reverse post order.
        /// </summary>
        /// <returns>The resulting node enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        #endregion

        #region IEnumerable

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<Node> IEnumerable<Node>.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
