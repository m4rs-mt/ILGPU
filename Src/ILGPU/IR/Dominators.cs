// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Dominators.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ILGPU.IR
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
        public static Dominators Create(CFG cfg)
        {
            return new Dominators(cfg);
        }

        /// <summary>
        /// Creates a dominator analysis in a separate task.
        /// </summary>
        /// <param name="cfg">The control flow graph.</param>
        public static Task<Dominators> CreateAsync(CFG cfg)
        {
            return Task.Run(() => Create(cfg));
        }

        #endregion

        #region Instance

        /// <summary>
        /// Stores all idoms in RPO.
        /// </summary>
        private readonly int[] idomsInRPO;

        /// <summary>
        /// Stores all CFG nodes in RPO.
        /// </summary>
        private readonly CFGNode[] nodesInRPO;

        /// <summary>
        /// Constructs the dominators for the given control flow graph.
        /// </summary>
        /// <param name="cfg">The control flow graph.</param>
        private Dominators(CFG cfg)
        {
            CFG = cfg ?? throw new ArgumentNullException(nameof(cfg));
            Context = cfg.Context;

            idomsInRPO = new int[cfg.Count];
            nodesInRPO = new CFGNode[cfg.Count];

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
                    nodesInRPO[node.RPONumber] = node;

                    while (enumerator.MoveNext())
                    {
                        node = enumerator.Current;
                        nodesInRPO[node.RPONumber] = node;
                        int currentIdom = -1;
                        foreach (var pred in node.Predecessors)
                        {
                            var predRPO = pred.RPONumber;
                            if (idomsInRPO[predRPO] != -1)
                            {
                                currentIdom = predRPO;
                                break;
                            }
                        }

                        Debug.Assert(currentIdom != -1, "Invalid idom");
                        foreach (var pred in node.Predecessors)
                        {
                            var predRPO = pred.RPONumber;
                            if (idomsInRPO[predRPO] != -1)
                                currentIdom = Intersect(currentIdom, predRPO);
                        }

                        var rpoNumber = node.RPONumber;
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
        public IRContext Context { get; }

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
        public bool IsDominatedBy(CFGNode cfgNode, CFGNode dominator)
        {
            Debug.Assert(cfgNode != null, "Invalid CFG node");
            Debug.Assert(dominator != null, "Invalid dominator");

            var left = cfgNode.RPONumber;
            var right = dominator.RPONumber;
            return Intersect(left, right) == right;
        }

        /// <summary>
        /// Returns true iff the given <paramref name="dominator"/> node. is
        /// dominating the <paramref name="cfgNode"/> node.
        /// </summary>
        /// <param name="dominator">The potential dominator.</param>
        /// <param name="cfgNode">The other node.</param>
        /// <returns>True, iff the given node is dominating the other node.</returns>
        public bool Dominates(CFGNode dominator, CFGNode cfgNode) =>
            IsDominatedBy(cfgNode, dominator);

        /// <summary>
        /// Returns the first dominator of the given node.
        /// This might be the node itself iff there are no other
        /// dominators.
        /// </summary>
        /// <param name="cfgNode">The node.</param>
        /// <returns>The first dominator.</returns>
        public CFGNode GetImmediateDominator(CFGNode cfgNode)
        {
            Debug.Assert(cfgNode != null, "Invalid CFG node");
            var rpoNumber = idomsInRPO[cfgNode.RPONumber];
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
        public CFGNode GetImmediateCommonDominator(CFGNode first, CFGNode second)
        {
            Debug.Assert(first != null, "Invalid first CFG node");
            Debug.Assert(second != null, "Invalid second CFG node");

            if (first == second)
                return first;

            var left = first.RPONumber;
            var right = second.RPONumber;
            var idom = Intersect(left, right);
            return nodesInRPO[idom];
        }

        #endregion
    }
}
