// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Dominators.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// Implements a dominator analysis.
    /// </summary>
    public sealed class Dominators
    {
        #region Static

        /// <summary>
        /// Creates a dominator analysis.
        /// </summary>
        /// <param name="cfg">The control flow graph.</param>
        public static Dominators Create(CFG cfg) => new Dominators(cfg);

        #endregion

        #region Instance

        /// <summary>
        /// Stores all idoms in RPO.
        /// </summary>
        private readonly int[] idomsInRPO;

        /// <summary>
        /// Stores all CFG nodes in RPO.
        /// </summary>
        private readonly CFG.Node[] nodesInRPO;

        /// <summary>
        /// Constructs the dominators for the given control flow graph.
        /// </summary>
        /// <param name="cfg">The control flow graph.</param>
        private Dominators(CFG cfg)
        {
            CFG = cfg ?? throw new ArgumentNullException(nameof(cfg));

            idomsInRPO = new int[cfg.Count];
            nodesInRPO = new CFG.Node[cfg.Count];

            idomsInRPO[0] = 0;
            for (int i = 1, e = idomsInRPO.Length; i < e; ++i)
                idomsInRPO[i] = -1;

            bool changed;
            do
            {
                changed = false;
                using (var enumerator = cfg.GetEnumerator())
                {
                    enumerator.MoveNext();
                    var node = enumerator.Current;
                    nodesInRPO[node.NodeIndex] = node;

                    while (enumerator.MoveNext())
                    {
                        node = enumerator.Current;
                        nodesInRPO[node.NodeIndex] = node;
                        int currentIdom = -1;
                        foreach (var pred in node.Predecessors)
                        {
                            var predRPO = pred.NodeIndex;
                            if (idomsInRPO[predRPO] != -1)
                            {
                                currentIdom = predRPO;
                                break;
                            }
                        }

                        Debug.Assert(currentIdom != -1, "Invalid idom");
                        foreach (var pred in node.Predecessors)
                        {
                            var predRPO = pred.NodeIndex;
                            if (idomsInRPO[predRPO] != -1)
                                currentIdom = Intersect(currentIdom, predRPO);
                        }

                        var rpoNumber = node.NodeIndex;
                        if (idomsInRPO[rpoNumber] != currentIdom)
                        {
                            idomsInRPO[rpoNumber] = currentIdom;
                            changed = true;
                        }
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
        /// Returns the parent context.
        /// </summary>
        public IRContext Context => CFG.Context;

        /// <summary>
        /// Returns the associated control flow graph.
        /// </summary>
        public CFG CFG { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true iff the given <paramref name="cfgNode"/> node is
        /// dominated by the <paramref name="dominator"/> node.
        /// </summary>
        /// <param name="cfgNode">The node.</param>
        /// <param name="dominator">The potential dominator.</param>
        /// <returns>True, iff the given node is dominated by the dominator.</returns>
        public bool IsDominatedBy(CFG.Node cfgNode, CFG.Node dominator)
        {
            Debug.Assert(cfgNode != null, "Invalid CFG node");
            Debug.Assert(dominator != null, "Invalid dominator");

            var left = cfgNode.NodeIndex;
            var right = dominator.NodeIndex;
            return Intersect(left, right) == right;
        }

        /// <summary>
        /// Returns true iff the given <paramref name="dominator"/> node. is
        /// dominating the <paramref name="cfgNode"/> node.
        /// </summary>
        /// <param name="dominator">The potential dominator.</param>
        /// <param name="cfgNode">The other node.</param>
        /// <returns>True, iff the given node is dominating the other node.</returns>
        public bool Dominates(CFG.Node dominator, CFG.Node cfgNode) =>
            IsDominatedBy(cfgNode, dominator);

        /// <summary>
        /// Returns the first dominator of the given node.
        /// This might be the node itself iff there are no other
        /// dominators.
        /// </summary>
        /// <param name="cfgNode">The node.</param>
        /// <returns>The first dominator.</returns>
        public CFG.Node GetImmediateDominator(CFG.Node cfgNode)
        {
            Debug.Assert(cfgNode != null, "Invalid CFG node");
            var rpoNumber = idomsInRPO[cfgNode.NodeIndex];
            return nodesInRPO[rpoNumber];
        }

        /// <summary>
        /// Returns the first dominator of the given node.
        /// This might be the node itself iff there are no other
        /// dominators.
        /// </summary>
        /// <param name="first">The first node.</param>
        /// <param name="second">The first node.</param>
        /// <returns>The first dominator.</returns>
        public CFG.Node GetImmediateCommonDominator(CFG.Node first, CFG.Node second)
        {
            Debug.Assert(first != null, "Invalid first CFG node");
            Debug.Assert(second != null, "Invalid second CFG node");

            if (first == second)
                return first;

            var left = first.NodeIndex;
            var right = second.NodeIndex;
            var idom = Intersect(left, right);
            return nodesInRPO[idom];
        }

        #endregion
    }
}
