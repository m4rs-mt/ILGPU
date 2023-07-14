// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: IOptimizer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Vectors;
using ILGPU.Runtime;
using System;
using System.Numerics;

#pragma warning disable CA1814 // Use jagged arrays if possible

#if NET7_0_OR_GREATER

namespace ILGPU.Algorithms.Optimization
{
    /// <summary>
    /// An abstract optimizer instance that can be used in combination with specific
    /// optimization functions.
    /// </summary>
    /// <typeparam name="TNumericType">The vectorized numeric type.</typeparam>
    /// <typeparam name="TElementType">The element type of a numeric type.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    public interface IOptimizer<TNumericType, TElementType, TEvalType> : IDisposable
        where TNumericType : unmanaged, IVectorType<TNumericType, TElementType>
        where TElementType : unmanaged, INumber<TElementType>
        where TEvalType : unmanaged, IEquatable<TEvalType>
    {
        /// <summary>
        /// Returns the parent optimization engine.
        /// </summary>
        OptimizationEngine<TNumericType, TElementType, TEvalType> Engine { get; }

        /// <summary>
        /// Starts a new optimization process.
        /// </summary>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="bestPosition">The best position vector.</param>
        /// <param name="bestResult">The best result.</param>
        /// <param name="resultView">The target result view to use.</param>
        /// <param name="evalView">The target evaluation view to use.</param>
        void Start(
            AcceleratorStream stream,
            ReadOnlySpan<TElementType> bestPosition,
            TEvalType bestResult,
            SingleVectorView<TNumericType> resultView,
            VariableView<TEvalType> evalView);

        /// <summary>
        /// Performs an optimizer iteration on the current data.
        /// </summary>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="resultView">The target result view to use.</param>
        /// <param name="evalView">The target evaluation view to use.</param>
        /// <param name="iteration">The iteration index of the current iteration.</param>
        void Iteration(
            AcceleratorStream stream,
            SingleVectorView<TNumericType> resultView,
            VariableView<TEvalType> evalView,
            int iteration);

        /// <summary>
        /// Finishes an optimization process.
        /// </summary>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="resultView">The target result view to use.</param>
        /// <param name="evalView">The target evaluation view to use.</param>
        void Finish(
            AcceleratorStream stream,
            SingleVectorView<TNumericType> resultView,
            VariableView<TEvalType> evalView);

        /// <summary>
        /// Begins an optimization run.
        /// </summary>
        /// <param name="stream">The currently accelerator stream.</param>
        /// <param name="bestPosition">The currently known best position.</param>
        /// <param name="bestResult">The currently known best result.</param>
        /// <returns>
        /// An optimization run instance to control the optimization process.
        /// </returns>
        OptimizerRun<TNumericType, TElementType, TEvalType> BeginOptimization(
            AcceleratorStream stream,
            ReadOnlySpan<TElementType> bestPosition,
            TEvalType bestResult)
        {
            double elapsed = Engine.BeginOptimization(
                stream,
                bestPosition,
                bestResult,
                this);
            return new(stream, this, elapsed);
        }

        /// <summary>
        /// Performs a full optimization run without creating an
        /// <see cref="OptimizerRun{TNumericType,TElementType,TEvalType}"/> instance.
        /// </summary>
        /// <param name="stream">The currently accelerator stream.</param>
        /// <param name="bestPosition">The currently known best position.</param>
        /// <param name="bestResult">The currently known best result.</param>
        /// <param name="maxNumIterations">
        /// The maximum number of iterations to perform.
        /// </param>
        /// <param name="maxTime">
        /// The maximum time allowed to elapse until a result needs to be available.
        /// </param>
        /// <returns>A result view pointing to results living in GPU memory.</returns>
        OptimizationResultView<TElementType, TEvalType> Optimize(
            AcceleratorStream stream,
            ReadOnlySpan<TElementType> bestPosition,
            TEvalType bestResult,
            int maxNumIterations,
            TimeSpan? maxTime = null)
        {
            double maxTimeInMilliseconds =
                (maxTime ?? TimeSpan.MaxValue).TotalMilliseconds;

            // Start optimization
            var run = BeginOptimization(stream, bestPosition, bestResult);

            // Iterate until completion
            for (int i = 0;
                i < maxNumIterations && run.ElapsedTime < maxTimeInMilliseconds;
                ++i)
            {
                run.Step();
            }

            // Finish optimization
            return run.Finish();
        }

        /// <summary>
        /// Performs a full optimization run without creating an
        /// <see cref="OptimizerRun{TNumericType,TElementType,TEvalType}"/> instance.
        /// Note that this function fetches all results back into CPU memory (async).
        /// </summary>
        /// <param name="stream">The currently accelerator stream.</param>
        /// <param name="bestPosition">The currently known best position.</param>
        /// <param name="bestResult">The currently known best result.</param>
        /// <param name="maxNumIterations">
        /// The maximum number of iterations to perform.
        /// </param>
        /// <param name="maxTime">
        /// The maximum time allowed to elapse until a result needs to be available.
        /// </param>
        /// <returns>
        /// A result object containing CPU views to the actual results being computed
        /// asynchronously on the device.
        /// </returns>
        OptimizationResult<TElementType, TEvalType> OptimizeToCPUAsync(
            AcceleratorStream stream,
            ReadOnlySpan<TElementType> bestPosition,
            TEvalType bestResult,
            int maxNumIterations,
            TimeSpan? maxTime = null)
        {
            var resultView = Optimize(
                stream,
                bestPosition,
                bestResult,
                maxNumIterations,
                maxTime);
            return Engine.FetchToCPUAsync(stream, resultView);
        }
    }

