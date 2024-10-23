// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityBlockScheduling.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Transformations;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System.Linq;

namespace ILGPU.Backends.Velocity.Transformations
{
    /// <summary>
    /// Adapts the actual block branch order in a way to ensure that loop exits are
    /// visited after the main loop bodies.
    /// </summary>
    sealed class VelocityBlockScheduling : UnorderedTransformation
    {
        /// <summary>
        /// Applies a Velocity-specific block order.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            // We change the control-flow structure during the transformation but
            // need to get information about previous predecessors and successors
            builder.AcceptControlFlowUpdates(accept: true);

            // Compute all loops of this method
            var cfg = builder.SourceBlocks.CreateCFG();
            var loops = cfg.CreateLoops();

            loops.ProcessLoops(loop =>
            {
                // Compute a set of all exit blocks
                var exits = loop.Exits.ToHashSet();

                // Check all blocks leaving the loop
                foreach (var breaker in loop.Breakers)
                {
                    // If we hit an if branch and the false target is leaving the loop,
                    // we will have to negate the condition to ensure that the ordering
                    // visits the internal block first
                    if (breaker.Terminator is IfBranch ifBranch &&
                        exits.Contains(ifBranch.FalseTarget))
                    {
                        // Invert the current branch
                        ifBranch.Invert(builder[breaker]);
                    }
                    else if (breaker.Terminator is SwitchBranch switchBranch)
                    {
                        // Skip this case for now
                        throw switchBranch.Location.GetNotSupportedException(
                            ErrorMessages.NotSupportedILInstruction);
                    }
                }
            });

            return true;
        }
    }
}
