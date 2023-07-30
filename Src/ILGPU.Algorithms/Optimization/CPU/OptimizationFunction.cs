// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: OptimizationFunction.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace ILGPU.Algorithms.Optimization.CPU
{
    /// <summary>
    /// Represents a generic optimization function to be used with CPU-specific parts
    /// of the optimization library.
    /// </summary>
    /// <typeparam name="T">The main element type for all position vectors.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    public delegate TEvalType CPUOptimizationFunction<T, out TEvalType>(
        ReadOnlySpan<T> position)
        where T : struct
        where TEvalType : struct, IEquatable<TEvalType>;

    /// <summary>
    /// A raw optimization function operating on all positions and evaluation values
    /// directly to implement specialized and highly domain-specific evaluators.
    /// </summary>
    /// <typeparam name="T">The main element type for all position vectors.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    /// <param name="allPositions">
    /// A memory instance pointing to all packed position vectors of all particles.
    /// </param>
    /// <param name="evaluations">
    /// A memory instance pointing to all evaluation values of all particles.
    /// </param>
    /// <param name="numDimensions">The number of dimensions.</param>
    /// <param name="numPaddedDimensions">
    /// The number of padded dimensions taking vectorization into account.
    /// </param>
    /// <param name="numParticles">The number of particles.</param>
    /// <param name="positionStride">
    /// The position stride to be used to compute individual vector elements. In this
    /// scope, the X dimension refers to the number of players and the Y dimension
    /// is equal to the number of padded dimensions.
    /// </param>
    /// <param name="options">
    /// Parallel processing options to be used if further parallel processing is desired.
    /// </param>
    public delegate void RawCPUOptimizationFunction<T, TEvalType>(
        Memory<T> allPositions,
        Memory<TEvalType> evaluations,
        int numDimensions,
        int numPaddedDimensions,
        int numParticles,
        Stride2D.DenseY positionStride,
        ParallelOptions options);

    /// <summary>
    /// A custom break function to break the optimization loop at some point. Returns
    /// true if the optimization loop should be stopped.
    /// </summary>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    public delegate bool CPUOptimizationBreakFunction<TEvalType>(
        TEvalType evalType,
        int iteration);

    /// <summary>
    /// Represents a comparison function operating on evaluation types. If the first
    /// value is considered to be better than the second one, true will be returned by
    /// this function.
    /// </summary>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    public delegate bool CPUEvaluationComparison<TEvalType>(
        TEvalType first,
        TEvalType second);

    /// <summary>
    /// An abstract optimization function to be used with CPU-specific optimizers.
    /// </summary>
    /// <typeparam name="T">The main element type for all position vectors.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    public interface ICPUOptimizationFunction<T, TEvalType> :
        IBaseOptimizationFunction<TEvalType>
        where T : struct
        where TEvalType : struct, IEquatable<TEvalType>
    {
        /// <summary>
        /// Evaluates the given position vector.
        /// </summary>
        /// <param name="position">The position span.</param>
        /// <returns>The resulting evaluation value.</returns>
        TEvalType Evaluate(ReadOnlySpan<T> position);
    }

    /// <summary>
    /// An abstract optimization function to be used with CPU-specific optimizers.
    /// </summary>
    /// <typeparam name="T">The main element type for all position vectors.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    /// <typeparam name="TIntermediate">
    /// The type of all intermediate states during processing.
    /// </typeparam>
    public interface ICPUOptimizationFunction<T, TEvalType, TIntermediate> :
        IBaseOptimizationFunction<TEvalType>,
        IParallelCache<TIntermediate>
        where T : struct
        where TIntermediate : class
        where TEvalType : struct, IEquatable<TEvalType>
    {
        /// <summary>
        /// Evaluates the given position vector.
        /// </summary>
        /// <param name="position">The position span.</param>
        /// <param name="intermediateState">The intermediate processing state.</param>
        /// <returns>The resulting evaluation value.</returns>
        TEvalType Evaluate(ReadOnlySpan<T> position, TIntermediate intermediateState);
    }

    /// <summary>
    /// An abstract optimizer break logic to realize custom iteration logic.
    /// </summary>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    public interface ICPUOptimizationBreakFunction<TEvalType>
        where TEvalType : struct
    {
        /// <summary>
        /// Tests the given evaluation type and the current iteration to enable the
        /// implementation of custom optimizer break functionality and returns true if
        /// the current optimizer process should be terminated.
        /// </summary>
        /// <param name="evalType">The best found evaluation result so far.</param>
        /// <param name="iteration">The current solver iteration.</param>
        /// <returns>True if the current solver iteration should be terminated.</returns>
        bool Break(TEvalType evalType, int iteration);
    }
}
