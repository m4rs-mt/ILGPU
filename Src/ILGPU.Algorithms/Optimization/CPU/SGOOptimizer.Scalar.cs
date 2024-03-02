// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: SGOOptimizer.Scalar.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

#if NET7_0_OR_GREATER

namespace ILGPU.Algorithms.Optimization.CPU
{
    partial class SGOOptimizer<T, TEvalType>
    {
        /// <summary>
        /// A scalar processor using default ALUs.
        /// </summary>
        private readonly struct ScalarProcessor : IProcessor<ScalarProcessor, T>
        {
            /// <summary>
            /// Creates a new scalar processor.
            /// </summary>
            public static ScalarProcessor New() => default;

            /// <summary>
            /// Returns 1;
            /// </summary>
            public static int Length => 1;

            /// <summary>
            /// Clamps the given value.
            /// </summary>
            /// <param name="lower">The lower bounds part.</param>
            /// <param name="upper">The upper bounds part.</param>
            /// <param name="value">The value to clamp.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Clamp(T lower, T upper, T value) =>
                T.Clamp(value, lower, upper);

            /// <summary>
            /// Resets the given data view.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset(out T data) => data = T.Zero;

            /// <summary>
            /// Adds the given source to the target view.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Accumulate(ref T target, T source) =>
                target += source;

            /// <summary>
            /// Computes the average by taking the given count into account.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ComputeAverage(ref T target, T count) =>
                target /= count;

            /// <summary>
            /// Determines a newly sampled position using scalars.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T GetRandomPosition(T lower, T upper, T randomNumber)
            {
                // Interpolate between lower and upper bound
                var lowerInfluence = (T.One - randomNumber) * lower;
                var upperInfluence = randomNumber * upper;
                return lowerInfluence + upperInfluence;
            }

            /// <summary>
            /// Determines a newly sampled position using scalars.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T DetermineNewPosition(
                T position,
                T firstC,
                T secondC,
                T r1,
                T r2,
                T stepSize)
            {
                // Determine new offset to use
                var newOffset = r1 * firstC - r2 * secondC;

                // Compute final position
                var finalPos = position + newOffset * stepSize;
                return finalPos;
            }
        }
    }
}

#endif
