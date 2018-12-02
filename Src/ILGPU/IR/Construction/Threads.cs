// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Threads.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new predicated barrier.
        /// </summary>
        /// <param name="predicate">The barrier predicate.</param>
        /// <param name="kind">The barrier kind.</param>
        /// <returns>A node that represents the barrier.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public MemoryValue CreateBarrier(
            Value predicate,
            PredicateBarrierKind kind)
        {
            Debug.Assert(predicate != null, "Invalid predicate value");
            Debug.Assert(predicate.BasicValueType == BasicValueType.Int1, "Invalid predicate bool type");

            return Append(new PredicateBarrier(
                Context,
                BasicBlock,
                predicate,
                kind));
        }

        /// <summary>
        /// Creates a new barrier.
        /// </summary>
        /// <param name="kind">The barrier kind.</param>
        /// <returns>A node that represents the barrier.</returns>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public MemoryValue CreateBarrier(BarrierKind kind) =>
            Append(new Barrier(
                Context,
                BasicBlock,
                kind));

        /// <summary>
        /// Creates a new shuffle operation.
        /// </summary>
        /// <param name="variable">The variable.</param>
        /// <param name="origin">The shuffle origin (depends on the operation).</param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>A node that represents the shuffle operation.</returns>
        public ValueReference CreateShuffle(
            Value variable,
            Value origin,
            ShuffleKind kind)
        {
            Debug.Assert(variable != null, "Invalid variable value");
            Debug.Assert(origin != null, "Invalid origin value");

            return Append(new Shuffle(
                BasicBlock,
                variable,
                origin,
                kind));
        }

    }
}
