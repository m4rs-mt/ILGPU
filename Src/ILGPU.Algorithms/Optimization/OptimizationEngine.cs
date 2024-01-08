// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: OptimizationEngine.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Algorithms.Vectors;
using ILGPU.Runtime;
using ILGPU.Util;
using ILGPU;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;

#if NET7_0_OR_GREATER

namespace ILGPU.Algorithms.Optimization
{
    /// <summary>
    /// Represents an abstract optimization engine supporting optimized memory layouts,
    /// custom operations to convert from and to vectorized representations used by this
    /// engine, and manages utility memory for optimizers operating on particles.
    /// In addition, it operates on multidimensional data supporting arbitrary problem
    /// descriptions and domains based on custom numeric vectors and types.
    /// </summary>
    /// <typeparam name="TNumericType">The vectorized numeric type.</typeparam>
    /// <typeparam name="TElementType">The element type of a numeric type.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    public abstract class OptimizationEngine<TNumericType, TElementType, TEvalType>
        : DisposeBase
        where TNumericType : unmanaged, IVectorType<TNumericType, TElementType>
        where TElementType : unmanaged, INumber<TElementType>
        where TEvalType : unmanaged, IEquatable<TEvalType>
    {
        #region Kernels
        
        /// <summary>
        /// Converts non-vectorized vector views into vectorized ones.
        /// </summary>
        internal static void ConvertToVectorizedViewKernel(
            Index1D index,
            SingleVectorView<TElementType> sourceView,
            SingleVectorView<TNumericType> targetView)
        {
            targetView[index] = TNumericType.FromElementView(
                sourceView,
                index * TNumericType.Length);
        }
        
        /// <summary>
        /// Converts vectorized vector views into non-vectorized ones.
        /// </summary>
        internal static void ConvertFromVectorizedViewKernel(
            Index1D index,
            SingleVectorView<TNumericType> sourceView,
            SingleVectorView<TElementType> targetView)
        {
            var sourceValue = sourceView[index];
            sourceValue.ToElementView(targetView, index * TNumericType.Length);
        }
        
        #endregion

        #region Instance

        private readonly TElementType[] stagingElements;
        private readonly PageLockedArray1D<TElementType> resultElementBufferCPU;
        private readonly PageLockedArray2D<TElementType> boundsElementBufferCPU;
        private readonly PageLockedArray1D<TEvalType> resultEvalBufferCPU;
        private readonly PageLockedArray1D<TElementType> parametersCPU;
        
        private readonly MemoryBuffer2D<TNumericType, Stride2D.DenseY> positionsBuffer;
        private readonly MemoryBuffer1D<TEvalType, Stride1D.Dense> evalBuffer;
        
        private readonly MemoryBuffer1D<long, Stride1D.Dense> maskBuffer;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> prefixSumBuffer;
        
        private readonly MemoryBuffer1D<TElementType, Stride1D.Dense> resultBuffer;
        private readonly ArrayView<TElementType> resultElementView;

        private readonly MemoryBuffer1D<TNumericType, Stride1D.Dense>
            resultVectorizedBuffer;
        private readonly SingleVectorView<TNumericType> resultVectorizedView;

        private readonly MemoryBuffer1D<TEvalType, Stride1D.Dense> resultEvalBuffer;
        private readonly VariableView<TEvalType> resultEvalView;
        
        private readonly MemoryBuffer2D<TNumericType, Stride2D.DenseY>
            boundsVectorizedBuffer;
        private readonly MemoryBuffer2D<TElementType, Stride2D.DenseY>
            boundsElementBuffer;
        private readonly VectorView<TNumericType> boundsVectorizedView;
        private readonly VectorView<TElementType> boundsElementView;

        private readonly Scan<long, Stride1D.Dense, Stride1D.Dense> scan;

        private readonly Stopwatch stopwatch;
        private int locked;

        private readonly Action<
            AcceleratorStream,
            Index1D,
            SingleVectorView<TElementType>,
            SingleVectorView<TNumericType>> convertToVectorizedView;
        private readonly Action<
            AcceleratorStream,
            Index1D,
            SingleVectorView<TNumericType>,
            SingleVectorView<TElementType>> convertFromVectorizedView;

        /// <summary>
        /// Constructs a new optimization engine.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="maxNumParticles">
        /// The maximum number of supported particles.
        /// </param>
        /// <param name="numParameters">The number of optimizer parameters.</param>
        /// <param name="dimension">
        /// The dimensionality of the optimization problem.
        /// </param>
        protected OptimizationEngine(
            Accelerator accelerator,
            LongIndex1D maxNumParticles,
            int numParameters,
            int dimension)
        {
            if (dimension % TNumericType.Length != 0)
                throw new ArgumentOutOfRangeException(nameof(dimension));
            int vectorDimension = dimension / TNumericType.Length;

            stagingElements = new TElementType[dimension];
            resultElementBufferCPU = accelerator.AllocatePageLocked1D<TElementType>(
                dimension);
            boundsElementBufferCPU = accelerator.AllocatePageLocked2D<TElementType>(
                (dimension, 2));
            resultEvalBufferCPU = accelerator.AllocatePageLocked1D<TEvalType>(1);
            parametersCPU = accelerator.AllocatePageLocked1D<TElementType>(
                numParameters);
            stopwatch = new Stopwatch();

            positionsBuffer = VectorView<TNumericType>.Allocate(
                accelerator,
                maxNumParticles,
                dimension);
            evalBuffer = accelerator.Allocate1D<TEvalType>(maxNumParticles);
            maskBuffer = accelerator.Allocate1D<long>(maxNumParticles * 2L);
            var tempBufferLength = accelerator.ComputeScanTempStorageSize<LongIndex1D>(
                maxNumParticles);
            prefixSumBuffer = accelerator.Allocate1D<int>(tempBufferLength);

            resultBuffer = accelerator.Allocate1D<TElementType>(dimension);
            resultElementView = resultBuffer.View;

            boundsVectorizedBuffer = VectorView<TNumericType>.Allocate(
                accelerator,
                2L,
                vectorDimension);
            boundsElementBuffer = VectorView<TElementType>.Allocate(
                accelerator,
                2L,
                dimension);
            boundsVectorizedView = boundsVectorizedBuffer.View;
            boundsElementView = boundsElementBuffer.View;

            resultVectorizedBuffer = accelerator.Allocate1D<TNumericType>(
                vectorDimension);
            resultVectorizedView = resultVectorizedBuffer.View.AsGeneral();

            resultEvalBuffer = accelerator.Allocate1D<TEvalType>(1);
            resultEvalView = resultEvalBuffer.View.VariableView(0);

            // Load kernels
            convertToVectorizedView = accelerator.LoadAutoGroupedKernel<
                Index1D,
                SingleVectorView<TElementType>,
                SingleVectorView<TNumericType>>(
                ConvertToVectorizedViewKernel);
            convertFromVectorizedView = accelerator.LoadAutoGroupedKernel<
                Index1D,
                SingleVectorView<TNumericType>,
                SingleVectorView<TElementType>>(
                ConvertFromVectorizedViewKernel);
            scan = accelerator.CreateScan<
                long,
                Stride1D.Dense,
                Stride1D.Dense,
                AddInt64>(ScanKind.Exclusive);

            Accelerator = accelerator;
            MaxNumParticles = maxNumParticles;
            NumParameters = numParameters;
        }
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Returns the current accelerator.
        /// </summary>
        public Accelerator Accelerator { get; }
        
        /// <summary>
        /// Returns the supported vector dimension of this optimizer.
        /// </summary>
        public int Dimension => (int)resultElementBufferCPU.Length;

        /// <summary>
        /// Returns the number of vector elements.
        /// </summary>
        public int VectorDimension => Dimension / TNumericType.Length;
        
        /// <summary>
        /// Returns the number of supported parameters.
        /// </summary>
        public int NumParameters { get; }
        
        /// <summary>
        /// Returns the maximum number of particles.
        /// </summary>
        public LongIndex1D MaxNumParticles { get; }
        
        /// <summary>
        /// Returns the current number of particles.
        /// </summary>
        public LongIndex1D NumParticles { get; protected set; }

        /// <summary>
        /// Returns a reference to the i-th parameter.
        /// </summary>
        /// <param name="index">The index of the parameter to address.</param>
        /// <returns>A reference to the i-th parameter.</returns>
        protected ref TElementType GetParameter(int index) => ref parametersCPU[index];

        /// <summary>
        /// Returns the underlying bounds view to control value ranges of individual
        /// variables to optimize.
        /// </summary>
        public BoundsView<TNumericType> BoundsView => new(
            boundsVectorizedView.SliceVector(0),
            boundsVectorizedView.SliceVector(1));

        /// <summary>
        /// Returns the underlying positions view.
        /// </summary>
        protected internal VectorView<TNumericType> PositionsView =>
            positionsBuffer.View;
        
        /// <summary>
        /// Returns the underlying evaluation view.
        /// </summary>
        protected ArrayView<TEvalType> EvaluationsView => evalBuffer.View;

        /// <summary>
        /// Returns the underlying mask view view to enable or disable particles.
        /// </summary>
        protected ArrayView<long> MaskView =>
            maskBuffer.View.SubView(0L, MaxNumParticles);
        
        #endregion
        
        #region Methods

        /// <summary>
        /// Converts a non-vectorized source view into a vectorized view.
        /// </summary>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="sourceView">The source view to convert from.</param>
        /// <param name="targetView">The target view to convert into.</param>
        /// <param name="targetIndex">The index within the target view.</param>
        public void ConvertToVectorView(
            AcceleratorStream stream,
            SingleVectorView<TElementType> sourceView,
            VectorView<TNumericType> targetView,
            int targetIndex)
        {
            var linearTarget = targetView.SliceVector(targetIndex);
            ConvertToVectorizedView(stream, sourceView, linearTarget);
        }
        
        /// <summary>
        /// Converts a non-vectorized source view into a vectorized view.
        /// </summary>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="sourceView">The source view to convert from.</param>
        /// <param name="targetView">The target view to convert into.</param>
        public void ConvertToVectorizedView(
            AcceleratorStream stream,
            SingleVectorView<TElementType> sourceView,
            SingleVectorView<TNumericType> targetView)
        {
            if (!sourceView.IsValid)
                throw new ArgumentNullException(nameof(sourceView));
            if (!targetView.IsValid)
                throw new ArgumentNullException(nameof(targetView));
            if (sourceView.Dimension != targetView.Dimension * TNumericType.Length)
                throw new ArgumentOutOfRangeException(nameof(targetView));
            
            convertToVectorizedView(stream, targetView.Dimension, sourceView, targetView);
        }
        
        /// <summary>
        /// Converts a vectorized source view into a non-vectorized view.
        /// </summary>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="sourceView">The source view to convert from.</param>
        /// <param name="sourceIndex">The index within the source view.</param>
        /// <param name="targetView">The target view to convert into.</param>
        public void ConvertFromVectorView(
            AcceleratorStream stream,
            VectorView<TNumericType> sourceView,
            int sourceIndex,
            ArrayView1D<TElementType, Stride1D.General> targetView)
        {
            var linearSource = sourceView.SliceVector(sourceIndex);
            ConvertFromVectorizedView(stream, linearSource, targetView);
        }
        
        /// <summary>
        /// Converts a vectorized source view into a non-vectorized view.
        /// </summary>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="sourceView">The source view to convert from.</param>
        /// <param name="targetView">The target view to convert into.</param>
        public void ConvertFromVectorizedView(
            AcceleratorStream stream,
            SingleVectorView<TNumericType> sourceView,
            SingleVectorView<TElementType> targetView)
        {
            if (!sourceView.IsValid)
                throw new ArgumentNullException(nameof(sourceView));
            if (!targetView.IsValid)
                throw new ArgumentNullException(nameof(targetView));
            if (sourceView.Dimension * TNumericType.Length != targetView.Dimension)
                throw new ArgumentOutOfRangeException(nameof(targetView));
            
            convertFromVectorizedView(
                stream,
                sourceView.Dimension,
                sourceView,
                targetView);
        }
        
        /// <summary>
        /// Loads optimization bounds from the given GPU-accessible views.
        /// </summary>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="lowerBounds">The view pointing to lower bounds.</param>
        /// <param name="upperBounds">The view pointing to upper bounds.</param>
        public void LoadBounds(
            AcceleratorStream stream,
            ArrayView1D<TElementType, Stride1D.General> lowerBounds,
            ArrayView1D<TElementType, Stride1D.General> upperBounds)
        {
            if (lowerBounds.Length != Dimension)
                throw new ArgumentOutOfRangeException(nameof(lowerBounds));
            if (upperBounds.Length != Dimension)
                throw new ArgumentOutOfRangeException(nameof(upperBounds));
            
            ConvertToVectorizedView(
                stream,
                lowerBounds,
                boundsVectorizedView.SliceVector(0));
            ConvertToVectorizedView(
                stream,
                upperBounds,
                boundsVectorizedView.SliceVector(1));
        }
        
        /// <summary>
        /// Loads optimization bounds from the given readonly spans.
        /// </summary>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="lowerBounds">The span pointing to lower bounds.</param>
        /// <param name="upperBounds">The span pointing to upper bounds.</param>
        public void LoadBounds(
            AcceleratorStream stream,
            ReadOnlySpan<TElementType> lowerBounds,
            ReadOnlySpan<TElementType> upperBounds)
        {
            if (lowerBounds.Length != Dimension)
                throw new ArgumentOutOfRangeException(nameof(lowerBounds));
            if (upperBounds.Length != Dimension)
                throw new ArgumentOutOfRangeException(nameof(upperBounds));

            for (int i = 0; i < Dimension; ++i)
            {
                boundsElementBufferCPU[i, 0] = lowerBounds[i];
                boundsElementBufferCPU[i, 1] = upperBounds[i];
            }
            
            boundsElementView.DataView.BaseView.CopyFrom(
                stream,
                boundsElementBufferCPU.ArrayView);
            
            ConvertToVectorizedView(
                stream,
                boundsElementView.SliceVector(0),
                boundsVectorizedView.SliceVector(0));
            ConvertToVectorizedView(
                stream,
                boundsElementView.SliceVector(1),
                boundsVectorizedView.SliceVector(1));
        }

        /// <summary>
        /// Loads optimization parameters from the given array view.
        /// </summary>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="parameters">
        /// The source array view to load parameters from.
        /// </param>
        public void LoadParameters(
            AcceleratorStream stream,
            ArrayView<TElementType> parameters)
        {
            if (parameters.Length != NumParameters)
                throw new ArgumentOutOfRangeException(nameof(parameters));
            LoadParametersInternal(stream, parameters);
        }

        /// <summary>
        /// Loads optimization parameters from the given span.
        /// </summary>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="parameters">
        /// The source array view to load parameters from.
        /// </param>
        public void LoadParameters(
            AcceleratorStream stream,
            ReadOnlySpan<TElementType> parameters)
        {
            if (parameters.Length != NumParameters)
                throw new ArgumentOutOfRangeException(nameof(parameters));
            LoadParametersFromCPUInternal(stream, parameters);
        }

        /// <summary>
        /// Loads optimization parameters from the given array view.
        /// </summary>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="parameters">
        /// The source array view to load parameters from.
        /// </param>
        protected virtual void LoadParametersInternal(
            AcceleratorStream stream,
            ArrayView<TElementType> parameters)
        {
            parameters.CopyTo(stream, parametersCPU.ArrayView);
            stream.Synchronize();
        }

        /// <summary>
        /// Loads optimization parameters from the given span.
        /// </summary>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="parameters">
        /// The source array view to load parameters from.
        /// </param>
        protected virtual void LoadParametersFromCPUInternal(
            AcceleratorStream stream,
            ReadOnlySpan<TElementType> parameters) =>
            parameters.CopyTo(parametersCPU.Span);
        
        #endregion
        
        #region Optimization Methods

        /// <summary>
        /// Creates optimizer instances configured for this optimization engine.
        /// </summary>
        /// <typeparam name="TFunc">The optimization function type.</typeparam>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="random">The CPU-based source RNG.</param>
        /// <param name="optimizationFunction">The optimization function to use.</param>
        /// <returns>The create optimizer instance.</returns>
        public abstract IOptimizer<TNumericType, TElementType, TEvalType>
            CreateOptimizer<TFunc>(
            AcceleratorStream stream,
            System.Random random,
            in TFunc optimizationFunction)
            where TFunc : struct,
                IOptimizationFunction<TNumericType, TElementType, TEvalType>;

        /// <summary>
        /// Compacts all particles by computing prefix-sum offsets.
        /// </summary>
        /// <param name="stream">The current accelerator stream.</param>
        /// <returns>An offset view computing target offsets.</returns>
        protected internal ArrayView<long> CompactParticles(AcceleratorStream stream)
        {
            var offsetView = maskBuffer.View.SubView(MaxNumParticles, MaxNumParticles);
            scan(stream, MaskView, offsetView, prefixSumBuffer.View);
            return offsetView;
        }
        
        /// <summary>
        /// Begins an optimization process.
        /// </summary>
        /// <param name="stream">The current stream.</param>
        /// <param name="bestPosition">The currently best known position.</param>
        /// <param name="bestResult">The currently best known result.</param>
        /// <param name="optimizer">The optimizer instance.</param>
        /// <returns>The elapsed time.</returns>
        protected internal double BeginOptimization(
            AcceleratorStream stream,
            ReadOnlySpan<TElementType> bestPosition,
            TEvalType bestResult,
            IOptimizer<TNumericType, TElementType, TEvalType> optimizer)
        {
            // Concurrency check
            if (Interlocked.CompareExchange(ref locked, 1, 0) != 0)
                throw new InvalidOperationException();
            
            stopwatch.Restart();

            // Copy to CPU buffers (the first copy is needed to avoid read-write
            // dependencies between multiple runs that could potentially occur)
            bestPosition.CopyTo(stagingElements);
            stagingElements.CopyTo(resultElementBufferCPU.Span);
            resultEvalBufferCPU[0] = bestResult;

            // Copy position and best result to GPU buffers
            resultElementView.CopyFrom(stream, resultElementBufferCPU.ArrayView);
            resultEvalView.BaseView.CopyFrom(stream, resultEvalBufferCPU.ArrayView);

            // Convert our best result view
            ConvertToVectorizedView(
                stream,
                resultElementView.AsGeneral(),
                resultVectorizedView);

            // Begin optimization process
            optimizer.Start(
                stream,
                bestPosition,
                bestResult,
                resultVectorizedView,
                resultEvalView);

            stopwatch.Stop();

            return stopwatch.Elapsed.TotalMilliseconds;
        }
        
        /// <summary>
        /// Performs a single optimization step.
        /// </summary>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="optimizer">The current optimizer instance.</param>
        /// <param name="iteration">The current iteration.</param>
        /// <returns>The elapsed time.</returns>
        protected internal double OptimizationStep(
            AcceleratorStream stream,
            IOptimizer<TNumericType, TElementType, TEvalType> optimizer,
            int iteration)
        {
            stopwatch.Restart();
            optimizer.Iteration(stream, resultVectorizedView, resultEvalView, iteration);
            stopwatch.Stop();

            return stopwatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// Finishes an ongoing optimization run.
        /// </summary>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="optimizer">The current optimizer instance.</param>
        /// <param name="elapsedTime">The elapsed time until now.</param>
        /// <returns>The resulting optimization view.</returns>
        protected internal OptimizationResultView<TElementType, TEvalType>
            FinishOptimization(
            AcceleratorStream stream,
            IOptimizer<TNumericType, TElementType, TEvalType> optimizer,
            double elapsedTime)
        {
            stopwatch.Restart();
            optimizer.Finish(stream, resultVectorizedView, resultEvalView);

            // Massage result vector and extract resulting elements
            ConvertFromVectorizedView(
                stream,
                resultVectorizedView,
                resultElementView.AsGeneral());

            stopwatch.Stop();

            // Free the current optimization pipeline
            Interlocked.Exchange(ref locked, 0);
            
            return new(
                resultEvalView,
                resultElementView,
                elapsedTime + stopwatch.Elapsed.TotalMilliseconds);
        }

        /// <summary>
        /// Fetches the given result view to the internal CPU buffers.
        /// </summary>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="resultView">The source view.</param>
        /// <returns>The CPU-based </returns>
        protected internal OptimizationResult<TElementType, TEvalType> FetchToCPUAsync(
            AcceleratorStream stream,
            OptimizationResultView<TElementType, TEvalType> resultView)
        {
            // Copy result to CPU to group by range of numerical values
            resultView.PositionView.CopyTo(
                stream,
                resultElementBufferCPU.ArrayView);
            resultView.ResultView.BaseView.CopyTo(
                stream,
                resultEvalBufferCPU.ArrayView);

            // Return the actual result
            return new OptimizationResult<TElementType, TEvalType>(
                resultEvalBufferCPU.Span,
                resultElementBufferCPU.Span,
                resultView.ElapsedTime);
        }
        
        #endregion
        
        #region IDisposable
        
        /// <summary>
        /// Frees internal CPU and GPU buffers directly.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                resultElementBufferCPU.Dispose();
                boundsElementBufferCPU.Dispose();
                resultEvalBufferCPU.Dispose();
                parametersCPU.Dispose();
                
                positionsBuffer.Dispose();
                evalBuffer.Dispose();
                maskBuffer.Dispose();
                prefixSumBuffer.Dispose();
                
                resultBuffer.Dispose();
                boundsVectorizedBuffer.Dispose();
                boundsElementBuffer.Dispose();
                resultVectorizedBuffer.Dispose();
                resultEvalBuffer.Dispose();
            }
            base.Dispose(disposing);
        }
        
        #endregion
    }
}

#endif
