// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: OptimizationResult.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Algorithms.Optimization
{
    /// <summary>
    /// An optimization result view pointing to regions in accelerator memory.
    /// </summary>
    /// <typeparam name="TElementType">
    /// The numeric element type used for optimization.
    /// </typeparam>
    /// <typeparam name="TEvalType">
    /// The evaluation type used for evaluation.
    /// </typeparam>
    public readonly struct OptimizationResultView<TElementType, TEvalType>
        where TElementType : unmanaged
        where TEvalType : unmanaged
    {
        internal OptimizationResultView(
            VariableView<TEvalType> resultView,
            ArrayView<TElementType> positionView,
            double elapsedTime)
        {
            ResultView = resultView;
            PositionView = positionView;
            ElapsedTime = elapsedTime;
        }
        
        /// <summary>
        /// Returns the actual result value.
        /// </summary>
        public VariableView<TEvalType> ResultView { get; }
        
        /// <summary>
        /// Returns the underlying result view containing the position vector.
        /// </summary>
        public ArrayView<TElementType> PositionView { get; }
        
        /// <summary>
        /// The total elapsed time in milliseconds.
        /// </summary>
        public double ElapsedTime { get; }
    }
    
    /// <summary>
    /// An optimization result in CPU space.
    /// </summary>
    /// <typeparam name="TElementType">
    /// The numeric element type used for optimization.
    /// </typeparam>
    /// <typeparam name="TEvalType">
    /// The evaluation type used for evaluation.
    /// </typeparam>
    public readonly ref struct OptimizationResult<TElementType, TEvalType>
        where TElementType : unmanaged
        where TEvalType : unmanaged
    {
        private readonly ReadOnlySpan<TEvalType> resultSpan;

        internal OptimizationResult(
            ReadOnlySpan<TEvalType> result,
            ReadOnlySpan<TElementType> resultVector,
            double elapsedTime)
        {
            resultSpan = result;
            ResultVector = resultVector;
            ElapsedTime = elapsedTime;
        }

        /// <summary>
        /// Returns the actual result value.
        /// </summary>
        public TEvalType Result => resultSpan[0];
        
        /// <summary>
        /// Returns the best result vector.
        /// </summary>
        public ReadOnlySpan<TElementType> ResultVector { get; }
        
        /// <summary>
        /// The total elapsed time in milliseconds.
        /// </summary>
        public double ElapsedTime { get; }
    }
}
