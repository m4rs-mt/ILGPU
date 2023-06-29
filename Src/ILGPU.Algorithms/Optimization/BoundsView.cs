// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: BoundsView.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Vectors;

#if NET7_0_OR_GREATER

#pragma warning disable CA1043 // Use integral types for indexers

namespace ILGPU.Algorithms.Optimization
{
    /// <summary>
    /// A multidimensional bounds view to operate on optimization-value bounds.
    /// </summary>
    /// <typeparam name="TNumericType">The numeric vector type used.</typeparam>
    public readonly struct BoundsView<TNumericType>
        where TNumericType : unmanaged, IVectorType
    {
        private readonly SingleVectorView<TNumericType> lowerBoundsView;
        private readonly SingleVectorView<TNumericType> upperBoundsView;

        /// <summary>
        /// Construct a new bounds view.
        /// </summary>
        /// <param name="lowerBounds">The lower bounds view.</param>
        /// <param name="upperBounds">The upper bounds view.</param>
        public BoundsView(
            SingleVectorView<TNumericType> lowerBounds,
            SingleVectorView<TNumericType> upperBounds)
        {
            lowerBoundsView = lowerBounds;
            upperBoundsView = upperBounds;
        }

        /// <summary>
        /// Loads an upper and its corresponding lower bound value of the given relative
        /// dimension index.
        /// </summary>
        /// <param name="dimensionIndex">The relative dimension index.</param>
        public (TNumericType Lower, TNumericType Upper) this[Index1D dimensionIndex] =>
            (GetLowerBound(dimensionIndex), GetUpperBound(dimensionIndex));
        
        /// <summary>
        /// Loads a lower bound value of the given relative dimension index.
        /// </summary>
        /// <param name="dimensionIndex">The relative dimension index.</param>
        /// <returns>The loaded bound value.</returns>
        public TNumericType GetLowerBound(Index1D dimensionIndex) =>
            lowerBoundsView[dimensionIndex];
        
        /// <summary>
        /// Loads an upper bound value of the given relative dimension index.
        /// </summary>
        /// <param name="dimensionIndex">The relative dimension index.</param>
        /// <returns>The loaded bound value.</returns>
        public TNumericType GetUpperBound(Index1D dimensionIndex) =>
            upperBoundsView[dimensionIndex];
    }
}

#pragma warning restore CA1043

#endif
