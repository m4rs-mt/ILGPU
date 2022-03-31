// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Threads.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new predicated barrier.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="predicate">The barrier predicate.</param>
        /// <param name="kind">The barrier kind.</param>
        /// <returns>A node that represents the barrier.</returns>
        public MemoryValue CreateBarrier(
            Location location,
            Value predicate,
            PredicateBarrierKind kind)
        {
            location.Assert(predicate.BasicValueType == BasicValueType.Int1);

            return Append(new PredicateBarrier(
                GetInitializer(location),
                predicate,
                kind));
        }

        /// <summary>
        /// Creates a new barrier.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="kind">The barrier kind.</param>
        /// <returns>A node that represents the barrier.</returns>
        public MemoryValue CreateBarrier(
            Location location,
            BarrierKind kind) =>
            Append(new Barrier(
                GetInitializer(location),
                kind));

        /// <summary>
        /// Returns true if the given variable is a constant with respect to a broadcast
        /// or shuffle value operating on the warp or the group level.
        /// </summary>
        /// <param name="variable">The variable to test.</param>
        /// <returns>
        /// True, if the given variable is a constant in the parent broadcast or shuffle
        /// context.
        /// </returns>
        private static bool IsShuffleOrBroadcastConstant(Value variable) =>
            variable switch
            {
                // Entry-point parameters can be considered uniform
                Parameter param => param.Method.HasFlags(MethodFlags.EntryPoint),
                PrimitiveValue _ => true,
                WarpSizeValue _ => true,
                GroupDimensionValue _ => true,
                GridDimensionValue _ => true,
                GridIndexValue _ => true,
                UnaryArithmeticValue unary =>
                    IsShuffleOrBroadcastConstant(unary.Value),
                BinaryArithmeticValue binary =>
                    IsShuffleOrBroadcastConstant(binary.Left) &&
                    IsShuffleOrBroadcastConstant(binary.Right),
                TernaryArithmeticValue ternary =>
                    IsShuffleOrBroadcastConstant(ternary.First) &&
                    IsShuffleOrBroadcastConstant(ternary.Second) &&
                    IsShuffleOrBroadcastConstant(ternary.Third),
                _ => false
            };

        /// <summary>
        /// Creates a new broadcast operation.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="variable">The variable.</param>
        /// <param name="origin">
        /// The broadcast origin (thread index within a group or a warp).
        /// </param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>A node that represents the broadcast operation.</returns>
        public ValueReference CreateBroadcast(
            Location location,
            Value variable,
            Value origin,
            BroadcastKind kind) =>
            IsShuffleOrBroadcastConstant(variable)
                ? variable
                : Append(new Broadcast(
                    GetInitializer(location),
                    variable,
                    origin,
                    kind));

        /// <summary>
        /// Creates a new shuffle operation involving all lanes of a warp.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="variable">The variable.</param>
        /// <param name="origin">The shuffle origin (depends on the operation).</param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>A node that represents the shuffle operation.</returns>
        public ValueReference CreateShuffle(
            Location location,
            Value variable,
            Value origin,
            ShuffleKind kind) =>
            IsShuffleOrBroadcastConstant(variable)
                ? variable
                : Append(new WarpShuffle(
                    GetInitializer(location),
                    variable,
                    origin,
                    kind));

        /// <summary>
        /// Creates a new sub-warp shuffle operation that operates
        /// on sub-groups of a warp.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="variable">The variable.</param>
        /// <param name="origin">The shuffle origin (depends on the operation).</param>
        /// <param name="width">The sub-warp width.</param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>A node that represents the sub shuffle operation.</returns>
        public ValueReference CreateShuffle(
            Location location,
            Value variable,
            Value origin,
            Value width,
            ShuffleKind kind) =>
            IsShuffleOrBroadcastConstant(variable)
                ? (ValueReference)variable
                : width is WarpSizeValue
                    ? CreateShuffle(
                        location,
                        variable,
                        origin,
                        kind)
                    : Append(new SubWarpShuffle(
                        GetInitializer(location),
                        variable,
                        origin,
                        width,
                        kind));
    }
}
