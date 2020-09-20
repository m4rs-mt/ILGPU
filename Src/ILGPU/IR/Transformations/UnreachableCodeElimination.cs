// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: UnreachableCodeElimination.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents an UCE transformation.
    /// </summary>
    public sealed class UnreachableCodeElimination : UnorderedTransformation
    {
        #region Nested Types

        /// <summary>
        /// An argument remapper for Phi values.
        /// </summary>
        private readonly struct PhiArgumentRemapper : PhiValue.IArgumentRemapper
        {
            /// <summary>
            /// Initializes a new scope.
            /// </summary>
            public PhiArgumentRemapper(in HashSet<BasicBlock> blocks)
            {
                Blocks = blocks;
            }

            /// <summary>
            /// Returns the associated scope.
            /// </summary>
            private HashSet<BasicBlock> Blocks { get; }

            /// <summary>
            /// Returns true if the given block is reachable.
            /// </summary>
            /// <param name="block">The block to test.</param>
            /// <returns>True, if the given block is reachable.</returns>
            public readonly bool IsReachable(BasicBlock block) =>
                Blocks.Contains(block);

            /// <summary>
            /// Returns true if any of the given blocks is no longer in the current
            /// scope.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool CanRemap(in ReadOnlySpan<BasicBlock> blocks)
            {
                foreach (var block in blocks)
                {
                    if (!IsReachable(block))
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Keeps the block mapping but returns false if this block has become
            /// unreachable.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool TryRemap(BasicBlock block, out BasicBlock newBlock)
            {
                newBlock = block;
                return IsReachable(block);
            }

            /// <summary>
            /// Returns the value of <paramref name="value"/>.
            /// </summary>
            public readonly Value RemapValue(BasicBlock updatedBlock, Value value) =>
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
            if (!remapper.IsReachable(phiValue.BasicBlock))
                return;
            phiValue.RemapArguments(context.Builder, remapper);
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly Rewriter<PhiArgumentRemapper> Rewriter =
            new Rewriter<PhiArgumentRemapper>();

        /// <summary>
        /// Registers all rewriting patterns.
        /// </summary>
        static UnreachableCodeElimination()
        {
            Rewriter.Add<PhiValue>(Update);
        }

        #endregion

        /// <summary>
        /// Constructs a new UCE transformation.
        /// </summary>
        public UnreachableCodeElimination() { }

        /// <summary>
        /// Applies the UCE transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            // Fold branch targets (if possible)
            var blocks = builder.SourceBlocks;
            bool updated = false;
            foreach (var block in blocks)
            {
                // Get the conditional terminator
                var terminator = block.GetTerminatorAs<ConditionalBranch>();
                if (terminator == null || !terminator.CanFold)
                    continue;

                // Fold branch
                var blockBuilder = builder[block];
                terminator.Fold(blockBuilder);

                updated = true;
            }

            // Check for changes
            if (!updated)
                return false;

            // Find all unreachable blocks
            var updatedBlocks = builder.ComputeBlockCollection<PreOrder>()
                .ToSet();
            foreach (var block in builder.SourceBlocks)
            {
                if (!updatedBlocks.Contains(block))
                {
                    // Block is unreachable -> remove all operations
                    var blockBuilder = builder[block];
                    blockBuilder.Clear();
                }
            }

            // Update all phi values
            Rewriter.Rewrite(
                blocks,
                builder,
                new PhiArgumentRemapper(updatedBlocks));

            return true;
        }
    }
}
