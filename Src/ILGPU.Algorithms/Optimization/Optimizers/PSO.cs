// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PSO.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Random;
using ILGPU.Algorithms.Vectors;
using ILGPU.Runtime;
using System;
using System.Numerics;

#if NET7_0_OR_GREATER

namespace ILGPU.Algorithms.Optimization.Optimizers
{
    /// <summary>
    /// General helpers and constants for PSO.
    /// </summary>
    public static class PSO
    {
        /// <summary>
        /// A common choice for Omega.
        /// </summary>
        public const float Omega  = 0.8f;
        
        /// <summary>
        /// A common choice for PhiG.
        /// </summary>
        public const float PhiG = 1.5f;
        
        /// <summary>
        /// A common choice for PhiP.
        /// </summary>
        public const float PhiP = 1.5f;

        /// <summary>
        /// Internal array holding all parameters in the right order.
        /// </summary>
        private static readonly float[] FloatParameters = new float[]
        {
            Omega, PhiG, PhiP,
        };

        /// <summary>
        /// Returns default parameters for common use cases.
        /// </summary>
        public static ReadOnlySpan<float> DefaultFloatParameters => FloatParameters;
    }
    
    /// <summary>
    /// Represents a particle-swap optimization engine (PSO) that uses a PSO algorithm
    /// to solve n-dimensional optimization problems.
    /// </summary>
    /// <typeparam name="TNumericType">The vectorized numeric type.</typeparam>
    /// <typeparam name="TElementType">The element type of a numeric type.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    /// <typeparam name="TRandomProvider">The RNG type.</typeparam>
    [CLSCompliant(false)]
    public sealed class PSO<TNumericType, TElementType, TEvalType, TRandomProvider>
        : OptimizationEngine<TNumericType, TElementType, TEvalType>
        where TNumericType : unmanaged, IVectorType<TNumericType, TElementType>
        where TElementType : unmanaged, INumber<TElementType>
        where TEvalType : unmanaged, IEquatable<TEvalType>
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        private const int NumEngineParameters = 3;
        private const int ParameterOmega = 0;
        private const int ParameterPhiP = 1;
        private const int ParameterPhiG = 2;
        
        private readonly MemoryBuffer2D<TNumericType, Stride2D.DenseY> velocities;
        private readonly MemoryBuffer2D<TNumericType, Stride2D.DenseY> bestPositions;
        
        /// <summary>
        /// Creates a new PS engine.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="maxNumParticles">
        /// The maximum number of supported particles.
        /// </param>
        /// <param name="dimension">
        /// The dimensionality of the optimization problem.
        /// </param>
        public PSO(
            Accelerator accelerator,
            LongIndex1D maxNumParticles,
            int dimension)
            : base(accelerator, maxNumParticles, NumEngineParameters, dimension)
        {
            velocities = VectorView<TNumericType>.Allocate(
                accelerator,
                maxNumParticles,
                VectorDimension);
            bestPositions = VectorView<TNumericType>.Allocate(
                accelerator,
                maxNumParticles,
                VectorDimension);

            DataView = new PSView<TNumericType, TEvalType>(
                PositionsView,
                velocities.View,
                bestPositions.View,
                EvaluationsView);
        }
        
        /// <summary>
        /// Returns a PSO-specific data view.
        /// </summary>
        internal PSView<TNumericType, TEvalType> DataView { get; }

        /// <summary>
        /// Gets or sets the omega parameter.
        /// </summary>
        public TElementType Omega
        {
            get => GetParameter(ParameterOmega);
            set => GetParameter(ParameterOmega) = value;
        }
        
        /// <summary>
        /// Gets or sets the phi p velocity parameter.
        /// </summary>
        public TElementType PhiP
        {
            get => GetParameter(ParameterPhiP);
            set => GetParameter(ParameterPhiP) = value;
        }
        
        /// <summary>
        /// Gets or sets the phi g velocity parameter.
        /// </summary>
        public TElementType PhiG
        {
            get => GetParameter(ParameterPhiG);
            set => GetParameter(ParameterPhiG) = value;
        }

        /// <summary>
        /// Creates a new PSO instance.
        /// </summary>
        /// <typeparam name="TFunc">The optimization function type.</typeparam>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="random">The CPU-based source RNG.</param>
        /// <param name="optimizationFunction">The optimization function to use.</param>
        /// <returns>The create optimizer instance.</returns>
        public override IOptimizer<TNumericType, TElementType, TEvalType>
            CreateOptimizer<TFunc>(
            AcceleratorStream stream,
            System.Random random,
            in TFunc optimizationFunction) =>
            new PSOptimizer<
                TNumericType,
                TElementType,
                TEvalType,
                TFunc,
                TRandomProvider>(
                Accelerator,
                stream,
                this,
                random,
                optimizationFunction);

        /// <summary>
        /// Frees internal velocity and best position buffers.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                velocities.Dispose();
                bestPositions.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

#endif
