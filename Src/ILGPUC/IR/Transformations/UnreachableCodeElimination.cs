// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: UnreachableCodeElimination.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.Rewriting;
using ILGPUC.IR.Values;
using System;
using System.Collections.Generic;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Represents an UCE transformation.
/// </summary>
sealed class UnreachableCodeElimination : UnorderedTransformation
{
    #region Nested Types

    /// <summary>
    /// An argument remapper for Phi values.
    /// </summary>
    private readonly struct PhiArgumentRemapper(HashSet<BasicBlock> blocks) :
        PhiValue.IArgumentRemapper
    {
        /// <summary>
        /// Returns true if the given block is reachable.
        /// </summary>
        /// <param name="block">The block to test.</param>
        /// <returns>True, if the given block is reachable.</returns>
        public bool IsReachable(BasicBlock block) =>
            blocks.Contains(block);

        /// <summary>
        /// Returns true if the given block is reachable and the block is one of the
        /// predecessors of the specified parent source value block.
        /// </summary>
        /// <param name="sourceBlock">
        /// The parent source block to query the predecessors.
        /// </param>
        /// <param name="block">The block to test.</param>
        /// <returns>
        /// True, if the given block is reachable and the block is one the
        /// predecessors of the source block.
        /// </returns>
        private bool IsReachableAndPredecessor(
            BasicBlock sourceBlock,
            BasicBlock block) =>
                IsReachable(block) &&
                sourceBlock.Predecessors.Contains(
                    block,
                    new BasicBlock.Comparer());

        /// <summary>
        /// Returns true if any of the given blocks is no longer in the current
        /// scope.
        /// </summary>
        public bool CanRemap(PhiValue phiValue)
        {
            var sourceBlock = phiValue.BasicBlock;
            foreach (var block in phiValue.Sources)
            {
                if (!IsReachableAndPredecessor(sourceBlock, block))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Keeps the block mapping but returns false if this block has become
        /// unreachable.
        /// </summary>
        public bool TryRemap(PhiValue phiValue, BasicBlock block, out BasicBlock newBlock)
        {
            newBlock = block;
            return IsReachableAndPredecessor(phiValue.BasicBlock, block);
        }

        /// <summary>
        /// Returns the value of <paramref name="value"/>.
        /// </summary>
        public Value RemapValue(
            PhiValue phiValue,
            BasicBlock updatedBlock,
            Value value) =>
            value;
    }

    #endregion

    #region Rewriter Methods

    /// <summary>
    /// Updates reachable phi values that have references to unreachable parts.
    /// </summary>
    private static void Update(
        RewriterContext context,
        PhiArgumentRemapper remapper,
        PhiValue phiValue)
    {
        // Check whether this phi has become unreachable
        if (!remapper.IsReachable(phiValue.BasicBlock))
            return;

        // Check whether this phi has been replaced due to a previous simplification
        if (phiValue.IsReplaced)
            return;

        // Remap all arguments and simplify phi values recursively
        phiValue.RemapArguments(context.GetMethodBuilder(), remapper);
    }

    #endregion

    #region Rewriter

    /// <summary>
    /// The internal rewriter.
    /// </summary>
    private static readonly Rewriter<PhiArgumentRemapper> Rewriter = new();

    /// <summary>
    /// Registers all rewriting patterns.
    /// </summary>
    static UnreachableCodeElimination()
    {
        Rewriter.Add<PhiValue>(Update);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Applies the UCE transformation.
    /// </summary>
    protected override void PerformTransformation(
        IRContext context,
        Method.Builder builder)
    {
        // Fold branch targets (if possible)
        var blocks = builder.SourceBlocks;
        bool updated = false;
        foreach (var block in blocks)
        {
            // Get the conditional terminator
            if (!(block.Terminator is ConditionalBranch branch) || !branch.CanFold)
                continue;

            // Fold branch
            var blockBuilder = builder[block];
            branch.Fold(blockBuilder);

            updated = true;
        }

        // Check for changes and update blocks (if required)
        if (!updated)
            return;

        // Update the internal control-flow structure and compute the set of all
        // remaining reachable basic blocks
        var updatedBlocks = builder.UpdateControlFlow().ToSet();

        // Update all phi values
        Rewriter.Rewrite(
            blocks,
            builder,
            new PhiArgumentRemapper(updatedBlocks));

        // Find all unreachable blocks and remove them
        foreach (var block in blocks)
        {
            if (!updatedBlocks.Contains(block))
            {
                // Block is unreachable -> remove all operations
                var blockBuilder = builder[block];
                blockBuilder.Clear();
            }
        }
    }

    #endregion
}
