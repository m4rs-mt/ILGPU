// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: SimplyControlFlow.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Merges multiple sequential branches (a call/branch chain) into a single block.
    /// </summary>
    public sealed class SimplifyControlFlow : UnorderedTransformation
    {
        #region Static

        /// <summary>
        /// Tries to merge a sequence of jumps.
        /// </summary>
        /// <param name="builder">The current method builder.</param>
        /// <param name="root">The block where to start merging.</param>
        /// <param name="visited">The collection of visited nodes.</param>
        /// <returns>True, if something could be merged.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MergeChain(
            Method.Builder builder,
            BasicBlock root,
            ref BasicBlockSet visited)
        {
            if (root.Successors.Length != 1 || visited.Contains(root))
                return false;

            // Mark node as seen
            visited.Add(root);

            // Init initial builder and successors list
            var rootBlockBuilder = builder[root];
            var successors = root.Successors;
            bool result = false;

            do
            {
                var nextBlock = successors[0];

                // We cannot merge jump targets in div. control-flow or in the case
                // of a block that we have already seen
                if (nextBlock.Predecessors.Length > 1 || visited.Contains(nextBlock))
                    break;

                // Mark next block as seen
                visited.Add(nextBlock);

                // Merge block
                successors = nextBlock.Successors;
                rootBlockBuilder.MergeBlock(nextBlock);

                // Return true as we have changed the IR
                result = true;
            } while (successors.Length == 1);

            // Return the success status
            return result;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new transformation to merge sequential jump chains.
        /// </summary>
        public SimplifyControlFlow() { }

        #endregion

        #region Methods

        /// <summary>
        /// Applies the control-flow simplification transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            // We change the control-flow structure during the transformation but
            // need to get information about previous predecessors and successors
            builder.AcceptControlFlowUpdates(accept: true);

            BasicBlockCollection<ReversePostOrder, Forwards> blocks =
                builder.SourceBlocks;

            var visited = blocks.CreateSet();
            bool result = false;
            foreach (var block in blocks)
                result |= MergeChain(builder, block, ref visited);
            return result;
        }

        #endregion
    }
}
