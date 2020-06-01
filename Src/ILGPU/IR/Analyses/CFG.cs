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
    /// Represents an abstract interface for all CFG nodes.
    /// </summary>
    public interface ICFGNode
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
    /// Represents a control-flow graph (CFG).
    /// </summary>
    /// <typeparam name="TOrder">The underlying block order.</typeparam>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    [SuppressMessage(
        "Microsoft.Naming",
        "CA1710:IdentifiersShouldHaveCorrectSuffix",
        Justification = "CFG stands for Control Flow Graph and not CFGCollection")]
    public sealed class CFG<TOrder, TDirection> :
        IReadOnlyCollection<CFG<TOrder, TDirection>.Node>
        where TOrder : struct, ITraversalOrder
        where TDirection : struct, IControlFlowDirection
    {
        #region Nested Types

        /// <summary>
        /// Represents a single node in the scope of a control-flow graph.
        /// </summary>
        public readonly struct Node : ICFGNode, ILocation
        {
            #region Nested Types

            /// <summary>
            /// Enumerates all CFG nodes.
            /// </summary>
            public ref struct Enumerator
            {
                [SuppressMessage(
                    "Style",
                    "IDE0044:Add readonly modifier",
                    Justification = "This instance variable will be modified")]
                private ReadOnlySpan<BasicBlock>.Enumerator nestedEnumerator;

                internal Enumerator(
                    CFG<TOrder, TDirection> cfg,
                    ReadOnlySpan<BasicBlock>.Enumerator enumerator)
                {
                    nestedEnumerator = enumerator;
                    CFG = cfg;
                }

                /// <summary>
                /// Returns the parent graph.
                /// </summary>
                public CFG<TOrder, TDirection> CFG { get; }

                /// <summary>
                /// Returns the current CFG node.
                /// </summary>
                public Node Current => CFG[nestedEnumerator.Current];

                /// <summary>
                /// Moves the enumerator to the next node.
                /// </summary>
                /// <returns>True, if the enumerator could be moved.</returns>
                public bool MoveNext() => nestedEnumerator.MoveNext();
            }

            /// <summary>
            /// Represents a node collection of attached nodes.
            /// </summary>
            public readonly ref struct NodeCollection
            {
                #region Instance

                private readonly ReadOnlySpan<BasicBlock> links;

                internal NodeCollection(
                    CFG<TOrder, TDirection> cfg,
                    in ReadOnlySpan<BasicBlock> collection)
                {
                    CFG = cfg;
                    links = collection;
                }

                #endregion

                #region Properties

                /// <summary>
                /// Returns the parent graph.
                /// </summary>
                public CFG<TOrder, TDirection> CFG { get; }

                /// <summary>
                /// Returns the number of nodes.
                /// </summary>
                public int Count => links.Length;

                /// <summary>
                /// Returns the i-th node.
                /// </summary>
                /// <param name="index">The relative node index.</param>
                /// <returns>The resolved node.</returns>
                public Node this[int index] => CFG[links[index]];

                #endregion

                #region IEnumerable

                /// <summary>
                /// Returns a node enumerator to iterate over all attached nodes.
                /// </summary>
                /// <returns>The resulting node enumerator.</returns>
                public Enumerator GetEnumerator() =>
                    new Enumerator(CFG, links.GetEnumerator());

                #endregion
            }

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new node.
            /// </summary>
            /// <param name="cfg">The parent graph.</param>
            /// <param name="block">The associated block.</param>
            /// <param name="traversalIndex">The traversal index.</param>
            internal Node(
                CFG<TOrder, TDirection> cfg,
                BasicBlock block,
                int traversalIndex)
            {
                CFG = cfg;
                Block = block;
                TraversalIndex = traversalIndex;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent graph.
            /// </summary>
            public CFG<TOrder, TDirection> CFG { get; }

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
            public NodeCollection Predecessors => new NodeCollection(
                CFG,
                GetPredecessors());

            /// <summary>
            /// Returns the successors of this node.
            /// </summary>
            public NodeCollection Successors => new NodeCollection(
                CFG,
                GetSuccessors());

            /// <summary>
            /// Returns the number of predecessors.
            /// </summary>
            public int NumPredecessors => GetPredecessors().Length;

            /// <summary>
            /// Returns the number of successors.
            /// </summary>
            public int NumSuccessors => GetSuccessors().Length;

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
            private ReadOnlySpan<BasicBlock> GetPredecessors() =>
                Block.GetPredecessors<TDirection>();

            /// <summary>
            /// Determines the actual successors based on the current direction.
            /// </summary>
            private ReadOnlySpan<BasicBlock> GetSuccessors() =>
                Block.GetSuccessors<TDirection>();

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
            public static implicit operator BasicBlock(Node node) =>
                node.Block;

            #endregion
        }

        /// <summary>
        /// Enumerates all CFG nodes.
        /// </summary>
        public struct Enumerator<TNestedEnumerator> : IEnumerator<Node>
            where TNestedEnumerator : IEnumerator<BasicBlock>
        {
            [SuppressMessage(
                "Style",
                "IDE0044:Add readonly modifier",
                Justification = "This instance variable will be modified")]
            private TNestedEnumerator nestedEnumerator;

            internal Enumerator(
                CFG<TOrder, TDirection> cfg,
                TNestedEnumerator enumerator)
            {
                nestedEnumerator = enumerator;
                CFG = cfg;
            }

            /// <summary>
            /// Returns the parent graph.
            /// </summary>
            public CFG<TOrder, TDirection> CFG { get; }

            /// <summary>
            /// Returns the current CFG node.
            /// </summary>
            public Node Current => CFG[nestedEnumerator.Current];

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            void IDisposable.Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => nestedEnumerator.MoveNext();

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        /// <summary>
        /// Provides traversal indices.
        /// </summary>
        private readonly struct TraversalIndexProvider :
            IBasicBlockMapValueProvider<int>
        {
            /// <summary>
            /// Returns the current traversal index.
            /// </summary>
            public readonly int GetValue(BasicBlock block, int traversalIndex) =>
                traversalIndex;
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new CFG.
        /// </summary>
        /// <param name="blocks">The block collection.</param>
        public static CFG<TOrder, TDirection> Create(
            in BasicBlockCollection<TOrder, TDirection> blocks) =>
            new CFG<TOrder, TDirection>(blocks);

        #endregion

        #region Instance

        private readonly BasicBlockMap<int> numbering;

        /// <summary>
        /// Constructs a new CFG.
        /// </summary>
        /// <param name="blocks">The block collection.</param>
        private CFG(in BasicBlockCollection<TOrder, TDirection> blocks)
        {
            numbering = blocks.CreateMap(new TraversalIndexProvider());
            Blocks = blocks;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying blocks.
        /// </summary>
        public BasicBlockCollection<TOrder, TDirection> Blocks { get; }

        /// <summary>
        /// Returns the number of nodes in the graph.
        /// </summary>
        public int Count => numbering.Count;

        /// <summary>
        /// Returns the root node.
        /// </summary>
        public Node Root => new Node(
            this,
            Blocks.EntryBlock,
            numbering[Blocks.EntryBlock]);

        /// <summary>
        /// Resolves the CFG node for the given basic block.
        /// </summary>
        /// <param name="block">The basic block to resolve.</param>
        /// <returns>The resolved basic block.</returns>
        public Node this[BasicBlock block] => new Node(
            this,
            block,
            numbering[block]);

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns a node enumerator to iterate over all nodes stored in this graph
        /// using the current order.
        /// </summary>
        /// <returns>The resulting node enumerator.</returns>
        public Enumerator<BasicBlockCollection<TOrder, TDirection>.Enumerator>
            GetEnumerator() =>
            new Enumerator<BasicBlockCollection<TOrder, TDirection>.Enumerator>(
                this,
                Blocks.GetEnumerator());

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<Node> IEnumerable<Node>.GetEnumerator() => GetEnumerator();

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
        /// Creates a new CFG based on the given blocks.
        /// </summary>
        /// <typeparam name="TOrder">The underlying block order.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="blocks">The block collection.</param>
        /// <returns>The created CFG.</returns>
        public static CFG<TOrder, TDirection> CreateCFG<TOrder, TDirection>(
            this BasicBlockCollection<TOrder, TDirection> blocks)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection =>
            CFG<TOrder, TDirection>.Create(blocks);
    }
}
