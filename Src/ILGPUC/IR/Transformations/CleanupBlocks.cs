// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CleanupBlocks.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.Values;
using System;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Cleans up empty blocks.
/// </summary>
/// <remarks>
/// TODO: this transformation should become much more aggressive by cloning values
/// into predecessor blocks to reduce the number of branches.
/// </remarks>
sealed class CleanupBlocks : UnorderedTransformation
{
    #region Nested Types

    /// <summary>
    /// Remaps source to target blocks.
    /// </summary>
    /// <remarks>
    /// Constructs a new remapper.
    /// </remarks>
    private readonly struct Remapper(BasicBlock source, BasicBlock target) :
        TerminatorValue.ITargetRemapper
    {
        /// <summary>
        /// Returns the source block.
        /// </summary>
        public BasicBlock Source { get; } = source;

        /// <summary>
        /// Returns the new target block.
        /// </summary>
        public BasicBlock Target { get; } = target;

        /// <summary>
        /// Returns true if the given block span contains the source block.
        /// </summary>
        public readonly bool CanRemap(in ReadOnlySpan<BasicBlock> blocks) =>
            blocks.Contains(Source, new BasicBlock.Comparer());

        /// <summary>
        /// Remaps the source block to the new target block.
        /// </summary>
        public readonly BasicBlock Remap(BasicBlock block) =>
            block == Source ? Target : block;
    }

    #endregion

    #region Static

    /// <summary>
    /// Returns true if the conditional branch has identical branch targets in all
    /// cases.
    /// </summary>
    /// <param name="conditionalBranch">The conditional branch.</param>
    /// <param name="target">The main target.</param>
    /// <returns>True, if all targets are identical.</returns>
    private static bool HasSameTargets(
        ConditionalBranch conditionalBranch,
        out BasicBlock target)
    {
        var targets = conditionalBranch.Targets;
        target = targets[0];
        for (int i = 1, e = targets.Length; i < e; ++i)
        {
            if (target != targets[i])
                return false;
        }
        return true;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Applies the cleanup transformation.
    /// </summary>
    protected override bool PerformTransformation(Method.Builder builder)
    {
        // We change the control-flow structure during the transformation but
        // need to get information about previous predecessors and successors
        builder.AcceptControlFlowUpdates(accept: true);

        var blocks = builder.SourceBlocks;
        var phiSources = blocks.ComputePhiSources();

        // Merge all empty blocks into their associated predecessors
        bool updated = false;
        foreach (var block in builder.SourceBlocks)
        {
            // Ignore complex blocks for now and ignore blocks that are associated
            // with phi values. This captures most of the cases that arise in
            // practice anyway.
            if (block.Count > 0 || phiSources.Contains(block) ||
                !(block.Terminator is UnconditionalBranch))
            {
                continue;
            }

            // Remap all branches to this block to its successor instead
            var successor = block.Successors[0];
            foreach (var pred in block.Predecessors)
            {
                pred.Terminator.AsNotNull().RemapTargets(
                    builder[pred],
                    new Remapper(block, successor));
            }
            updated = true;
        }

        if (!updated)
            return false;

        // Update all conditional branches that might have become obsolete
        foreach (var block in blocks)
        {
            if (!(block.Terminator is ConditionalBranch conditionalBranch) ||
                !HasSameTargets(conditionalBranch, out var target))
            {
                continue;
            }

            // Simplify to an unconditional branch
            builder[block].CreateBranch(conditionalBranch.Location, target);
        }

        return true;
    }

    #endregion
}
