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
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DominanceOrder = ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// Implements a dominator analysis.
    /// </summary>
    /// <typeparam name="TDirection">The control-flow direction.</typeparam>
    public sealed class Dominators<TDirection>
        where TDirection : struct, IControlFlowDirection
    {
        #region Static

        /// <summary>
        /// Creates a new dominator analysis.
        /// </summary>
        /// <param name="cfg">The parent graph.</param>
        /// <returns>The created dominator analysis.</returns>
        public static Dominators<TDirection> Create(
            CFG<DominanceOrder, TDirection> cfg) =>
            new Dominators<TDirection>(cfg);

        #endregion

        #region Instance

        /// <summary>
        /// Stores all idoms in RPO.
        /// </summary>
        private readonly int[] idomsInRPO;

        /// <summary>
        /// Stores all blocks in RPO.
        /// </summary>
        private readonly BasicBlock[] nodesInRPO;

        /// <summary>
        /// Constructs the dominators for the given control-flow graph.
        /// </summary>
        /// <param name="cfg">The parent graph.</param>
        private Dominators(CFG<DominanceOrder, TDirection> cfg)
        {
            idomsInRPO = new int[cfg.Count];
            nodesInRPO = new BasicBlock[cfg.Count];
            CFG = cfg;
            Root = cfg.Root;

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
        /// Returns the underlying graph.
        /// </summary>
        public CFG<DominanceOrder, TDirection> CFG { get; }

        /// <summary>
        /// Returns the root block.
        /// </summary>
        public BasicBlock Root { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the given <paramref name="block"/> is dominated by the
        /// <paramref name="dominator"/>.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="dominator">The potential dominator.</param>
        /// <returns>True, if the given block is dominated by the dominator.</returns>
        public bool IsDominatedBy(BasicBlock block, BasicBlock dominator)
        {
            var left = CFG[block].TraversalIndex;
            var right = CFG[dominator].TraversalIndex;
            return Intersect(left, right) == right;
        }

        /// <summary>
        /// Returns true if the given <paramref name="dominator"/> is dominating the
        /// <paramref name="block"/>.
        /// </summary>
        /// <param name="dominator">The potential dominator.</param>
        /// <param name="block">The other block.</param>
        /// <returns>True, if the given block is dominating the other block.</returns>
        public bool Dominates(BasicBlock dominator, BasicBlock block) =>
            IsDominatedBy(block, dominator);

        /// <summary>
        /// Returns the first dominator of the given block. This might be the block
        /// itself if there are no other dominators.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <returns>The first dominator.</returns>
        public BasicBlock GetImmediateDominator(BasicBlock block)
        {
            var rpoNumber = idomsInRPO[CFG[block].TraversalIndex];
            return nodesInRPO[rpoNumber];
        }

        /// <summary>
        /// Returns the immediate common dominator of both blocks.
        /// </summary>
        /// <param name="first">The first block.</param>
        /// <param name="second">The second block.</param>
        /// <returns>The immediate common dominator of both blocks.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BasicBlock GetImmediateCommonDominator(
            BasicBlock first,
            BasicBlock second)
        {
            if (first == second)
                return first;

            var left = CFG[first].TraversalIndex;
            var right = CFG[second].TraversalIndex;
            var idom = Intersect(left, right);
            return nodesInRPO[idom];
        }

        /// <summary>
        /// Returns the immediate common dominator of all blocks.
        /// </summary>
        /// <param name="blocks">The list of block.</param>
        /// <returns>The immediate common dominator of all blocks.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BasicBlock GetImmediateCommonDominator(ReadOnlySpan<BasicBlock> blocks)
        {
            if (blocks.Length < 1)
                throw new ArgumentOutOfRangeException(nameof(blocks));
            var result = blocks[0];
            for (int i = 1, e = blocks.Length; i < e; ++i)
                result = GetImmediateCommonDominator(result, blocks[i]);
            return result;
        }

        #endregion
    }

    /// <summary>
    /// Helper utility for the class <see cref="Dominators{TDirection}"/>
    /// </summary>
    public static class Dominators
    {
        /// <summary>
        /// Creates a new dominator analysis.
        /// </summary>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="cfg">The parent graph.</param>
        /// <returns>The created dominator analysis.</returns>
        public static Dominators<TDirection> CreateDominators<TDirection>(
            this CFG<DominanceOrder, TDirection> cfg)
            where TDirection : struct, IControlFlowDirection =>
            Dominators<TDirection>.Create(cfg);

        /// <summary>
        /// Creates a new dominator analysis.
        /// </summary>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="blocks">The source blocks.</param>
        /// <returns>The created dominator analysis.</returns>
        public static Dominators<TDirection> CreateDominators<TDirection>(
            this BasicBlockCollection<DominanceOrder, TDirection> blocks)
            where TDirection : struct, IControlFlowDirection =>
            blocks.CreateCFG().CreateDominators();

        /// <summary>
        /// Creates a new post dominator analysis.
        /// </summary>
        /// <param name="blocks">The source blocks.</param>
        /// <returns>The created post dominator analysis.</returns>
        public static Dominators<Backwards> CreatePostDominators(
            this BasicBlockCollection<DominanceOrder, Forwards> blocks) =>
            blocks.ChangeDirection<Backwards>().CreateDominators();
    }
}