    /// <summary>
    /// Represents a single optimization run.
    /// </summary>
    /// <typeparam name="TNumericType">The vectorized numeric type.</typeparam>
    /// <typeparam name="TElementType">The element type of a numeric type.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    public sealed class OptimizerRun<TNumericType, TElementType, TEvalType>
        where TNumericType : unmanaged, IVectorType<TNumericType, TElementType>
        where TElementType : unmanaged, INumber<TElementType>
        where TEvalType : unmanaged, IEquatable<TEvalType>
    {
        /// <summary>
        /// Constructs a new optimization run.
        /// </summary>
        /// <param name="stream">The current stream being used.</param>
        /// <param name="optimizer">The parent optimizer instance.</param>
        /// <param name="elapsedTime">The currently known elapsed time.</param>
        public OptimizerRun(
            AcceleratorStream stream,
            IOptimizer<TNumericType, TElementType, TEvalType> optimizer,
            double elapsedTime)
        {
            Stream = stream;
            Optimizer = optimizer;
            ElapsedTime = elapsedTime;
        }

        /// <summary>
        /// Returns the parent accelerator stream.
        /// </summary>
        public AcceleratorStream Stream { get; }

        /// <summary>
        /// Returns the underlying optimizer instance.
        /// </summary>
        public IOptimizer<TNumericType, TElementType, TEvalType> Optimizer { get; }

        /// <summary>
        /// Returns the parent optimization engine.
        /// </summary>
        public OptimizationEngine<TNumericType, TElementType, TEvalType> Engine =>
            Optimizer.Engine;

        /// <summary>
        /// Returns the elapsed time until now.
        /// </summary>
        public double ElapsedTime { get; private set; }

        /// <summary>
        /// Returns the current iteration.
        /// </summary>
        public int Iteration { get; private set; }

        /// <summary>
        /// Loads all particle positions into the given page locked memory.
        /// </summary>
        /// <param name="data">The data to load the particles to.</param>
        public void LoadParticles(PageLockedArray2D<TNumericType> data) =>
            Optimizer.Engine.PositionsView.DataView.CopyToPageLockedAsync(Stream, data);

        /// <summary>
        /// Loads all particle positions and converts them into a 2D array.
        /// </summary>
        /// <remarks>Caution as this operation can be slow.</remarks>
        /// <returns>The loaded 2D array of particle positions.</returns>
        public TNumericType[,] LoadParticles() =>
            Optimizer.Engine.PositionsView.DataView.GetAsArray2D(Stream);

        /// <summary>
        /// Performs a single optimization step.
        /// </summary>
        public void Step()
        {
            ElapsedTime += Optimizer.Engine.OptimizationStep(
                Stream,
                Optimizer,
                Iteration++);
        }

        /// <summary>
        /// Finishes the current optimization run.
        /// </summary>
        /// <returns>The optimization result view.</returns>
        public OptimizationResultView<TElementType, TEvalType> Finish()
        {
            var result = Engine.FinishOptimization(
                Stream,
                Optimizer,
                ElapsedTime);
            ElapsedTime = result.ElapsedTime;
            return result;
        }

        /// <summary>
        /// Finishes the current optimization run while loading all results to the CPU.
        /// </summary>
        /// <returns>The optimization result accessible from the CPU side..</returns>
        public OptimizationResult<TElementType, TEvalType> FinishToCPUAsync()
        {
            var resultView = Finish();
            return Engine.FetchToCPUAsync(Stream, resultView);
        }
    }
}

#endif

#pragma warning restore CA1814
