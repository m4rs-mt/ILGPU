// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ConstantPropagation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using ILGPU.Util;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Applies aggressive constant propagation to all methods.
    /// </summary>
    public sealed class ConstantPropagation : UnorderedTransformation
    {
        /// <summary>
        /// Applies constant propagation to the given builder.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            // We change the control-flow structure during the transformation but
            // need to get information about previous predecessors and successors
            builder.AcceptControlFlowUpdates(accept: true);

            // Build an identity mapping
            var parameterValues = InlineList<ValueReference>.Create(builder.NumParams);
            for (int i = 0, e = builder.NumParams; i < e; ++i)
                parameterValues.Add(builder[i]);

            // Rebuild the whole function body enforcing all constants to be folded
            var rebuilder = builder.CreateRebuilder<IRRebuilder.InlineMode>(
                builder.Method.CreateParameterMapping(parameterValues),
                builder.SourceBlocks);

            // Create an exit return
            var (exitBlock, exitValue) = rebuilder.Rebuild();
            exitBlock.CreateReturn(exitValue.Location, exitValue);

            // Merge the entry block
            var entryBlock = builder.EntryBlockBuilder;
            entryBlock.Clear();
            entryBlock.MergeBlock(rebuilder.EntryBlock);

            return true;
        }
    }
}
