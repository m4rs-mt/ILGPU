// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PSView.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Vectors;
using System;

#if NET7_0_OR_GREATER

namespace ILGPU.Algorithms.Optimization.Optimizers
{
    /// <summary>
    /// An internal PS view containing all nested data views required to realize 
    /// </summary>
    /// <typeparam name="TNumericType">The vectorized numeric type.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    readonly struct PSView<TNumericType, TEvalType>
        where TNumericType : unmanaged, IVectorType
        where TEvalType : unmanaged, IEquatable<TEvalType>
    {
        internal PSView(
            in VectorView<TNumericType> positions,
            in VectorView<TNumericType> velocities,
            in VectorView<TNumericType> bestPositions,
            in ArrayView<TEvalType> fitness)
        {
            Positions = positions;
            Velocities = velocities;
            BestPositions = bestPositions;
            Fitness = fitness;
        }
        
        /// <summary>
        /// Returns a view to all position vectors.
        /// </summary>
        public VectorView<TNumericType> Positions { get; }
        
        /// <summary>
        /// Returns a view to all velocity vectors.
        /// </summary>
        public VectorView<TNumericType> Velocities { get; }
        
        /// <summary>
        /// Returns a view to all best-position vectors.
        /// </summary>
        public VectorView<TNumericType> BestPositions { get; }
        
        /// <summary>
        /// Returns a view to all best evaluation values per particle.
        /// </summary>
        public ArrayView<TEvalType> Fitness { get; }
    }
}

#endif
