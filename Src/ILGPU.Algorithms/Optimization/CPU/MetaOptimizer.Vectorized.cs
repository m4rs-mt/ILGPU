// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetaOptimizer.Vectorized.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Numerics;
using System.Runtime.CompilerServices;

#if NET7_0_OR_GREATER

namespace ILGPU.Algorithms.Optimization.CPU
{
    partial class MetaOptimizer<T, TEvalType>
    {
        /// <summary>
        /// A vectorized processor using SIMD operations.
        /// </summary>
        private readonly struct VectorizedProcessor :
            IProcessor<VectorizedProcessor, Vector<T>>
        {
            /// <summary>
            /// Creates a new vectorized processor.
            /// </summary>
            public static VectorizedProcessor New() => default;

            /// <summary>
            /// Returns the vector length.
            /// </summary>
            public static int Length => Vector<int>.Count;

            /// <summary>
            /// Clamps the given vector.
            /// </summary>
            /// <param name="lower">The lower bounds part.</param>
            /// <param name="upper">The upper bounds part.</param>
            /// <param name="value">The vector to clamp.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector<T> Clamp(
                Vector<T> lower,
                Vector<T> upper,
                Vector<T> value) =>
                Vector.Min(Vector.Max(value, lower), upper);

            /// <summary>
            /// Resets the given data view.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset(out Vector<T> data) =>
                data = new Vector<T>(T.Zero);

            /// <summary>
            /// Adds the given source to the target view.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Accumulate(ref Vector<T> target, Vector<T> source)
            {
                var accumulated = source + target;
                target = accumulated;
            }

            /// <summary>
            /// Computes the average by taking the given count into account.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ComputeAverage(ref Vector<T> target, T count)
            {
                var countValue = new Vector<T>(count);
                var average = target / countValue;
                target = average;
            }

            /// <summary>
            /// Determines a newly sampled position using vectors.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector<T> GetRandomPosition(
                Vector<T> lower,
                Vector<T> upper,
                Vector<T> randomNumber)
            {
                // Interpolate between lower and upper bound
                var lowerFactor = new Vector<T>(T.One) - randomNumber;
                var lowerInfluence = lowerFactor * lower;
                var upperInfluence = randomNumber * upper;
                return lowerInfluence + upperInfluence;
            }

            /// <summary>
            /// Determines a newly sampled position using vectors.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector<T> DetermineNewPosition(
                Vector<T> position,
                Vector<T> firstC,
                Vector<T> secondC,
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
