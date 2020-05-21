// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Dominators.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// Implements a dominator analysis.
    /// </summary>
    /// <typeparam name="TDirection">The control flow direction.</typeparam>
    public sealed class Dominators<TDirection>
        where TDirection : struct, IControlFlowDirection
    {
        #region Static

        /// <summary>
        /// Creates a dominator analysis.
        /// </summary>
        /// <param name="cfg">The control flow graph.</param>
        public static Dominators<TDirection> Create(
            CFG<ReversePostOrder, TDirection> cfg) =>
            new Dominators<TDirection>(cfg);

        #endregion

        #region Instance

        /// <summary>
        /// Stores all idoms in RPO.
        /// </summary>
        private readonly int[] idomsInRPO;

        /// <summary>
        /// Stores all CFG nodes in RPO.
        /// </summary>
        private readonly CFG.Node<TDirection>[] nodesInRPO;

        /// <summary>
        /// Constructs the dominators for the given control flow graph.
        /// </summary>
        /// <param name="cfg">The control flow graph.</param>
        private Dominators(CFG<ReversePostOrder, TDirection> cfg)
        {
            CFG = cfg ?? throw new ArgumentNullException(nameof(cfg));

            idomsInRPO = new int[cfg.Count];
            nodesInRPO = new CFG.Node<TDirection>[cfg.Count];

            idomsInRPO[0] = 0;
            for (int i = 1, e = idomsInRPO.Length; i < e; ++i)
                idomsInRPO[i] = -1;

            bool changed;
            do
            {
                changed = false;
                var enumerator = cfg.GetEnumerator();
                enumerator.MoveNext();
                var node = enumerator.Current;
                nodesInRPO[node.TraversalIndex] = node;

                while (enumerator.MoveNext())
                {
                    node = enumerator.Current;
                    nodesInRPO[node.TraversalIndex] = node;
                    int currentIdom = -1;
                    foreach (var pred in node.Predecessors)
                    {
                        var predRPO = pred.TraversalIndex;
                        if (idomsInRPO[predRPO] != -1)
                        {
                            currentIdom = predRPO;
                            break;
                        }
                    }

                    Debug.Assert(currentIdom != -1, "Invalid idom");
                    foreach (var pred in node.Predecessors)
                    {
                        var predRPO = pred.TraversalIndex;
                        if (idomsInRPO[predRPO] != -1)
                            currentIdom = Intersect(currentIdom, predRPO);
                    }

                    var rpoNumber = node.TraversalIndex;
                    if (idomsInRPO[rpoNumber] != currentIdom)
                    {
                        idomsInRPO[rpoNumber] = currentIdom;
                        changed = true;
                    }
                }
            }
            while (changed);
        }

        /// <summary>
        /// Intersects two RPO numbers in  order to find the associated idom.
        /// </summary>
        /// <returns>The resulting LCA node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Intersect(int left, int right)
        {
            while (left != right)
            {
                while (left < right)
                    right = idomsInRPO[right];
                while (right < left)
                    left = idomsInRPO[left];
            }
            return left;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated control flow graph.
        /// </summary>
        public CFG<ReversePostOrder, TDirection> CFG { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the given <paramref name="cfgNode"/> node is
        /// dominated by the <paramref name="dominator"/> node.
        /// </summary>
        /// <param name="cfgNode">The node.</param>
        /// <param name="dominator">The potential dominator.</param>
        /// <returns>True, if the given node is dominated by the dominator.</returns>
        public bool IsDominatedBy(
            CFG.Node<TDirection> cfgNode,
            CFG.Node<TDirection> dominator)
        {
            Debug.Assert(cfgNode != null, "Invalid CFG node");
            Debug.Assert(dominator != null, "Invalid dominator");

            var left = cfgNode.TraversalIndex;
            var right = dominator.TraversalIndex;
            return Intersect(left, right) == right;
        }

        /// <summary>
        /// Returns true if the given <paramref name="dominator"/> node. is
        /// dominating the <paramref name="cfgNode"/> node.
        /// </summary>
        /// <param name="dominator">The potential dominator.</param>
        /// <param name="cfgNode">The other node.</param>
        /// <returns>True, if the given node is dominating the other node.</returns>
        public bool Dominates(
            CFG.Node<TDirection> dominator,
            CFG.Node<TDirection> cfgNode) =>
            IsDominatedBy(cfgNode, dominator);

        /// <summary>
        /// Returns the first dominator of the given node.
        /// This might be the node itself if there are no other
        /// dominators.
        /// </summary>
        /// <param name="cfgNode">The node.</param>
        /// <returns>The first dominator.</returns>
        public CFG.Node<TDirection> GetImmediateDominator(CFG.Node<TDirection> cfgNode)
        {
            Debug.Assert(cfgNode != null, "Invalid CFG node");
            var rpoNumber = idomsInRPO[cfgNode.TraversalIndex];
            return nodesInRPO[rpoNumber];
        }

        /// <summary>
        /// Returns the first dominator of the given node.
        /// This might be the node itself if there are no other
        /// dominators.
        /// </summary>
        /// <param name="first">The first node.</param>
        /// <param name="second">The first node.</param>
        /// <returns>The first dominator.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CFG.Node<TDirection> GetImmediateCommonDominator(
            CFG.Node<TDirection> first,
            CFG.Node<TDirection> second)
        {
            Debug.Assert(first != null, "Invalid first CFG node");
            Debug.Assert(second != null, "Invalid second CFG node");

            if (first == second)
                return first;

            var left = first.TraversalIndex;
            var right = second.TraversalIndex;
            var idom = Intersect(left, right);
            return nodesInRPO[idom];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CFG.Node<TDirection> GetImmediateCommonDominator<TList>(
            TList nodes)
            where TList : IReadOnlyList<CFG.Node<TDirection>>
        {
            if (nodes.Count < 1)
                return null;
            var result = nodes[0];
            for (int i = 1, e = nodes.Count; i < e; ++i)
                result = GetImmediateCommonDominator(result, nodes[i]);
            return result;
        }

        #endregion
    }

    public static class Dominators
    {
        public static Dominators<TDirection> ComputeDominators<TDirection>(
            this CFG<ReversePostOrder, TDirection> cfg)
            where TDirection : struct, IControlFlowDirection =>
            Dominators<TDirection>.Create(cfg);
    }
}
