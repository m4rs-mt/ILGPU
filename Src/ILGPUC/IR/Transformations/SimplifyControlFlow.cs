// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
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
    /// <param name="toMove">The temporary move collection to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void MergeChain(
        MethodTransform transform,
        BasicBlock root,
        ValueSet<Method, BasicBlock> visited,
        ValueSet<Method, BasicBlock> phiSources,
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
            // that are phi sources as this would break SSA form.
            if (nextBlock.Predecessors.Length > 1 ||
                visited.Contains(nextBlock) ||
                phiSources.Contains(nextBlock))
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

        // Move all values to target block
        var lastValue = root.LastValue;
        foreach (var basicBlock in toMove)
        {
            transform.AppendTo(lastValue, basicBlock.FirstValue);
            lastValue = basicBlock.LastValue ?? lastValue;
        }

        // Copy termination from the last merged block to root
        if (toMove.Count > 0)
        {
            var lastBlock = toMove[^1];
            var blockTransform = transform.GetBasicBlockTransform(root);

            if (lastValue is not null)
                blockTransform.UpdateLastFromChain(lastValue);

            // Copy termination from the last block in the chain to the root block
            lastBlock.CopyTerminationTo(blockTransform);

            // Tell the root block's transform to accept values that
            // RebuildAndMemoize creates in merged blocks' new blocks.
            // Without this, DemandRewriteBlock's same-block filter drops
            // merged values because they end up in bb_new, not root_new.
            foreach (var basicBlock in toMove)
            {
                var mergedNewBlock = transform
                    .GetBasicBlockTransform(basicBlock).BasicBlock;
                blockTransform.AddMergedBlock(mergedNewBlock);
            }
        }
    }

    /// <summary>
    /// Cleans up empty blocks by redirecting branches to their successors.
    /// </summary>
    /// <param name="transform">The current transform.</param>
    /// <param name="block">The block to potentially clean up.</param>
    /// <param name="phiSources">The set of blocks that are phi sources.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CleanupEmptyBlock(
        MethodTransform transform,
        BasicBlock block,
        ValueSet<Method, BasicBlock> phiSources)
    {
        // Ignore complex blocks and blocks that are associated with phi values.
        // This captures most of the cases that arise in practice anyway.
        if (phiSources.Contains(block) ||
            block.TerminationKind != BlockTerminationKind.Unconditional)
            return;

        // Remap all branches to this block to its successor instead
        var successor = block.Successors[0];
        transform.Replace(block, successor);
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
        var toMove = InlineList<BasicBlock>.Create(16);

        // First, merge chains of blocks
        foreach (var block in oldMethod.Blocks)
            MergeChain(transform, block, visited, phiSources, ref toMove);

        // Then, clean up empty blocks
        foreach (var block in toMove)
            CleanupEmptyBlock(transform, block, phiSources);
    }
}
