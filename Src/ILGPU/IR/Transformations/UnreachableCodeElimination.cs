// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: UnreachableCodeElimination.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Values;
using System.Collections.Immutable;
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
            public PhiArgumentRemapper(Scope scope)
            {
                Scope = scope;
            }

            /// <summary>
            /// Returns the associated scope.
            /// </summary>
            public Scope Scope { get; }

            /// <summary>
            /// Returns true if any of the given blocks is no longer in the current scope.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool CanRemap(ImmutableArray<BasicBlock> blocks)
            {
                foreach (var block in blocks)
                {
                    if (!Scope.Contains(block))
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Keeps the block mapping but returns false if this block has become unreachable.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryRemap(BasicBlock block, out BasicBlock newBlock)
            {
                newBlock = block;
                return Scope.Contains(block);
            }
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
            Rewriter.Add<PhiValue>((context, mapper, phiValue) =>
            {
                phiValue.RemapArguments(context.Builder, mapper);
            });
        }

        #endregion

        /// <summary>
        /// Constructs a new UCE transformation.
        /// </summary>
        public UnreachableCodeElimination() { }

        /// <summary cref="UnorderedTransformation.PerformTransformation(Method.Builder)"/>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var scope = builder.CreateScope();

            // Fold branch targets (if possible)
            bool updated = false;
            foreach (var block in scope)
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
            var updatedScope = builder.CreateScope();
            foreach (var block in scope)
            {
                if (!updatedScope.Contains(block))
                {
                    // Block is unreachable -> remove all operations
                    var blockBuilder = builder[block];
                    blockBuilder.Clear();
                }
            }

            // Update all phi values
            Rewriter.Rewrite(
                updatedScope,
                builder,
                new PhiArgumentRemapper(updatedScope));

            return true;
        }
    }
}
