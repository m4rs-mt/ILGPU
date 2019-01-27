// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: SimplyControlFlow.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Merges multiple sequential branches (a call/branch chain) into a single block.
    /// </summary>
    public sealed class SimplifyControlFlow : UnorderedTransformation
    {
        /// <summary>
        /// Constructs a new transformation to merge sequential jump chains.
        /// </summary>
        public SimplifyControlFlow() { }

        /// <summary cref="UnorderedTransformation.PerformTransformation(Method.Builder)"/>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var scope = builder.CreateScope();
            var cfg = scope.CreateCFG();

            bool result = false;

            var mergedNodes = new HashSet<CFG.Node>();
            foreach (var cfgNode in cfg)
            {
                if (mergedNodes.Contains(cfgNode))
                    continue;

                result |= MergeChain(builder, cfgNode, cfg, mergedNodes);
            }

            return result;
        }

        /// <summary>
        /// Tries to merge a sequence of jumps.
        /// </summary>
        /// <param name="builder">The current method builder.</param>
        /// <param name="rootNode">The block where to start merging.</param>
        /// <param name="cfg">The current CFG.</param>
        /// <param name="mergedNodes">The collection of merged nodes.</param>
        /// <returns>True, if something could be merged.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MergeChain(
            Method.Builder builder,
            CFG.Node rootNode,
            CFG cfg,
            HashSet<CFG.Node> mergedNodes)
        {
            if (rootNode.NumSuccessors != 1)
                return false;

            var rootBlockBuilder = builder[rootNode.Block];
            var successors = rootNode.Successors;
            bool result = false;

            do
            {
                var nextBlock = successors[0];

                // We cannot merge jump targets in div. control flow
                if (nextBlock.NumPredecessors > 1)
                    break;

                mergedNodes.Add(nextBlock);
                successors = nextBlock.Successors;
                rootBlockBuilder.MergeBlock(nextBlock.Block);
                result = true;
            }
            while (successors.Count == 1);

            return result;
        }
    }
}
