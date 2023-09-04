// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PSOptimizer.cs
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
    /// A PS-specific optimizer feature that realizes domain-specific initialization and
    /// position updates.
    /// </summary>
    /// <typeparam name="TNumericType">The vectorized numeric type.</typeparam>
    /// <typeparam name="TElementType">The element type of a numeric type.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    /// <typeparam name="TRandomProvider">The RNG type.</typeparam>
    readonly struct PSOptimizerFunc<
        TNumericType,
        TElementType,
        TEvalType,
        TRandomProvider>
        : IOptimizerFunc<TNumericType, TElementType, TEvalType, TRandomProvider>
        where TNumericType : unmanaged, IVectorType<TNumericType, TElementType>
        where TElementType : unmanaged, INumber<TElementType>
        where TEvalType : unmanaged, IEquatable<TEvalType>
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        /// <summary>
        /// Creates a new PS-specific optimizer function.
        /// </summary>
        /// <param name="view">The parent PS view.</param>
        public PSOptimizerFunc(PSView<TNumericType, TEvalType> view)
        {
            View = view;
        }
        
        /// <summary>
        /// Returns the parent particle swarm view.
        /// </summary>
        public PSView<TNumericType, TEvalType> View { get; }

        /// <summary>
        /// Initializes the current Fitness and BestPosition values using the provided
        /// RNG.
        /// </summary>
        public void Initialize(
            ref TRandomProvider random,
            LongIndex1D index,
            Index1D vectorIndex,
            TEvalType bestResult,
            TNumericType lowerBound,
            TNumericType upperBound,
            TNumericType bestPosition)
        {
            // Initialize fitness and best result vector element
            if (vectorIndex == 0)
                View.Fitness[index] = bestResult;
            View.BestPositions[index, vectorIndex] = bestPosition;
            
            // Select a random start position
            var initPosition = TNumericType.GetRandom(
                ref random,
                lowerBound,
                upperBound);
            View.Positions[index, vectorIndex] = initPosition;

            // Select a random start velocity
            var initVelocity = TNumericType.GetRandom(
                ref random,
                -TNumericType.Abs(upperBound - lowerBound),
                TNumericType.Abs(upperBound - lowerBound));
            View.Velocities[index, vectorIndex] = initVelocity;
        }

        /// <summary>
        /// Returns the evaluation result for the given particle.
        /// </summary>
        public TEvalType GetEvaluationResult(LongIndex1D index) =>
            View.Fitness[index];

        /// <summary>
        /// Returns a view to the i-th particle position.
        /// </summary>
        public SingleVectorView<TNumericType> GetPosition(LongIndex1D index) =>
            View.Positions.SliceVector(index);
        
        /// <summary>
        /// Does not change the internal state as new evaluation results will not modify
        /// PS-based particles.
        /// </summary>
        public void SetEvaluationResult(
            LongIndex1D index,
            TEvalType evalValue,
            Index1D dimension)
        {
            // Do not change anything in this case
        }
        
        /// <summary>
        /// As soon as a better result for this particle is available, the local fitness
        /// value is updated and the best-known position for the current particle is
        /// overwritten with the newly available position information.
        /// </summary>
        public void ReportBetterEvaluationResult(
            LongIndex1D index,
            TEvalType evalValue,
            Index1D dimension)
        {
            // Set the main evaluation result of the referenced particle
            View.Fitness[index] = evalValue;
            
            // Copy the current positions vector to the locally best-found view
            for (int i = 0; i < dimension; ++i)
                View.BestPositions[index, i] = View.Positions[index, i];
        }
    }
    
    /// <summary>
    /// Represents a PS and objective-function specific optimizer instance.
    /// </summary>
    /// <typeparam name="TNumericType">The vectorized numeric type.</typeparam>
    /// <typeparam name="TElementType">The element type of a numeric type.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    /// <typeparam name="TFunc">The objective function type.</typeparam>
    /// <typeparam name="TRandomProvider">The RNG type.</typeparam>
    sealed class PSOptimizer<
        TNumericType,
        TElementType,
        TEvalType,
        TFunc,
        TRandomProvider> :
        Optimizer<
            TNumericType,
            TElementType,
            TEvalType,
            TFunc,
            PSOptimizerFunc<TNumericType, TElementType, TEvalType, TRandomProvider>,
            TRandomProvider>,
        IOptimizer<TNumericType, TElementType, TEvalType>
        where TNumericType : unmanaged, IVectorType<TNumericType, TElementType>
        where TElementType : unmanaged, INumber<TElementType>
        where TEvalType : unmanaged, IEquatable<TEvalType>
        where TFunc : struct, IOptimizationFunction<TNumericType, TElementType, TEvalType>
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        /// <summary>
        /// Moves all particles closer to currently best known solution.
        /// </summary>
        internal static void MoveParticlesKernel(
            ArrayView<TRandomProvider> rngView,
            SingleVectorView<TNumericType> bestPositionView,
            BoundsView<TNumericType> boundsView,
            PSView<TNumericType, TEvalType> view,
            LongIndex1D numParticles,
            TElementType omega,
            TElementType phiP,
            TElementType phiG,
            SpecializedValue<Index1D> vectorDimension)
        {
            // Iterate over all particles
            for (LongIndex1D particleIndex = Grid.GlobalLinearIndex;
                particleIndex < numParticles;
                particleIndex += GridExtensions.GridStrideLoopStride)
            {
                var random = rngView[particleIndex];
                // Iterate over all vector dimensions
                for (Index1D vi = 0; vi < vectorDimension.Value; ++vi)
                {
                    var positionV = view.Positions[particleIndex, vi];

                    // Evaluate the first position adjustment using the currently best-
                    // known position per particle
                    var localBestPositionV = view.BestPositions[particleIndex, vi];
                    var rp = TNumericType.GetRandom(
                        ref random,
                        TNumericType.Zero,
                        TNumericType.One);
                    var phiPVec = TNumericType.FromScalar(phiP) * rp *
                        (localBestPositionV - positionV);

                    // Evaluate the second position adjustment using the currently best-
                    // known global position
                    var globalBestPositionV = bestPositionView[vi];
                    var rg = TNumericType.GetRandom(
                        ref random,
                        TNumericType.Zero,
                        TNumericType.One);
                    var phiGVec = TNumericType.FromScalar(phiG) * rg *
                        (globalBestPositionV - positionV);

                    // Compute new velocity vector
                    var sourceVelocityV = view.Velocities[particleIndex, vi];
                    var velocityV = TNumericType.FromScalar(omega) *
                        sourceVelocityV + phiGVec + phiPVec;
                    
                    // Adjust velocity
                    view.Velocities[particleIndex, vi] = velocityV;
                    
                    // Get bounds
                    var (lower, upper) = boundsView[vi];

                    // Adjust position
                    var newPositionV = positionV + velocityV;
                    var clamped = TNumericType.Clamp(newPositionV, lower, upper);
                    view.Positions[particleIndex, vi] = clamped;
                }
                
                // Update random state
                rngView[particleIndex] = random;
            }
        }

        private readonly PSView<TNumericType, TEvalType> view;
        private readonly TFunc optimizationFunction;
        
        private readonly Action<
            AcceleratorStream,
            KernelConfig,
            ArrayView<TRandomProvider>,
            SingleVectorView<TNumericType> ,
            BoundsView<TNumericType>,
            PSView<TNumericType, TEvalType>,
            LongIndex1D,
            TElementType,
            TElementType,
            TElementType,
            SpecializedValue<Index1D>> moveParticles;
        
        /// <summary>
        /// Creates a new PS optimizer.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="engine">The parent PS engine.</param>
        /// <param name="random">The parent random number generator.</param>
        /// <param name="function">The current optimization function.</param>
        internal PSOptimizer(
            Accelerator accelerator,
            AcceleratorStream stream,
            PSO<TNumericType, TElementType, TEvalType, TRandomProvider> engine,
            System.Random random,
            in TFunc function)
            : base(
                accelerator,
                stream,
                engine.MaxNumParticles,
                engine.VectorDimension,
                random)
        {
            optimizationFunction = function;
            view = engine.DataView;

            Engine = engine;
            
            // Load kernels
            moveParticles = accelerator.LoadKernel<
                ArrayView<TRandomProvider>,
                SingleVectorView<TNumericType>,
                BoundsView<TNumericType>,
                PSView<TNumericType, TEvalType>,
                LongIndex1D,
                TElementType,
                TElementType,
                TElementType,
                SpecializedValue<Index1D>>(MoveParticlesKernel);
        }
        
        /// <summary>
        /// Returns the parent optimization engine.
        /// </summary>
        OptimizationEngine<TNumericType, TElementType, TEvalType>
            IOptimizer<TNumericType, TElementType, TEvalType>.Engine => Engine;
        
        /// <summary>
        /// Returns the parent PSO engine.
        /// </summary>
        public PSO<TNumericType, TElementType, TEvalType, TRandomProvider> Engine { get; }

        /// <summary>
        /// Gets or sets the omega parameter.
        /// </summary>
        public TElementType Omega => Engine.Omega;

        /// <summary>
        /// Gets or sets the phi p velocity parameter.
        /// </summary>
        public TElementType PhiP => Engine.PhiP;

        /// <summary>
        /// Gets or sets the phi g velocity parameter.
        /// </summary>
        public TElementType PhiG => Engine.PhiG;

        /// <summary>
        /// Starts a single iteration by initializing all positions and velocities.
        /// </summary>
        public void Start(
            AcceleratorStream stream,
            ReadOnlySpan<TElementType> bestPosition,
            TEvalType bestResult,
            SingleVectorView<TNumericType> resultView,
            VariableView<TEvalType> evalView)
        {
            InitializeParticles(
                stream,
                new(view),
                Engine.BoundsView,
                resultView,
                evalView);
        }
        
        /// <summary>
        /// Implements a single PSO iteration by evaluating all particles, aggregating
        /// all intermediate results, and moving all particles to updated positions.
        /// </summary>
        public void Iteration(
            AcceleratorStream stream,
            SingleVectorView<TNumericType> resultView,
            VariableView<TEvalType> evalView,
            int iteration)
        {
            // Evaluate all particles first
            EvaluateParticles(
                stream,
                new(view),
                optimizationFunction);
            
            // Aggregate all evaluations to ensure the best solution is visible for all
            // particles in the optimization domain
            AggregateParticleEvaluations(
                stream,
                new(view),
                resultView,
                evalView);
            
            // Move all particles
            int gridSize = GetGridSize(out var _);
            moveParticles(
                stream,
                (gridSize, GroupSize),
                RNGView,
                resultView,
                Engine.BoundsView,
                view,
                NumParticles,
                Omega,
                PhiP,
                PhiG,
                SpecializedValue.New(VectorDimension));
        }
        
        /// <summary>
        /// Does not perform any operation in the case of PSO.
        /// </summary>
        public void Finish(
            AcceleratorStream stream,
            SingleVectorView<TNumericType> resultView,
            VariableView<TEvalType> evalView)
        {
            // Nothing to do in this case...
        }
    }
}

#endif
