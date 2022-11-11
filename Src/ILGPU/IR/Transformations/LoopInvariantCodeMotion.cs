// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: LoopInvariantCodeMotion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Loop = ILGPU.IR.Analyses.Loops<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>.Node;
using Loops = ILGPU.IR.Analyses.Loops<
    ILGPU.IR.Analyses.TraversalOrders.ReversePostOrder,
    ILGPU.IR.Analyses.ControlFlowDirection.Forwards>;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Moves loop-invariant code pieces out of loops.
    /// </summary>
    public sealed class LoopInvariantCodeMotion : UnorderedTransformation
    {
        #region Nested Types

        /// <summary>
        /// Manages knowledge loop invariance.
        /// </summary>
        private readonly struct LoopInvariance
        {
            #region Instance

            private readonly Dictionary<Value, bool> mapping;

            public LoopInvariance(Loop loop)
            {
                mapping = new Dictionary<Value, bool>(loop.Count << 1);
                Loop = loop;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent loop.
            /// </summary>
            public Loop Loop { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Registers the given value as not invariant and returns false.
            /// </summary>
            /// <param name="value">The value to mark.</param>
            /// <returns>False.</returns>
            private readonly bool ReturnNotInvariant(Value value)
            {
                mapping.Add(value, false);
                return false;
            }

            /// <summary>
            /// Returns true if the given value is loop invariant.
            /// </summary>
            /// <param name="value">The value to test.</param>
            /// <returns>True, if the given value is loop invariant.</returns>
            public readonly bool IsLoopInvariant(Value value)
            {
                if (mapping.TryGetValue(value, out bool invariant))
                    return invariant;
                switch (value)
                {
                    case PhiValue phiValue:
                        // Phi values with additional loop-specific dependencies cannot
                        // be moved out of loops
                        foreach (var source in phiValue.Sources)
                        {
                            if (Loop.Contains(source))
                                return ReturnNotInvariant(phiValue);
                        }
                        break;
                    case SideEffectValue _:
                        // Values with side effects cannot be moved out of loops
                        return false;
                    default:
                        // Values with associated dependencies can be moved out of loops
                        // if and only if all of its dependencies are loop invariant
                        foreach (Value node in value)
                        {
                            if (!IsLoopInvariant(node))
                                return ReturnNotInvariant(value);
                        }
                        break;
                }
                mapping.Add(value, true);
                return true;
            }

            #endregion
        }

        /// <summary>
        /// A helper structure to move values around
        /// </summary>
        private struct Mover
        {
            private readonly HashSet<Value> visited;
            private InlineList<Value> toMove;

            /// <summary>
            /// Initializes a new mover.
            /// </summary>
            /// <param name="numBlocks">The number of blocks of the parent loop.</param>
            public Mover(int numBlocks)
            {
                visited = new HashSet<Value>();
                toMove = InlineList<Value>.Create(numBlocks << 1);
            }

            /// <summary>
            /// Registers the given value to be moved later.
            /// </summary>
            /// <param name="value">The value to be moved.</param>
            public void Add(Value value)
            {
                visited.Add(value);
                toMove.Add(value);
            }

            /// <summary>
            /// Returns a span to iterate over all values to be moved.
            /// </summary>
            public readonly ReadOnlySpan<Value> ToMove => toMove.AsReadOnlySpan();

            /// <summary>
            /// Checks whether given value should be actually moved out of the loop.
            /// </summary>
            /// <param name="value">The value to check.</param>
            /// <returns>True, if the given value should be moved.</returns>
            public readonly bool ShouldBeMoved(Value value)
            {
                // Check whether the given value is scheduled to be moved
                if (!visited.Contains(value))
                    return false;

                // Check whether this value is a constant and should not be moved out of
                // the loop due to direct dependencies which will not be moved out of the
                // current loop
                if (value is ConstantNode)
                {
                    foreach (Value use in value.Uses)
                    {
                        if (ShouldBeMoved(use))
                            return true;
                    }
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Applies the LICM transformation to all loops.
        /// </summary>
        private struct LoopProcessor : Loops.ILoopProcessor
        {
            public LoopProcessor(Method.Builder builder)
            {
                Builder = builder;
                Applied = false;
            }

            /// <summary>
            /// Returns the parent method builder.
            /// </summary>
            public Method.Builder Builder { get; }

            /// <summary>
            /// Returns true if the loop processor could be applied.
            /// </summary>
            public bool Applied { get; private set; }

            /// <summary>
            /// Applies the LICM transformation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Process(Loop loop)
            {
                if (loop.Entries.Length > 1)
                    return;
                Applied |= ApplyLICM(Builder, loop);
            }
        }

        #endregion

        #region Static

        /// <summary>
        /// Applies the LICM transformation to the given loop.
        /// </summary>
        /// <param name="builder">The parent method builder.</param>
        /// <param name="loop">The current loop.</param>
        /// <returns>True, if the transformation could be applied.</returns>
        private static bool ApplyLICM(Method.Builder builder, Loop loop)
        {
            // Initialize the current loop invariance cache
            var invariance = new LoopInvariance(loop);

            // Ensure that all blocks are in the correct order
            BasicBlockCollection<ReversePostOrder, Forwards> blocks =
                loop.ComputeOrderedBlocks(0);

            // Get the initial entry builder to move all values to
            var entry = loop.Entries[0];
            var mover = new Mover(blocks.Count);

            // Traverse all blocks in reverse post order to move all values without
            // violating any SSA properties
            foreach (var block in blocks)
            {
                if (block == entry)
                    continue;
                foreach (Value value in block)
                {
                    // Move out of the loop if this value is loop invariant
                    if (invariance.IsLoopInvariant(value))
                        mover.Add(value);
                }
            }

            // Move values
            var entryBuilder = builder[entry];
            foreach (var valueToMove in mover.ToMove)
            {
                // Check whether this value should be moved out of the current loop
                if (mover.ShouldBeMoved(valueToMove))
                    entryBuilder.AddFromOtherBlock(valueToMove);
            }

            return mover.ToMove.Length > 0;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Applies the LICM transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var cfg = builder.SourceBlocks.CreateCFG();
            var loops = cfg.CreateLoops();

            // We change the control-flow structure during the transformation but
            // need to get information about previous predecessors and successors
            builder.AcceptControlFlowUpdates(accept: true);

            return loops.ProcessLoops(new LoopProcessor(builder)).Applied;
        }

        #endregion
    }
}
