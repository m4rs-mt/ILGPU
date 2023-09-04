// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Optimizer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Random;
using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Algorithms.Vectors;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

#if NET7_0_OR_GREATER

namespace ILGPU.Algorithms.Optimization
{
    /// <summary>
    /// An abstract optimizer function operating on particles.
    /// </summary>
    /// <typeparam name="TNumericType">The vectorized numeric type.</typeparam>
    /// <typeparam name="TElementType">The element type of a numeric type.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    /// <typeparam name="TRandomProvider">The RNG provider type.</typeparam>
    interface IOptimizerFunc<
        TNumericType,
        TElementType,
        TEvalType,
        TRandomProvider>
        where TNumericType : unmanaged, IVectorType<TNumericType, TElementType>
        where TElementType : unmanaged, INumber<TElementType>
        where TEvalType : unmanaged
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        /// <summary>
        /// Initializes underlying particle data structures using the random number
        /// provider and the lower and upper bounds.
        /// </summary>
        /// <param name="bestResult">The best known result.</param>
        /// <param name="random">The current RNG provider.</param>
        /// <param name="index">The current particle index.</param>
        /// <param name="vectorIndex">The current relative vector element index.</param>
        /// <param name="lowerBound">The lower bound part.</param>
        /// <param name="upperBound">The upper bound part.</param>
        /// <param name="bestPosition">
        /// The position representing the best known position vector.
        /// </param>
        void Initialize(
            ref TRandomProvider random,
            LongIndex1D index,
            Index1D vectorIndex,
            TEvalType bestResult,
            TNumericType lowerBound,
            TNumericType upperBound,
            TNumericType bestPosition);

        /// <summary>
        /// Returns the evaluation result of the referenced particle.
        /// </summary>
        /// <param name="index">The particle index.</param>
        /// <returns>The evaluation result.</returns>
        TEvalType GetEvaluationResult(LongIndex1D index);
        
        /// <summary>
        /// Returns the position vector view of the referenced particle.
        /// </summary>
        /// <param name="index">The particle index.</param>
        /// <returns>The position vector view.</returns>
        SingleVectorView<TNumericType> GetPosition(LongIndex1D index);

        /// <summary>
        /// Sets a new evaluation result which is better than the current one.
        /// </summary>
        /// <param name="index">The particle index.</param>
        /// <param name="evalValue">The (better) evaluation value.</param>
        /// <param name="dimension">The current vector dimension.</param>
        void SetEvaluationResult(
            LongIndex1D index,
            TEvalType evalValue,
            Index1D dimension);
        
