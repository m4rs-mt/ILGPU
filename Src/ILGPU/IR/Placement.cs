// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Placement.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents a builder to construct placement information.
    /// </summary>
    public interface IPlacementBuilder
    {
        /// <summary>
        /// Places the given node in the specified CFG node.
        /// </summary>
        /// <param name="node">The node to place.</param>
        /// <param name="cfgNode">The target CFG node.</param>
        void Place(Value node, CFGNode cfgNode);

        /// <summary>
        /// Tries to resolve placement information.
        /// </summary>
        /// <param name="node">The node to resolve.</param>
        /// <param name="cfgNode">The resolved CFG node.</param>
        /// <returns>True, iff the placement information could be resolved.</returns>
        bool TryResolvePlacement(Value node, out CFGNode cfgNode);
    }

    /// <summary>
    /// A generic placement strategy.
    /// </summary>
    public interface IPlacementStrategy
    {
        /// <summary>
        /// Places the given node using the specified placement builder.
        /// </summary>
        /// <typeparam name="TPlacementBuilder">The placement builder type.</typeparam>
        /// <param name="scope">The current scope.</param>
        /// <param name="node">The node to place.</param>
        /// <param name="builder">The used placement builder.</param>
        /// <returns>The target CFG node in which the node has been placed.</returns>
        CFGNode Place<TPlacementBuilder>(Scope scope, Value node, ref TPlacementBuilder builder)
            where TPlacementBuilder : struct, IPlacementBuilder;
    }

    /// <summary>
    /// Represents an instruction-placement analysis.
    /// </summary>
    public sealed class Placement
    {
        #region Nested Types

        /// <summary>
        /// Represents a placement enumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<Value>
        {
            #region Instance

            private readonly List<Value> nodes;
            private int index;

            internal Enumerator(List<Value> nodeList)
                : this(nodeList, -1)
            { }

            internal Enumerator(List<Value> nodeList, int startIndex)
            {
                nodes = nodeList;
                index = startIndex;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the current node.
            /// </summary>
            public Value Current => nodes[index];

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext()
            {
                if (index + 1 >= nodes.Count)
                    return false;
                ++index;
                return true;
            }

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();

            #endregion
        }

        /// <summary>
        /// Represents the placement within a single block.
        /// </summary>
        internal sealed class BlockPlacement
        {
            private readonly Dictionary<Value, int> indexLookup =
                new Dictionary<Value, int>();
            private readonly List<Value> orderedNodes = new List<Value>();
            private readonly HashSet<Value> nodeSet = new HashSet<Value>();

            /// <summary>
            /// Constructs a new block placement.
            /// </summary>
            /// <param name="cfgNode">The parent CFG node.</param>
            public BlockPlacement(CFGNode cfgNode)
            {
                CFGNode = cfgNode;
            }

            /// <summary>
            /// Returns the associated CFG node.
            /// </summary>
            public CFGNode CFGNode { get; }

            /// <summary>
            /// Adds a node to this block.
            /// </summary>
            /// <param name="node">The node to append.</param>
            public void Add(Value node)
            {
                Debug.Assert(node != null, "Invalid node");
                nodeSet.Add(node);
            }

            /// <summary>
            /// Computes the correct order of all nodes in this block.
            /// </summary>
            /// <param name="nodeMarker">The active node marker.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ComputeOrder(NodeMarker nodeMarker)
            {
                orderedNodes.Capacity = nodeSet.Count;
                foreach (var node in nodeSet)
                    ComputeOrder(node, nodeMarker);
                Debug.Assert(
                    nodeSet.Count >= orderedNodes.Count,
                    "Inconsistent node information");
#if DEBUG
                foreach (var orderedNode in orderedNodes)
                    Debug.Assert(nodeSet.Contains(orderedNode));
#endif
            }

            /// <summary>
            /// Computes the order of the given node.
            /// </summary>
            /// <param name="node">The node.</param>
            /// <param name="nodeMarker">The active node marker.</param>
            private void ComputeOrder(Value node, NodeMarker nodeMarker)
            {
                Debug.Assert(nodeSet.Contains(node), "Invalid node");
                if (!node.Mark(nodeMarker))
                    return;
                foreach (var childNodeRef in node)
                {
                    var childNode = childNodeRef.Resolve();
                    if (!nodeSet.Contains(childNode))
                        continue;
                    ComputeOrder(childNode, nodeMarker);
                }
                if (node as FunctionCall == null)
                    orderedNodes.Add(node);
            }

            /// <summary>
            /// Returns an enumerator for nodes in this block.
            /// </summary>
            /// <returns>An enumerator for nodes in this block.</returns>
            public Enumerator GetEnumerator() => new Enumerator(orderedNodes);

            /// <summary>
            /// Returns an enumerator that enumerates the remaining nodes
            /// in this block starting with the given one.
            /// </summary>
            /// <param name="nodeOffset">The node to start the enumeration at.</param>
            /// <returns>An enumerator for nodes in this block.</returns>
            public Enumerator GetEnumerator(Value nodeOffset)
            {
                var offset = indexLookup[nodeOffset];
                return new Enumerator(orderedNodes, offset);
            }
        }

        /// <summary>
        /// The internal placement builder.
        /// </summary>
        private struct PlacementBuilder : IPlacementBuilder
        {
            /// <summary>
            /// Constructs a new internal placement builder.
            /// </summary>
            /// <param name="nodeMapping">The node mapping.</param>
            /// <param name="placements">The placement mapping.</param>
            public PlacementBuilder(
                Dictionary<Value, CFGNode> nodeMapping,
                Dictionary<CFGNode, BlockPlacement> placements)
            {
                NodeMapping = nodeMapping;
                Placements = placements;
            }

            /// <summary>
            /// Returns the associated node mapping.
            /// </summary>
            public Dictionary<Value, CFGNode> NodeMapping { get; }

            /// <summary>
            /// Returns the associated placement mapping.
            /// </summary>
            public Dictionary<CFGNode, BlockPlacement> Placements { get; }

            /// <summary cref="IPlacementBuilder.Place(Value, CFGNode)"/>
            public void Place(Value node, CFGNode cfgNode)
            {
                NodeMapping[node] = cfgNode;
                Placements[cfgNode].Add(node);
            }

            /// <summary cref="IPlacementBuilder.TryResolvePlacement(Value, out CFGNode)"/>
            public bool TryResolvePlacement(Value node, out CFGNode cfgNode)
            {
                return NodeMapping.TryGetValue(node, out cfgNode);
            }
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new placement analysis.
        /// </summary>
        /// <param name="cfg">The required CFG.</param>
        /// <param name="strategy">The used placement strategy.</param>
        public static Placement Create<TStrategy>(CFG cfg, in TStrategy strategy)
            where TStrategy : IPlacementStrategy
        {
            var result = new Placement(cfg);
            result.ComputePlacement(strategy);
            return result;
        }

        /// <summary>
        /// Creates a new placement analysis (async).
        /// </summary>
        /// <param name="cfg">The required CFG.</param>
        /// <param name="strategy">The used placement strategy.</param>
        public static Task<Placement> CreateAsync<TStrategy>(CFG cfg, TStrategy strategy)
            where TStrategy : IPlacementStrategy
        {
            return Task.Run(() => Create(cfg, strategy));
        }

        /// <summary>
        /// Creates a new placement analysis using the <see cref="CSEPlacementStrategy"/>.
        /// </summary>
        /// <param name="cfg">The required CFG.</param>
        public static Placement CreateCSEPlacement(CFG cfg) =>
            CreateCSEPlacement(Dominators.Create(cfg));

        /// <summary>
        /// Creates a new placement analysis using the <see cref="CSEPlacementStrategy"/>.
        /// </summary>
        /// <param name="dominators">The required dominators.</param>
        public static Placement CreateCSEPlacement(Dominators dominators)
        {
            if (dominators == null)
                throw new ArgumentNullException(nameof(dominators));
            return Create(dominators.CFG, new CSEPlacementStrategy(dominators));
        }

        /// <summary>
        /// Creates a new placement analysis (async) using the <see cref="CSEPlacementStrategy"/>
        /// </summary>
        /// <param name="cfg">The required CFG.</param>
        public static Task<Placement> CreateCSEPlacementAsync(CFG cfg)
        {
            return Task.Run(() => CreateCSEPlacement(cfg));
        }

        #endregion

        #region Instance

        /// <summary>
        /// Maps nodes to a single placement-information instance.
        /// </summary>
        private readonly Dictionary<Value, CFGNode> nodeMapping =
            new Dictionary<Value, CFGNode>();

        /// <summary>
        /// Maps CFG nodes to placement information.
        /// </summary>
        private readonly Dictionary<CFGNode, BlockPlacement> placements =
            new Dictionary<CFGNode, BlockPlacement>();

        /// <summary>
        /// Creates a new placement analysis.
        /// </summary>
        /// <param name="cfg">The required CFG.</param>
        private Placement(CFG cfg)
        {
            CFG = cfg ?? throw new ArgumentNullException(nameof(cfg));

            foreach (var function in Scope.Functions)
            {
                var cfgNode = CFG[function];
                var blockPlacement = new BlockPlacement(cfgNode);
                placements[cfgNode] = blockPlacement;

                nodeMapping[function] = cfgNode;
                foreach (var param in function.Parameters)
                    nodeMapping[param] = cfgNode;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent context.
        /// </summary>
        public IRContext Context => CFG.Context;

        /// <summary>
        /// Returns the associated control flow graph.
        /// </summary>
        public CFG CFG { get; }

        /// <summary>
        /// Returns the associated scope.
        /// </summary>
        public Scope Scope => CFG.Scope;

        /// <summary>
        /// Resolves a placement enumerator for all nodes that have been placed
        /// placed in the given CFG node.
        /// </summary>
        /// <param name="cfgNode">The CFG node.</param>
        /// <returns>The resolved placement enumerator.</returns>
        public Enumerator this[CFGNode cfgNode] => placements[cfgNode].GetEnumerator();

        /// <summary>
        /// Resolves the target CFG node in which the given node was placed.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>The resolved CFG node in which the node was placed.</returns>
        public CFGNode this[Value node] => nodeMapping[node];

        #endregion

        #region Methods

        /// <summary>
        /// Computes the actual placement using the given strategy.
        /// </summary>
        /// <typeparam name="TStrategy">The type of the strategy.</typeparam>
        /// <param name="strategy">The placement strategy.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ComputePlacement<TStrategy>(in TStrategy strategy)
            where TStrategy : IPlacementStrategy
        {
            var builder = new PlacementBuilder(
                nodeMapping,
                placements);

            // Place all nodes
            foreach (var node in Scope)
                strategy.Place(Scope, node, ref builder);

            // Compute the correct node order in all blocks
            var nodeMarker = Context.NewNodeMarker();
            foreach (var block in placements.Values)
                block.ComputeOrder(nodeMarker);
        }

        /// <summary>
        /// Enumerates the next instructions in the associated block starting
        /// with the given node.
        /// </summary>
        /// <param name="node">The node to start the enumeration at.</param>
        /// <returns>The resolved placement enumerator.</returns>
        public Enumerator GetNextInstructions(Value node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            var cfgNode = this[node];
            return placements[cfgNode].GetEnumerator(node);
        }

        /// <summary>
        /// Visits all nodes in the approproate order.
        /// Blocks are visited in reverse post order while nodes within
        /// blocks are visited in placement order.
        /// </summary>
        /// <typeparam name="T">The visitor type.</typeparam>
        /// <param name="visitor">The visitor instance.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VisitNodes<T>(T visitor)
            where T : IValueVisitor
        {
            foreach (var node in CFG)
                VisitNodes(visitor, node);
        }

        /// <summary>
        /// Visits all nodes in the block in placement order.
        /// </summary>
        /// <typeparam name="T">The visitor type.</typeparam>
        /// <param name="visitor">The visitor instance.</param>
        /// <param name="cfgNode">The CFG node.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void VisitNodes<T>(T visitor, CFGNode cfgNode)
            where T : IValueVisitor
        {
            using (var enumerator = this[cfgNode])
            {
                while (enumerator.MoveNext())
                    enumerator.Current.Accept(visitor);
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a placement strategy that uses dominators to maximize the
    /// reuse of already computed values.
    /// </summary>
    public readonly struct CSEPlacementStrategy : IPlacementStrategy
    {
        /// <summary>
        /// Constructs a placement strategy.
        /// </summary>
        /// <param name="dominators">The associated dominators.</param>
        public CSEPlacementStrategy(Dominators dominators)
        {
            Dominators = dominators ?? throw new ArgumentNullException(nameof(dominators));
        }

        /// <summary>
        /// Returns the associated dominators.
        /// </summary>
        public Dominators Dominators { get; }

        /// <summary cref="IPlacementStrategy.Place{TPlacementBuilder}(Scope, Value, ref TPlacementBuilder)"/>
        public CFGNode Place<TPlacementBuilder>(Scope scope, Value node, ref TPlacementBuilder builder)
            where TPlacementBuilder : struct, IPlacementBuilder
        {
            if (builder.TryResolvePlacement(node, out CFGNode cfgNode))
                return cfgNode;

            var uses = scope.GetUses(node);
            using (var useEnumerator = uses.GetEnumerator())
            {
                var moved = useEnumerator.MoveNext();
                Debug.Assert(moved, "Unreachable nodes should not be visible");

                var currentNode = Place(scope, useEnumerator.Current.Target, ref builder);
                while (useEnumerator.MoveNext())
                {
                    var otherNode = Place(scope, useEnumerator.Current.Target, ref builder);
                    currentNode = Dominators.GetImmediateCommonDominator(currentNode, otherNode);
                }

                builder.Place(node, currentNode);
                return currentNode;
            }
        }
    }
}
