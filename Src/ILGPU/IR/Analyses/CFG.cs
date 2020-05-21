// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CFG.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// Represents a control flow graph (CFG).
    /// </summary>
    /// <typeparam name="TOrder">
    /// The order which has been used to construct this CFG.
    /// </typeparam>
    /// <typeparam name="TDirection">The control flow direction.</typeparam>
    [SuppressMessage(
        "Microsoft.Naming",
        "CA1710:IdentifiersShouldHaveCorrectSuffix",
        Justification = "CFG stands for Control Flow Graph and not CFGCollection")]
    public sealed class CFG<TOrder, TDirection> :
        IReadOnlyCollection<CFG.Node<TDirection>>
        where TOrder : struct, ITraversalOrder
        where TDirection : struct, IControlFlowDirection
    {
        #region Nested Types

        /// <summary>
        /// Enumerates all CFG nodes.
        /// </summary>
        public struct Enumerator : IEnumerator<CFG.Node<TDirection>>
        {
            private BasicBlockCollection<TOrder>.Enumerator enumerator;
            private BasicBlockMap<CFG.Node<TDirection>> mapping;

            internal Enumerator(
                in BasicBlockCollection<TOrder> collection,
                in BasicBlockMap<CFG.Node<TDirection>> map)
            {
                enumerator = collection.GetEnumerator();
                mapping = map;
            }

            /// <summary>
            /// Returns the current CFG node.
            /// </summary>
            public CFG.Node<TDirection> Current => mapping[enumerator.Current];

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            void IDisposable.Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => enumerator.MoveNext();

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new CFG.
        /// </summary>
        /// <param name="collection">The block collection.</param>
        public static CFG<TOrder, TDirection> Create(
            in BasicBlockCollection<TOrder> collection) =>
            new CFG<TOrder, TDirection>(collection);

        #endregion

        #region Instance

        private readonly BasicBlockMap<CFG.Node<TDirection>> mapping;

        /// <summary>
        /// Constructs a new CFG.
        /// </summary>
        /// <param name="collection">The block collection.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CFG(in BasicBlockCollection<TOrder> collection)
        {
            mapping = collection.CreateMap(
                (block, index) => new CFG.Node<TDirection>(block, index));
            Collection = collection;
            Root = mapping[collection.EntryBlock];

            // Build node mapping
            foreach (var (block, node) in mapping)
            {
                foreach (var successor in block.Successors)
                    node.AddSuccessor(this[successor]);
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying collection.
        /// </summary>
        public BasicBlockCollection<TOrder> Collection { get; }

        /// <summary>
        /// Returns the number of nodes in the graph.
        /// </summary>
        public int Count => mapping.Count;

        /// <summary>
        /// Returns the root node.
        /// </summary>
        public CFG.Node<TDirection> Root { get; }

        /// <summary>
        /// Resolves the CFG node for the given basic block.
        /// </summary>
        /// <param name="block">The basic block to resolve.</param>
        /// <returns>The resolved basic block.</returns>
        public CFG.Node<TDirection> this[BasicBlock block] => mapping[block];

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new CFG-based mapping.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="valueProvider">The value provider.</param>
        /// <returns>The created traversal map.</returns>
        public BasicBlockMap<T> CreateMapping<T>(
            Func<CFG.Node<TDirection>, T> valueProvider) =>
            mapping.Remap<T>(valueProvider);

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns a node enumerator to iterate over all nodes stored in this graph
        /// using the current order.
        /// </summary>
        /// <returns>The resulting node enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(
            Collection,
            mapping);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<CFG.Node<TDirection>>
            IEnumerable<CFG.Node<TDirection>>.GetEnumerator() => GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }

    /// <summary>
    /// Helper utility for the class <see cref="CFG{TOrder, TDirection}"/>.
    /// </summary>
    public static class CFG
    {
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
            int TraversalIndex { get; }
        }

        /// <summary>
        /// Represents a single node in the scope of a control flow graph.
        /// </summary>
        /// <typeparam name="TDirection">The control flow direction.</typeparam>
        public sealed class Node<TDirection> : INode, ILocation
            where TDirection : struct, IControlFlowDirection
        {
            #region Nested Types

            /// <summary>
            /// Represents a node collection of attached nodes.
            /// </summary>
            public readonly struct NodeCollection : IReadOnlyList<Node<TDirection>>
            {
                #region Instance

                internal NodeCollection(List<Node<TDirection>> nodes)
                {
                    Nodes = nodes;
                }

                #endregion

                #region Properties

                /// <summary>
                /// Returns the associated node set.
                /// </summary>
                private List<Node<TDirection>> Nodes { get; }

                /// <summary cref="IReadOnlyCollection{T}.Count"/>
                public int Count => Nodes.Count;

                /// <summary>
                /// Returns the i-th node.
                /// </summary>
                /// <param name="index">The relative node index.</param>
                /// <returns>The resolved node.</returns>
                public Node<TDirection> this[int index] => Nodes[index];

                #endregion

                #region Methods

                /// <summary>
                /// Tries to find a node with the given id.
                /// </summary>
                /// <param name="nodeId">The node id to look for.</param>
                /// <returns>The found node (if any).</returns>
                public Node<TDirection> Find(NodeId nodeId)
                {
                    foreach (var node in Nodes)
                    {
                        if (node.Block.Id == nodeId)
                            return node;
                    }
                    return null;
                }

                #endregion

                #region IEnumerable

                /// <summary>
                /// Returns a node enumerator to iterate over all attached nodes.
                /// </summary>
                /// <returns>The resulting node enumerator.</returns>
                public Enumerator GetEnumerator() => new Enumerator(Nodes);

                /// <summary cref="IEnumerable{T}.GetEnumerator"/>
                IEnumerator<Node<TDirection>>
                    IEnumerable<Node<TDirection>>.GetEnumerator() =>
                    GetEnumerator();

                /// <summary cref="IEnumerable.GetEnumerator"/>
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

                #endregion
            }

            /// <summary>
            /// Represents a child enumerator.
            /// </summary>
            public struct Enumerator : IEnumerator<Node<TDirection>>
            {
                #region Instance

                private List<Node<TDirection>>.Enumerator enumerator;

                /// <summary>
                /// Constructs a new child enumerator.
                /// </summary>
                /// <param name="valueSet">The nodes to iterate over.</param>
                internal Enumerator(List<Node<TDirection>> valueSet)
                {
                    enumerator = valueSet.GetEnumerator();
                }

                #endregion

                #region Properties

                /// <summary>
                /// Returns the current node.
                /// </summary>
                public Node<TDirection> Current => enumerator.Current;

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                #endregion

                #region Methods

                /// <summary cref="IDisposable.Dispose"/>
                public void Dispose() => enumerator.Dispose();

                /// <summary cref="IEnumerator.MoveNext"/>
                public bool MoveNext() => enumerator.MoveNext();

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();

                #endregion
            }

            #endregion

            #region Instance

            private readonly List<Node<TDirection>> successors =
                new List<Node<TDirection>>(4);
            private readonly List<Node<TDirection>> predecessors =
                new List<Node<TDirection>>(4);

            /// <summary>
            /// Constructs a new node.
            /// </summary>
            /// <param name="block">The associated block.</param>
            /// <param name="traversalIndex">The traveral index.</param>
            internal Node(BasicBlock block, int traversalIndex)
            {
                Block = block;
                TraversalIndex = traversalIndex;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated block.
            /// </summary>
            public BasicBlock Block { get; }

            /// <summary>
            /// Returns the zero-based traversal index that has been assigned during
            /// traversal of all input blocks.
            /// </summary>
            public int TraversalIndex { get; }

            /// <summary>
            /// Returns the predecessors of this node.
            /// </summary>
            public NodeCollection Predecessors => new NodeCollection(GetPredecessors());

            /// <summary>
            /// Returns the successors of this node.
            /// </summary>
            public NodeCollection Successors => new NodeCollection(GetSuccessors());

            /// <summary>
            /// Returns the number of predecessors.
            /// </summary>
            public int NumPredecessors => GetPredecessors().Count;

            /// <summary>
            /// Returns the number of successors.
            /// </summary>
            public int NumSuccessors => GetSuccessors().Count;

            #endregion

            #region Methods

            /// <summary>
            /// Formats an error message to include specific exception information.
            /// </summary>
            /// <param name="message">The source error message.</param>
            /// <returns>The formatted error message.</returns>
            public string FormatErrorMessage(string message) =>
                Block.FormatErrorMessage(message);

            /// <summary>
            /// Determines the actual predecessors based on the current direction.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private List<Node<TDirection>> GetPredecessors()
            {
                TDirection direction = default;
                return direction.GetPredecessors<
                    Node<TDirection>,
                    List<Node<TDirection>>>(
                    predecessors,
                    successors);
            }

            /// <summary>
            /// Determines the actual successors based on the current direction.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private List<Node<TDirection>> GetSuccessors()
            {
                TDirection direction = default;
                return direction.GetSuccessors<
                    Node<TDirection>,
                    List<Node<TDirection>>>(
                    predecessors,
                    successors);
            }

            /// <summary>
            /// Adds the given block as successor.
            /// </summary>
            /// <param name="successor">The successor to add.</param>
            internal void AddSuccessor(Node<TDirection> successor)
            {
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

            #region Operators

            /// <summary>
            /// Converts the given node implicitly to its underlying basic block.
            /// </summary>
            /// <param name="node">The node to convert.</param>
            public static implicit operator BasicBlock(Node<TDirection> node) =>
                node.Block;

            #endregion
        }

        public static CFG<TOrder, Forwards> CreateCFG<TOrder>(
            this BasicBlockCollection<TOrder> collection)
            where TOrder : struct, ITraversalOrder =>
            collection.CreateCFG<TOrder, Forwards>();

        public static CFG<TOrder, TDirection> CreateCFG<TOrder, TDirection>(
            this BasicBlockCollection<TOrder> collection)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection =>
            CFG<TOrder, TDirection>.Create(collection);
    }
}