        /// <summary>
        /// Sets a better evaluation result which is better than the current one.
        /// </summary>
        /// <param name="index">The particle index.</param>
        /// <param name="evalValue">The (better) evaluation value.</param>
        /// <param name="dimension">The current vector dimension.</param>
        void ReportBetterEvaluationResult(
            LongIndex1D index,
            TEvalType evalValue,
            Index1D dimension);
    }
    
    abstract class Optimizer<
        TNumericType,
        TElementType,
        TEvalType,
        TFunc,
        TOptimizerFunc,
        TRandomProvider> : ThreadWiseRNG<TRandomProvider>
        where TNumericType : unmanaged, IVectorType<TNumericType, TElementType>
        where TElementType : unmanaged, INumber<TElementType>
        where TEvalType : unmanaged, IEquatable<TEvalType>
        where TFunc : struct, IOptimizationFunction<TNumericType, TElementType, TEvalType>
        where TOptimizerFunc :
            struct,
            IOptimizerFunc<TNumericType, TElementType, TEvalType, TRandomProvider>
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        #region Nested Types
        
        /// <summary>
        /// A composed temporary result storing an evaluation value and a source index
        /// from which the evaluation result has been computed.
        /// </summary>
        internal readonly struct TempResult
        {
            /// <summary>
            /// An invalid temporary result.
            /// </summary>
            public static readonly TempResult Invalid = new(default, LongIndex1D.Zero);
            
            /// <summary>
            /// Constructs a new temporary result.
            /// </summary>
            /// <param name="result">The result evaluation value.</param>
            /// <param name="particleIndex">The source particle index (if any).</param>
            public TempResult(TEvalType result, LongIndex1D particleIndex)
            {
                Result = result;
                ParticleIndex = particleIndex;
            }
            
            /// <summary>
            /// Returns the evaluation result.
            /// </summary>
            public TEvalType Result { get; }
            /// <summary>
            /// Returns the current particle index (if any).
            /// </summary>
            public LongIndex1D ParticleIndex { get; }
            /// <summary>
            /// Returns true if the particle index is valid (greater than or equal to 0).
            /// </summary>
            public bool HasPointIndex => ParticleIndex > 0;

            /// <summary>
            /// Returns true if the current instance is better than the other instance.
            /// </summary>
            /// <param name="other">The other temp result instance.</param>
            public bool IsBetterThan(TempResult other)
            {
                // Basic comparison checks
                if (!HasPointIndex & other.HasPointIndex)
                    return false;
                if (HasPointIndex & !other.HasPointIndex)
                    return true;
                // Evaluate user function
                var defaultFunc = default(TFunc);
                return defaultFunc.CurrentIsBetter(Result, other.Result);
            }

            /// <summary>
            /// Returns a tuple-like string representation of this instance.
            /// </summary>
            public override string ToString() => $"({Result}, {ParticleIndex})";
        }

        /// <summary>
        /// An internal result reduction provider.
        /// </summary>
        private readonly struct TempResultReduction : IScanReduceOperation<TempResult>
        {
            /// <summary>
            /// OpenCL is not supported by this reduction provider.
            /// </summary>
            public string CLCommand => throw new NotSupportedException();

            /// <summary>
            /// Returns an invalid temp result.
            /// </summary>
            public TempResult Identity => TempResult.Invalid;
        
            /// <summary>
            /// Uses the custom <see cref="TempResult.IsBetterThan(TempResult)"/>
            /// function to determine whether the first or the second result is best.
            /// </summary>
            public TempResult Apply(TempResult first, TempResult second)
            {
                bool firstIsBetter = first.IsBetterThan(second);
                return Utilities.Select(firstIsBetter, first, second);
            }

            /// <summary>
            /// Not supported operation.
            /// </summary>
            public void AtomicApply(ref TempResult target, TempResult value) =>
                throw new NotSupportedException();
        }
        
        #endregion

        #region Kernels

        /// <summary>
        /// Initializes all particles to random positions within given bounds.
        /// </summary>
        internal static void InitializeParticlesKernel(
            ArrayView<TRandomProvider> rngView,
            BoundsView<TNumericType> boundsView,
            SingleVectorView<TNumericType> bestPositionView,
            VariableView<TEvalType> bestResultView,
            TOptimizerFunc optimizerFunc,
            LongIndex1D numParticles,
            SpecializedValue<Index1D> vectorDimension)
        {
            var bestResult = bestResultView.Value;
            for (LongIndex1D particleIndex = Grid.GlobalLinearIndex;
                particleIndex < numParticles;
                particleIndex += GridExtensions.GridStrideLoopStride)
            {
                // Load RNG state
                var random = rngView[particleIndex];

                // Draw random numbers within all bounds
                for (Index1D vi = 0; vi < vectorDimension.Value; ++vi)
                {
                    var lowerBoundV = boundsView.GetLowerBound(vi);
                    var upperBoundV = boundsView.GetUpperBound(vi);
                
                    optimizerFunc.Initialize(
                        ref random,
                        particleIndex,
                        vi,
                        bestResult,
                        lowerBoundV,
                        upperBoundV,
                        bestPositionView[vi]);
                }

                // Store updated RNG state
                rngView[particleIndex] = random;
            }
        }
        
        /// <summary>
        /// Initializes all particles to random positions within given bounds.
        /// </summary>
        internal static void EvaluateParticlesKernel(
            TOptimizerFunc optimizerFunc,
            TFunc func,
            LongIndex1D numParticles,
            SpecializedValue<Index1D> dimension)
        {
            for (LongIndex1D particleIndex = Grid.GlobalLinearIndex;
                particleIndex < numParticles;
                particleIndex += GridExtensions.GridStrideLoopStride)
            {
                // Evaluate current particle
                var positionView = optimizerFunc.GetPosition(particleIndex);
                var evaluationResult = func.Evaluate(
                    particleIndex,
                    dimension.Value,
                    positionView);

                // Set the current result
                optimizerFunc.SetEvaluationResult(
                    particleIndex,
                    evaluationResult,
                    dimension.Value);
                
                // Compare the result
                var currentResult = optimizerFunc.GetEvaluationResult(particleIndex);
                if (func.CurrentIsBetter(currentResult, evaluationResult))
                    continue;
                
                // Report a better result if possible
                optimizerFunc.ReportBetterEvaluationResult(
                    particleIndex,
                    evaluationResult,
                    dimension.Value);
            }
        }

        /// <summary>
        /// The first pass of the aggregation kernel.
        /// </summary>
        internal static void AggregateEvaluations1Kernel(
            TOptimizerFunc optimizerFunc,
            ArrayView<TEvalType> intermediateResultsView,
            ArrayView<LongIndex1D> intermediateIndicesView,
            LongIndex1D numParticles)
        {
            var sharedBestResult = SharedMemory.GetDynamic<TempResult>();
            Debug.Assert(
                sharedBestResult.IntLength % Warp.WarpSize == 0,
                "Shared memory size not sufficient for reduction");

            // Initialize shared memory
            for (int i = Group.IdxX; i < sharedBestResult.IntLength; i += Group.DimX)
                sharedBestResult[i] = TempResult.Invalid;
            Group.Barrier();

            // Iterate over all results and aggregate them
            var localBestResult = TempResult.Invalid;
            for (LongIndex1D resultIndex = Grid.GlobalLinearIndex;
                resultIndex < numParticles;
                resultIndex += GridExtensions.GridStrideLoopStride)
            {
                var result = optimizerFunc.GetEvaluationResult(resultIndex);
                var newResult = new TempResult(result, resultIndex + 2L);
                localBestResult = Utilities.Select(
                    newResult.IsBetterThan(localBestResult),
                    newResult,
                    localBestResult);
            }
            
            // Reduce in each warp
            var warpBestResult = WarpExtensions.Reduce<
                TempResult,
                TempResultReduction>(localBestResult);
            if (Warp.IsFirstLane)
                sharedBestResult[Warp.WarpIdx] = warpBestResult;
            Group.Barrier();

            // Keep the first warp active
            if (Warp.WarpIdx > 0)
                return;

            // Iterate over all results
            var globalBestResult = TempResult.Invalid;
            for (int i = Warp.LaneIdx;
                i < sharedBestResult.IntLength;
                i += Warp.WarpSize)
            {
                var bestResult = sharedBestResult[i];
                var currentWarpBestResult = WarpExtensions.Reduce<
                    TempResult,
                    TempResultReduction>(bestResult);
                if (!Warp.IsFirstLane)
                    continue;
                
                bool currentIsBetter =
                    currentWarpBestResult.IsBetterThan(globalBestResult);
                globalBestResult = Utilities.Select(currentIsBetter,
                    currentWarpBestResult, globalBestResult);
            }
            Warp.Barrier();

            // Write the result in the first lane
            if (Warp.IsFirstLane)
            {
                intermediateResultsView[Grid.IdxX] = globalBestResult.Result;
                intermediateIndicesView[Grid.IdxX] = globalBestResult.ParticleIndex;
            }
        }
        
        /// <summary>
        /// The second pass of the aggregation kernel.
        /// </summary>
        internal static void AggregateEvaluations2Kernel(
            TOptimizerFunc optimizerFunc,
            ArrayView<TEvalType> intermediateResultsView,
            ArrayView<LongIndex1D> intermediateIndicesView,
            SingleVectorView<TNumericType> resultsView,
            VariableView<TEvalType> evalView)
        {
            // Load initial shared best result
            var sharedBestResult = SharedMemory.GetDynamic<TempResult>();
            Debug.Assert(
                sharedBestResult.IntLength % Warp.WarpSize == 0,
                "Shared memory size not sufficient for reduction");
            
            // Initialize shared memory
            var bestKnownResult = new TempResult(evalView.Value, LongIndex1D.One);
            for (int i = Group.IdxX; i < sharedBestResult.IntLength; i += Group.DimX)
                sharedBestResult[i] = bestKnownResult;
            Group.Barrier();

            // Iterate over all results and aggregate them
            var localBestResult = sharedBestResult[0];
            for (Index1D i = Group.IdxX;
                i < intermediateResultsView.IntLength;
                i += Group.DimX)
            {
                var result = new TempResult(
                    intermediateResultsView[i],
                    intermediateIndicesView[i]);
                localBestResult = Utilities.Select(
                    result.IsBetterThan(localBestResult),
                    result,
                    localBestResult);
            }
            
            // Reduce in each warp
            var warpBestResult = WarpExtensions.Reduce<
                TempResult,
                TempResultReduction>(localBestResult);
            if (Warp.IsFirstLane)
                sharedBestResult[Warp.WarpIdx] = warpBestResult;
            Group.Barrier();
            
            // Reduce in the first warp
            if (Warp.WarpIdx == 0)
            {
                // Iterate over all results
                var globalBestResult = TempResult.Invalid;
                for (int i = Warp.LaneIdx;
                    i < sharedBestResult.IntLength;
                    i += Warp.WarpSize)
                {
                    var bestResult = sharedBestResult[i];
                    var currentWarpBestResult = WarpExtensions.Reduce<
                        TempResult,
                        TempResultReduction>(bestResult);
                    if (!Warp.IsFirstLane)
                        continue;
                    
                    bool currentIsBetter =
                        currentWarpBestResult.IsBetterThan(globalBestResult);
                    globalBestResult = Utilities.Select(currentIsBetter,
                        currentWarpBestResult, globalBestResult);
                }
                Warp.Barrier();
                
                if (Warp.IsFirstLane)
                    sharedBestResult[0] = globalBestResult;
            }
            
            // Wait for all results
            Group.Barrier();
            
            // Validate whether we have successfully achieved an improvement
            var finalResult = sharedBestResult[0];
            if (!finalResult.IsBetterThan(bestKnownResult) |
                !bestKnownResult.HasPointIndex |
                finalResult.ParticleIndex < 2L)
            {
                // Ignore non-optimal and invalid results
                return;
            }

            // Get the position vector and copy data
            // ! Please note that we have to adjust our particle index by subtracting 2,
            // since we use 0 and 1 to store special cases !
            var position = optimizerFunc.GetPosition(finalResult.ParticleIndex - 2L);
            for (int i = Group.IdxX; i < resultsView.Dimension; i += Group.DimX)
                resultsView[i] = position[i];
            if (Group.IsFirstThread)
                evalView.Value = finalResult.Result;
        }
        
        #endregion
        
        #region Instance
        
        private readonly MemoryBuffer1D<TEvalType, Stride1D.Dense>
            intermediateResultsBuffer;
        private readonly MemoryBuffer1D<LongIndex1D, Stride1D.Dense>
            intermediateIndicesBuffer;

        private LongIndex1D numParticles;

        private readonly Action<
            AcceleratorStream,
            KernelConfig,
            ArrayView<TRandomProvider>,
            BoundsView<TNumericType>,
            SingleVectorView<TNumericType>,
            VariableView<TEvalType>,
            TOptimizerFunc,
            LongIndex1D,
            SpecializedValue<Index1D>> initializeParticles;
        private readonly Action<
            AcceleratorStream,
            KernelConfig,
            TOptimizerFunc,
            TFunc,
            LongIndex1D,
            SpecializedValue<Index1D>> evaluateParticles;
        private readonly Action<
            AcceleratorStream,
            KernelConfig,
            TOptimizerFunc,
            ArrayView<TEvalType>,
            ArrayView<LongIndex1D>,
            LongIndex1D> aggregateEvaluations1;
        private readonly Action<
            AcceleratorStream,
            KernelConfig,
            TOptimizerFunc,
            ArrayView<TEvalType>,
            ArrayView<LongIndex1D>,
            SingleVectorView<TNumericType>,
            VariableView<TEvalType>> aggregateEvaluations2;

        /// <summary>
        /// Creates a new optimizer instance.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="random">The parent random number generator.</param>
        /// <param name="maxNumParticles">The maximum number of particles.</param>
        /// <param name="vectorDimension">The vector dimension.</param>
        protected Optimizer(
            Accelerator accelerator,
            AcceleratorStream stream,
            LongIndex1D maxNumParticles,
            Index1D vectorDimension,
            System.Random random)
            : base(accelerator, stream, maxNumParticles, random)
        {
            // Determine group size
            GroupSize = accelerator.MaxNumThreadsPerMultiprocessor / 2;
            MaxGroupSize = accelerator.MaxNumThreadsPerGroup;
            
            // Allocate buffers
            intermediateResultsBuffer = accelerator.Allocate1D<TEvalType>(
                XMath.DivRoundUp(maxNumParticles, GroupSize));
            intermediateIndicesBuffer = accelerator.Allocate1D<LongIndex1D>(
                XMath.DivRoundUp(maxNumParticles, GroupSize));
            
            // Load kernels
            initializeParticles = accelerator.LoadKernel<
                ArrayView<TRandomProvider>,
                BoundsView<TNumericType>,
                SingleVectorView<TNumericType>,
                VariableView<TEvalType>,
                TOptimizerFunc,
                LongIndex1D,
                SpecializedValue<Index1D>>(InitializeParticlesKernel);
            evaluateParticles = accelerator.LoadKernel<
                TOptimizerFunc,
                TFunc,
                LongIndex1D,
                SpecializedValue<Index1D>>(EvaluateParticlesKernel);
            aggregateEvaluations1 = accelerator.LoadKernel<
                TOptimizerFunc,
                ArrayView<TEvalType>,
                ArrayView<LongIndex1D>,
                LongIndex1D>(AggregateEvaluations1Kernel);
            aggregateEvaluations2 = accelerator.LoadKernel<
                TOptimizerFunc,
                ArrayView<TEvalType>,
                ArrayView<LongIndex1D>,
                SingleVectorView<TNumericType>,
                VariableView<TEvalType>>(AggregateEvaluations2Kernel);
            
            MaxNumParticles = maxNumParticles;
            VectorDimension = vectorDimension;
            numParticles = maxNumParticles;
        }
        
        #endregion
        
        #region Properties

        /// <summary>
        /// Returns the assigned accelerator.
        /// </summary>
        public Accelerator Accelerator => IntermediateResultsView.GetAccelerator();
        
        /// <summary>
        /// Returns the maximum number of supported particles.
        /// </summary>
        public LongIndex1D MaxNumParticles { get; }

        /// <summary>
        /// Gets or sets the number of particles.
        /// </summary>
        public LongIndex1D NumParticles
        {
            get => numParticles;
            set
            {
                VerifyNumParticles(value);
                numParticles = value;
            }
        }
        
        /// <summary>
        /// Returns the vector dimension used.
        /// </summary>
        public Index1D VectorDimension { get; }

        /// <summary>
        /// Returns the intermediate results view.
        /// </summary>
        public ArrayView1D<TEvalType, Stride1D.Dense> IntermediateResultsView =>
            intermediateResultsBuffer.View;

        /// <summary>
        /// Returns the intermediate indices view.
        /// </summary>
        public ArrayView1D<LongIndex1D, Stride1D.Dense> IntermediateIndicesView =>
            intermediateIndicesBuffer.View;
        
        /// <summary>
        /// Returns the group size used for most kernels to achieve max occupancy.
        /// </summary>
        public int GroupSize { get; }
        
        /// <summary>
        /// Returns the maximum group size used for specific kernels to achieve
        /// max occupancy via a single group.
        /// </summary>
        public int MaxGroupSize { get; }
        
        #endregion
        
        #region Methods

        /// <summary>
        /// Verifies teh given number of particles.
        /// </summary>
        /// <param name="numParticles">The number of particles to verify.</param>
        protected virtual void VerifyNumParticles(LongIndex1D numParticles)
        {
            if (numParticles < 1 || numParticles > MaxNumParticles)
                throw new ArgumentOutOfRangeException(nameof(numParticles));
        }

        /// <summary>
        /// Determines the linear grid-size dimension to process particles.
        /// </summary>
        /// <param name="numSlices">The number of kernel launches required.</param>
        /// <returns>The linear grid-size dimension to process particles.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetGridSize(out Index1D numSlices)
        {
            long numChunks = XMath.DivRoundUp(numParticles, GroupSize);
            int maxGridSize = (int)XMath.Min(numChunks, Accelerator.MaxGridSize.X);
            numSlices = (int)XMath.DivRoundUp(numChunks, maxGridSize);
            return maxGridSize;
        }

        /// <summary>
        /// Initializes particles defined by the given optimization functions.
        /// </summary>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="optimizerFunc">The current optimizer function.</param>
        /// <param name="boundsView">The boundary min/max view.</param>
        /// <param name="bestPositionView">The best position view.</param>
        /// <param name="bestResultView">The best result view.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void InitializeParticles(
            AcceleratorStream stream,
            in TOptimizerFunc optimizerFunc,
            BoundsView<TNumericType> boundsView,
            SingleVectorView<TNumericType> bestPositionView,
            VariableView<TEvalType> bestResultView)
        {
            // Determine kernel config
            int gridSize = GetGridSize(out var _);
            
            // Initialize all particles
            initializeParticles(
                stream,
                (gridSize, GroupSize),
                RNGView.SubView(0, NumParticles),
                boundsView,
                bestPositionView,
                bestResultView,
                optimizerFunc,
                NumParticles,
                SpecializedValue.New(VectorDimension));
        }
        
        /// <summary>
        /// Initializes particles defined by the given optimization functions.
        /// </summary>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="optimizerFunc">The current optimizer function.</param>
        /// <param name="func">The objective function.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void EvaluateParticles(
            AcceleratorStream stream,
            in TOptimizerFunc optimizerFunc,
            in TFunc func)
        {
            // Determine kernel config
            int gridSize = GetGridSize(out var _);
            
            // Initialize all particles
            evaluateParticles(
                stream,
                (gridSize, GroupSize),
                optimizerFunc,
                func,
                NumParticles,
                SpecializedValue.New(VectorDimension));
        }
        
        /// <summary>
        /// Aggregates all intermediate particle evaluation results, updates the best
        /// available global result, and propagates particle changes to the 
        /// </summary>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="optimizerFunc">The current optimizer function.</param>
        /// <param name="resultsView">
        /// The vectorized results view to store the best vectorized position.
        /// </param>
        /// <param name="evalView">The evaluation view to store the best result.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AggregateParticleEvaluations(
            AcceleratorStream stream,
            in TOptimizerFunc optimizerFunc,
            SingleVectorView<TNumericType> resultsView,
            VariableView<TEvalType> evalView)
        {
            // Reset eval results
            IntermediateIndicesView.MemSetToZero(stream);
            IntermediateResultsView.MemSetToZero(stream);
            
            // Determine basic grid config
            int gridSize = GetGridSize(out var _);
            
            // Aggregate all evaluations (pass 1)
            KernelConfig kernelConfigPass1 = (
                gridSize,
                GroupSize,
                SharedMemoryConfig.RequestDynamic<TempResult>(GroupSize));
            aggregateEvaluations1(
                stream,
                kernelConfigPass1,
                optimizerFunc,
                IntermediateResultsView,
                IntermediateIndicesView,
                NumParticles);
            
            // Aggregate all evaluations (pass 2)
            KernelConfig kernelConfigPass2 = (
                1,
                MaxGroupSize,
                SharedMemoryConfig.RequestDynamic<TempResult>(MaxGroupSize));
            aggregateEvaluations2(
                stream,
                kernelConfigPass2,
                optimizerFunc,
                IntermediateResultsView,
                IntermediateIndicesView,
                resultsView,
                evalView);
        }

        #endregion
        
        #region IDisposable

        /// <summary>
        /// Frees native intermediate buffers.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                intermediateResultsBuffer.Dispose();
                intermediateIndicesBuffer.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}

#endif
