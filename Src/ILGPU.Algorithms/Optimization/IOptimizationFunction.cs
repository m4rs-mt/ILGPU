// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IOptimizationFunction.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Vectors;
using System;
using System.Numerics;

namespace ILGPU.Algorithms.Optimization
{
    /// <summary>
    /// An abstract optimization function supporting comparisons between evaluation types.
    /// </summary>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    public interface IBaseOptimizationFunction<TEvalType>
        where TEvalType : struct, IEquatable<TEvalType>
    {
        /// <summary>
        /// Compares the current evaluation value with the proposed one and returns true
        /// if the current one is considered better in any way.
        /// </summary>
        /// <param name="current">The currently known value.</param>
        /// <param name="proposed">The proposed evaluation value.</param>
        /// <returns>
        /// True if the current value is considered better than the proposed value.
        /// </returns>
        bool CurrentIsBetter(TEvalType current, TEvalType proposed);
    }

#if NET7_0_OR_GREATER
    /// <summary>
    /// A generic optimization function that defines the objective of an optimization
    /// process using evaluation and comparison methods.
    /// </summary>
    /// <typeparam name="TNumericType">The vectorized numeric type.</typeparam>
    /// <typeparam name="TElementType">The element type of a numeric type.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    public interface IOptimizationFunction<TNumericType, TElementType, TEvalType> :
        IBaseOptimizationFunction<TEvalType>
        where TNumericType : unmanaged, IVectorType<TNumericType, TElementType>
        where TElementType : unmanaged, INumber<TElementType>
        where TEvalType : unmanaged, IEquatable<TEvalType>
    {
        /// <summary>
        /// Evaluates the given position vector view using the current particle index and
        /// a given vector dimension (length). Conceptually, this method is intended to
        /// iterate over all position-view elements, load all vector values sequentially
        /// and aggregate a single evaluation value for this particle.
        /// </summary>
        /// <param name="index">The current particle index.</param>
        /// <param name="dimension">The statically known vector dimension.</param>
        /// <param name="positionView">The current position view.</param>
        /// <returns>The evaluation result.</returns>
        TEvalType Evaluate(
            LongIndex1D index,
            Index1D dimension,
            SingleVectorView<TNumericType> positionView);
    }
#endif
}

