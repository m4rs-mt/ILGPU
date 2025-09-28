// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Dominators.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DominanceOrder = ILGPUC.IR.Analyses.ReversePostOrder<
    ILGPUC.IR.MethodValues.BasicBlock>;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Implements a dominator analysis.
/// </summary>
/// <typeparam name="TDirection">The control-flow direction.</typeparam>
sealed class Dominators<TDirection>
    where TDirection : struct, IControlFlowDirection
{
    #region Static

    /// <summary>
    /// Creates a new dominator analysis.
    /// </summary>
    /// <param name="cfg">The parent graph.</param>
    /// <returns>The created dominator analysis.</returns>
    public static Dominators<TDirection> Create(CFG<DominanceOrder, TDirection> cfg) =>
        new(cfg);

    #endregion

    #region Instance

    /// <summary>
    /// Stores all idoms in RPO.
    /// </summary>
    private readonly int[] _idomsInRPO;

    /// <summary>
    /// Stores all blocks in RPO.
    /// </summary>
    private readonly BasicBlock[] _nodesInRPO;

    /// <summary>
    /// Constructs the dominators for the given control-flow graph.
    /// </summary>
    /// <param name="cfg">The parent graph.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Dominators(CFG<DominanceOrder, TDirection> cfg)
    {
        _idomsInRPO = new int[cfg.Count];
        _nodesInRPO = new BasicBlock[cfg.Count];
        CFG = cfg;
        Root = cfg.Root;

        _idomsInRPO[0] = 0;
        for (int i = 1, e = _idomsInRPO.Length; i < e; ++i)
            _idomsInRPO[i] = -1;

        bool changed;
        do
        {
            changed = false;
            var enumerator = cfg.GetEnumerator();
            enumerator.MoveNext();
            var node = enumerator.Current;
            _nodesInRPO[node.TraversalIndex] = node;

            while (enumerator.MoveNext())
            {
                node = enumerator.Current;
                _nodesInRPO[node.TraversalIndex] = node;
                int currentIdom = -1;
                foreach (var pred in node.Predecessors)
                {
                    var predRPO = pred.TraversalIndex;
                    if (_idomsInRPO[predRPO] != -1)
                    {
                        currentIdom = predRPO;
                        break;
                    }
                }

                Debug.Assert(currentIdom != -1, "Invalid idom");
                foreach (var pred in node.Predecessors)
                {
                    var predRPO = pred.TraversalIndex;
                    if (_idomsInRPO[predRPO] != -1)
                        currentIdom = Intersect(currentIdom, predRPO);
                }

                var rpoNumber = node.TraversalIndex;
                if (_idomsInRPO[rpoNumber] != currentIdom)
                {
                    _idomsInRPO[rpoNumber] = currentIdom;
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
                right = _idomsInRPO[right];
            while (right < left)
                left = _idomsInRPO[left];
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BasicBlock GetImmediateDominator(BasicBlock block)
    {
        var rpoNumber = _idomsInRPO[CFG[block].TraversalIndex];
        return _nodesInRPO[rpoNumber];
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
        return _nodesInRPO[idom];
    }

    /// <summary>
    /// Returns the immediate common dominator of all blocks.
    /// </summary>
    /// <param name="blocks">The list of block.</param>
    /// <returns>The immediate common dominator of all blocks.</returns>
    public BasicBlock GetImmediateCommonDominator(ReadOnlySpan<BasicBlock> blocks)
    {
        if (blocks.Length < 1)
            throw new ArgumentOutOfRangeException(nameof(blocks));
        var result = blocks[0];
        for (int i = 1, e = blocks.Length; i < e; ++i)
            result = GetImmediateCommonDominator(result, blocks[i]);
        return result;
    }

    /// <summary>
    /// Determines a dominator block of all phi-value uses.
    /// </summary>
    /// <param name="dominatorBlock">The current dominator block.</param>
    /// <param name="uses">The collection of all uses.</param>
    /// <returns>The dominator block given by all phi-value uses.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private BasicBlock GetPhiParent(BasicBlock dominatorBlock, UseCollection uses)
    {
        // Check for phi-value references
        BasicBlock? phiParent = null;
        foreach (Use use in uses)
        {
            if (use.Target is not PhiValue phiValue)
                continue;

            // If we encounter a phi value we have to check whether our value occurs
            // in one of the predecessors. In this case, we have to adjust our
            // current dominator.
            var newParent = phiValue.Sources[use.Index];
            phiParent = phiParent == null
                ? newParent
                : GetImmediateCommonDominator(phiParent, newParent);
        }

        // If we have found a new parent dominator block for all phi values, use this
        // block instead of the given dominator
        return phiParent ?? dominatorBlock;
    }

    /// <summary>
    /// Gets the immediate common dominator of all given uses.
    /// </summary>
    /// <param name="dominatorBlock">The initial dominator block.</param>
    /// <param name="uses">The uses to get the common dominator for.</param>
    /// <returns>The common dominator block of all uses.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public BasicBlock GetImmediateCommonDominatorOfUses(
        BasicBlock dominatorBlock,
        UseCollection uses)
    {
        // Check for phi-value uses
        dominatorBlock = GetPhiParent(dominatorBlock, uses);

        // Initialize the parent dominator block using the given uses
        foreach (Use use in uses)
        {
            Value value = use;
            if (value is PhiValue || value is not BasicBlockValue bbValue)
                continue;

            // Get the immediate common dominator of the current dominator block and
            // the block of the use reference
            var valueBlock = bbValue.BasicBlock;
            dominatorBlock = GetImmediateCommonDominator(dominatorBlock, valueBlock);
        }
        return dominatorBlock;
    }

    #endregion
}

/// <summary>
/// Helper utility for the class <see cref="Dominators{TDirection}"/>
/// </summary>
static class Dominators
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
