// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXBlockScheduling.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.PTX.Analyses;
using ILGPU.IR;
using ILGPU.IR.Transformations;
using ILGPU.IR.Values;

namespace ILGPU.Backends.PTX.Transformations
{
    /// <summary>
    /// Adapts the actual block branch order in a way to avoid negated predicated
    /// branches and which maximizes the number of implicit block branches.
    /// </summary>
    sealed class PTXBlockScheduling : UnorderedTransformation
    {
        /// <summary>
        /// Applies the PTX-specific block schedule to the given builder.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            // We change the control-flow structure during the transformation but
            // need to get information about previous predecessors and successors
            builder.AcceptControlFlowUpdates(accept: true);

            // Compute the intended block scheduling order and assign all blocks to their
            // traversal index
            var schedule = builder.SourceBlocks.CreatePTXScheduleToOptimize();

            // Ensure that all conditional if branches (which can fall through in the
            // true case) will be inverted to have "optimistic" branch checks in the
            // false case. This circumvents several limitations in the current PTX
            // assembler implementation.
            foreach (var block in schedule)
            {
                if (!(block.Terminator is IfBranch ifBranch))
                    continue;

                // Ensure that all branches are not inverted at this point
                block.Assert(!ifBranch.IsInverted);

                // Ignore if branches that can jump to arbitrary locations and branches
                // that are already in the desired state (with respect to the other false
                // branch target).
                if (!schedule.IsImplicitSuccessor(block, ifBranch.TrueTarget))
                    continue;

                // Invert the current if such that the false target becomes the true
                // branch target and the condition is properly inverted to avoid the
                // generation of negated predicate checks
                ifBranch.Invert(builder[block]);
            }

            return true;
        }
    }
}
