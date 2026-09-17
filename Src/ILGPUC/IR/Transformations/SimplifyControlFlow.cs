// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SimplifyControlFlow.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Simplifies control flow by merging multiple sequential branches (a call/branch
/// chain) into a single block and removing empty blocks that are not phi sources.
/// </summary>
/// <remarks>
/// This transformation combines two optimizations:
/// 1. Merging chains of blocks with single successors/predecessors
/// 2. Cleaning up empty blocks that only contain unconditional branches
/// Both optimizations respect phi value sources to avoid breaking SSA form.
/// </remarks>
sealed class SimplifyControlFlow(TransformationArgs args) : Transformation(args)
{
    /// <summary>
    /// Tries to merge a sequence of jumps.
    /// </summary>
    /// <param name="transform">The current transform.</param>
    /// <param name="root">The block where to start merging.</param>
    /// <param name="visited">The collection of visited nodes.</param>
    /// <param name="phiSources">The set of blocks that are phi sources.</param>
    /// <param name="merged">
    /// The set of blocks that took part in a merge, as root or as merged block.
    /// </param>
    /// <param name="chainRoots">
    /// Maps the last block of each merged chain to the root that took over its
    /// termination.
    /// </param>
    /// <param name="toMove">The temporary move collection to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void MergeChain(
        MethodTransform transform,
        BasicBlock root,
        ValueSet<Method, BasicBlock> visited,
        ValueSet<Method, BasicBlock> phiSources,
        ValueSet<Method, BasicBlock> merged,
        ValueMap<Method, BasicBlock, BasicBlock> chainRoots,
        ref InlineList<BasicBlock> toMove)
    {
        if (root.Successors.Length != 1 || visited.Contains(root))
            return;

        // Mark node as seen
        visited.Add(root);
        toMove.Clear();

        // Init initial builder and successors list
        var successors = root.Successors;
        do
        {
            var nextBlock = successors[0];

            // We cannot merge jump targets in div. control-flow or in the case
            // of a block that we have already seen. We also cannot merge blocks
            // that are phi sources as this would break SSA form. Neither can we
            // merge blocks with phis: they are left behind when a constant
            // branch into a join block is folded, and moving them would place
            // phis in the middle of the merged block.
            if (nextBlock.Predecessors.Length > 1 ||
                visited.Contains(nextBlock) ||
                phiSources.Contains(nextBlock) ||
                nextBlock.FirstPhiValue is not null)
            {
                break;
            }

            // Mark next block as seen
            visited.Add(nextBlock);

            // Merge block
            toMove.Add(nextBlock);
            successors = nextBlock.Successors;
        }
        while (successors.Length == 1);

        if (toMove.Count < 1)
            return;

        // Move all values to target block
        var lastValue = root.LastValue;
        foreach (var basicBlock in toMove)
        {
            transform.AppendTo(lastValue, basicBlock.FirstValue);
            lastValue = basicBlock.LastValue ?? lastValue;
        }

        // Copy termination from the last merged block to root
        var lastBlock = toMove[^1];
        var blockTransform = transform.GetBasicBlockTransform(root);

        if (lastValue is not null)
            blockTransform.UpdateLastFromChain(lastValue);

        // Copy termination from the last block in the chain to the root block
        lastBlock.CopyTerminationTo(blockTransform);
        chainRoots.Add(lastBlock, root);
        merged.Add(root);

        // Tell the root block's transform to accept values that
        // RebuildAndMemoize creates in merged blocks' new blocks.
        // Without this, DemandRewriteBlock's same-block filter drops
        // merged values because they end up in bb_new, not root_new.
        foreach (var basicBlock in toMove)
        {
            merged.Add(basicBlock);
            var mergedNewBlock = transform
                .GetBasicBlockTransform(basicBlock).BasicBlock;
            blockTransform.AddMergedBlock(mergedNewBlock);
        }
    }

    /// <summary>
    /// Returns true if the given block is an empty block that only forwards to
    /// its single successor and can be removed by branching to the successor
    /// directly.
    /// </summary>
    /// <param name="block">The block to test.</param>
    /// <param name="entryBlock">The entry block of the method.</param>
    /// <param name="phiSources">The set of blocks that are phi sources.</param>
    /// <param name="merged">The set of blocks that took part in a merge.</param>
    /// <remarks>
    /// Blocks that are phi sources are kept, as their predecessors would have to
    /// become phi sources in their place. The successor must not have phis
    /// either: a block forwarding to a block with phis is always a phi source,
    /// and a predecessor redirected to it would lack a phi argument otherwise.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsRemovableEmptyBlock(
        BasicBlock block,
        BasicBlock entryBlock,
        ValueSet<Method, BasicBlock> phiSources,
        ValueSet<Method, BasicBlock> merged) =>
        block != entryBlock &&
        block.TerminationKind == BlockTerminationKind.Unconditional &&
        block.FirstValue is null &&
        block.FirstPhiValue is null &&
        !phiSources.Contains(block) &&
        !merged.Contains(block) &&
        block.Successors[0] != block &&
        block.Successors[0].FirstPhiValue is null;

    /// <summary>
    /// Removes empty forwarding blocks by redirecting all branches to them to
    /// their successors.
    /// </summary>
    /// <param name="transform">The current transform.</param>
    /// <param name="phiSources">The set of blocks that are phi sources.</param>
    /// <param name="merged">The set of blocks that took part in a merge.</param>
    /// <param name="chainRoots">
    /// Maps the last block of each merged chain to its root.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CleanupEmptyBlocks(
        MethodTransform transform,
        ValueSet<Method, BasicBlock> phiSources,
        ValueSet<Method, BasicBlock> merged,
        ValueMap<Method, BasicBlock, BasicBlock> chainRoots)
    {
        var oldMethod = transform.OldMethod;
        var entryBlock = oldMethod.EntryBlock;
        var removable = oldMethod.CreateSet<BasicBlock>();
        foreach (var block in oldMethod.Blocks)
        {
            if (IsRemovableEmptyBlock(block, entryBlock, phiSources, merged))
                removable.Add(block);
        }
        if (removable.Count < 1)
            return;

        // Keep blocks on cycles that consist of removable blocks only: such an
        // empty endless loop has no block left to forward to
        var cycleCheck = oldMethod.CreateSet<BasicBlock>();
        var toRemove = InlineList<BasicBlock>.Create(removable.Count);
        var toKeep = InlineList<BasicBlock>.Create(4);
        foreach (var block in oldMethod.Blocks)
        {
            if (!removable.Contains(block))
                continue;

            cycleCheck.Clear();
            var current = block;
            while (removable.Contains(current) && cycleCheck.Add(current))
                current = current.Successors[0];
            if (removable.Contains(current))
                toKeep.Add(block);
            else
                toRemove.Add(block);
        }
        foreach (var block in toKeep)
            removable.Remove(block);
        if (toRemove.Count < 1)
            return;

        // Resolve all references to removed blocks to their successors. The
        // successor is an old block, so chains of removed blocks resolve to the
        // first block that is kept.
        foreach (var block in toRemove)
            transform.Replace(block, block.Successors[0]);

        // Rebuild the terminations of all predecessors to pick up the new
        // targets. The termination of the last block of a merged chain lives in
        // its root. Conditional branches whose targets become the same block
        // fold to unconditional branches.
        var updated = oldMethod.CreateSet<BasicBlock>();
        foreach (var block in toRemove)
        {
            foreach (var predecessor in block.Predecessors)
            {
                if (removable.Contains(predecessor) || !updated.Add(predecessor))
                    continue;

                var target = chainRoots.TryGetValue(predecessor, out var root)
                    ? root
                    : predecessor;
                predecessor.CopyTerminationTo(transform.GetBasicBlockTransform(target));
            }
        }
    }

    /// <summary>
    /// Applies the control-flow simplification transformation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    protected override void OnTransform(MethodTransform transform)
    {
        base.OnTransform(transform);

        var oldMethod = transform.OldMethod;
        var phiSources = oldMethod.Blocks.ComputePhiSources();
        var visited = oldMethod.CreateSet<BasicBlock>();
        var merged = oldMethod.CreateSet<BasicBlock>();
        var chainRoots = oldMethod.CreateMap<BasicBlock, BasicBlock>();
        var toMove = InlineList<BasicBlock>.Create(16);

        // First, merge chains of blocks
        foreach (var block in oldMethod.Blocks)
        {
            MergeChain(
                transform,
                block,
                visited,
                phiSources,
                merged,
                chainRoots,
                ref toMove);
        }

        // Then, remove empty blocks that only forward to their successors
        CleanupEmptyBlocks(transform, phiSources, merged, chainRoots);
    }
}
