// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetaOptimizer.Instance.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Random;
using ILGPU.Util;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

#if NET7_0_OR_GREATER

#pragma warning disable CA1000 // No static members on generic types

namespace ILGPU.Algorithms.Optimization.CPU
{
    partial class MetaOptimizer<T, TEvalType>
    {
        /// <summary>
        /// Holds intermediate and run-specific optimizer instances that depend on
        /// objective function and random instances.
        /// </summary>
        /// <typeparam name="TEvaluator">The internal evaluator type.</typeparam>
        /// <typeparam name="TFunction">The objective function type.</typeparam>
        /// <typeparam name="TIntermediate">
        /// The type of all intermediate states during processing.
        /// </typeparam>
        /// <typeparam name="TProcessor">The processor type being used.</typeparam>
        /// <typeparam name="TType">The processor element type.</typeparam>
        /// <typeparam name="TRandom">The random range generator type.</typeparam>
        sealed class RuntimeInstance<
            TEvaluator,
            TFunction,
            TIntermediate,
            TProcessor,
            TType,
            TRandom> : DisposeBase
            where TEvaluator : class, IEvaluator
            where TFunction : IBaseOptimizationFunction<TEvalType>
            where TIntermediate : class
            where TProcessor : struct, IProcessor<TProcessor, TType>
            where TType : unmanaged
            where TRandom : struct, IRandomRangeProvider<T>
        {
            private readonly MetaOptimizer<T, TEvalType> optimizer;
            private readonly TEvaluator evaluator;
            private readonly UpdatePlayers<
                TFunction,
                TProcessor,
                TType,
                TRandom> updatePlayers;

            /// <summary>
            /// Creates a new runtime instance.
            /// </summary>
            /// <param name="parent">The parent optimizer.</param>
            /// <param name="createRandom">
            /// A specialized random provider generator.
            /// </param>
            /// <param name="function">The objective function.</param>
            /// <param name="evaluatorInstance">The evaluator instance.</param>
            public RuntimeInstance(
                MetaOptimizer<T, TEvalType> parent,
                Func<MetaOptimizer<T, TEvalType>, TRandom> createRandom,
                in TFunction function,
                TEvaluator evaluatorInstance)
            {
                optimizer = parent;
                evaluator = evaluatorInstance;
                updatePlayers = new(parent, createRandom, function)
                {
                    BestPosition = evaluator.ResultManager.BestInternalPosition
                };
            }

            /// <summary>
            /// Returns the best result manager.
            /// </summary>
            public ResultManager ResultManager => evaluator.ResultManager;

            /// <summary>
            /// Evaluates all player positions.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EvaluatePlayers(ParallelOptions options) =>
                evaluator.EvaluatePlayers(options);

            /// <summary>
            /// Updates all player positions.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void UpdatePlayers(ParallelOptions options)
            {
                updatePlayers.ParallelFor(0, optimizer.M, options);

                // Update SOG and SDG information
                updatePlayers.HasCurrentSOGAndSDG = true;
            }

            /// <summary>
            /// Disposes the current evaluator and the specialized update players
            /// instance.
            /// </summary>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    evaluator.Dispose();
                    updatePlayers.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// An instance implementing
        /// </summary>
        /// <typeparam name="TProcessor">The processor type being used.</typeparam>
        /// <typeparam name="TType">The processor element type.</typeparam>
        /// <typeparam name="TRandom">
        /// The random range generator type for scalar types.
        /// </typeparam>
        /// <typeparam name="TTypeRandom">
        /// The random range generator type for specialized processing types.
        /// </typeparam>
        sealed class Instance<
            TProcessor,
            TType,
            TRandom,
            TTypeRandom> : MetaOptimizer<T, TEvalType>
            where TProcessor : struct, IProcessor<TProcessor, TType>
            where TType : unmanaged
            where TRandom : struct, IRandomRangeProvider<T>
            where TTypeRandom : struct, IRandomRangeProvider<TType>
        {
            private readonly ParallelOptions parallelOptions;
            private readonly OGAndDG<TProcessor, TType> ogAndDG;
            private readonly AdjustSOGPlayers<
                TProcessor,
                TType,
                TRandom> adjustSOGPlayers;
            private readonly InitializePlayers<
                TProcessor,
                TType,
                TTypeRandom> initializePlayers;

            private readonly Func<MetaOptimizer<T, TEvalType>, TRandom> getRandom;

            /// <summary>
            /// Creates a new meta optimizer instance.
            /// </summary>
            /// <param name="inputRandom">The input random number generator.</param>
            /// <param name="numPlayers">The number of players to use.</param>
            /// <param name="numDimensions">The dimensionality of the problem.</param>
            /// <param name="maxNumParallelThreads">
            /// The maximum number of parallel processing threads (if any).
            /// </param>
            /// <param name="createRandom">
            /// A function callback to create random range generators for type T.
            /// </param>
            /// <param name="createTTypeRandom">
            /// A function callback to create random range generators for type TType.
            /// </param>
            public Instance(
                System.Random inputRandom,
                int numPlayers,
                int numDimensions,
                int? maxNumParallelThreads,
                Func<MetaOptimizer<T, TEvalType>, TRandom> createRandom,
                Func<MetaOptimizer<T, TEvalType>, TTypeRandom> createTTypeRandom)
                : base(
                    inputRandom,
                    numPlayers,
                    numDimensions,
                    maxNumParallelThreads,
                    TProcessor.Length)
            {
                ogAndDG = new(this);
                adjustSOGPlayers = new(this, createRandom);
                initializePlayers = new(this, createTTypeRandom);

                getRandom = createRandom;

                // Create new parallel options limiting the max degree of parallelism
                parallelOptions = new ParallelOptions()
                {
                    MaxDegreeOfParallelism = MaxNumWorkers,
                };
            }

            /// <summary>
            /// Optimizes the given optimization function while using a specified
            /// break function and initial values for the best result.
            /// </summary>
            /// <typeparam name="TFunction">The optimization function type.</typeparam>
            /// <typeparam name="TIntermediate">
            /// The intermediate optimization state type.
            /// </typeparam>
            /// <typeparam name="TBreakFunction">The break function type.</typeparam>
            /// <typeparam name="TModifier">The position modifier type.</typeparam>
            /// <param name="optimizationFunction">
            /// The optimization function to use.
            /// </param>
            /// <param name="breakFunction">The break function to use.</param>
            /// <param name="positionModifier">
            /// The position modifier to apply to all position updates during
            /// optimization.
            /// </param>
            /// <param name="bestResult">Te best known result.</param>
            /// <param name="bestKnownPosition">The best known position.</param>
            /// <returns>
            /// A tuple consisting of the best result and position found.
            /// </returns>
            public override (TEvalType Result, Memory<T> Position) Optimize<
                TFunction,
                TIntermediate,
                TBreakFunction,
                TModifier>(
                in TFunction optimizationFunction,
                in TBreakFunction breakFunction,
                in TModifier positionModifier,
                TEvalType bestResult,
                ReadOnlyMemory<T>? bestKnownPosition = default)
            {
                // Create new evaluator based on the given optimization function
                var evaluator = new Evaluator<TFunction, TIntermediate, TModifier>(
                    this,
                    optimizationFunction,
                    positionModifier,
                    bestResult,
                    bestKnownPosition);

                // Create a new runtime instance to track all instances for this run
                using var runtimeInstance = new RuntimeInstance<
                    Evaluator<TFunction, TIntermediate, TModifier>,
                    TFunction,
                    TIntermediate,
                    TProcessor,
                    TType,
                    TRandom>(
                    this,
                    getRandom,
                    optimizationFunction,
                    evaluator);

                // Perform optimization
                OptimizeInternal(breakFunction, runtimeInstance);

                // Load best result information
                var resultManager = runtimeInstance.ResultManager;
                return (resultManager.BestResult, resultManager.BestPosition);
            }

            public override (TEvalType Result, Memory<T> Position) OptimizeRaw(
                RawCPUOptimizationFunction<T, TEvalType> optimizationFunction,
                CPUOptimizationBreakFunction<TEvalType> breakFunction,
                CPUEvaluationComparison<TEvalType> evaluationComparison,
                TEvalType bestResult,
                ReadOnlyMemory<T>? bestKnownPosition = default)
            {
                // Create new evaluator based on the given optimization function
                var evaluator = new RawEvaluator(
                    this,
                    optimizationFunction,
                    evaluationComparison,
                    bestResult,
                    bestKnownPosition);

                // Create our raw function wrapper
                var wrapper = new RawComparisonWrapper(evaluationComparison);

                // Create a new runtime instance to track all instances for this run
                using var runtimeInstance = new RuntimeInstance<
                    RawEvaluator,
                    RawComparisonWrapper,
                    object,
                    TProcessor,
                    TType,
                    TRandom>(
                    this,
                    getRandom,
                    wrapper,
                    evaluator);

                // Perform optimization
                var breakFunctionWrapper = new BreakFunctionWrapper(breakFunction);
                OptimizeInternal(breakFunctionWrapper, runtimeInstance);

                // Load best result information
                var resultManager = runtimeInstance.ResultManager;
                return (resultManager.BestResult, resultManager.BestPosition);
            }

            /// <summary>
            /// The internal optimizer loop which used the SGO algorithm to adjust
            /// player/particle positions according to the objective functions and the
            /// update parameters defined.
            /// </summary>
            /// <param name="breakFunction">The break function to use.</param>
            /// <param name="runtimeInstance">
            /// The current runtime instance holding all temporary instances.
            /// </param>
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            private void OptimizeInternal<
                TFunction,
                TIntermediate,
                TBreakFunction,
                TEvaluator>(
                in TBreakFunction breakFunction,
                RuntimeInstance<
                    TEvaluator,
                    TFunction,
                    TIntermediate,
                    TProcessor,
                    TType,
                    TRandom> runtimeInstance)
                where TEvaluator : class, IEvaluator
                where TFunction : IBaseOptimizationFunction<TEvalType>
                where TIntermediate : class
                where TBreakFunction : ICPUOptimizationBreakFunction<TEvalType>
            {
                // Update internal references
                adjustSOGPlayers.BestPosition =
                    runtimeInstance.ResultManager.BestInternalPosition;

                // Initialize all players
                initializePlayers.ParallelFor(0, NumPlayers, parallelOptions);

                // Evaluate all players first
                runtimeInstance.EvaluatePlayers(parallelOptions);

                // Enter actual optimizer loop
                for (int iteration = 0; ; ++iteration)
                {
                    // Permute all indices in the beginning
                    Permute();

                    // Copy positions to new versions
                    CopyPositions();

                    // Initialize all SOG information
                    InitSOGList();

                    // Compute OG and DG information
                    ogAndDG.ParallelFor(0, M, parallelOptions);

                    // Update all players
                    runtimeInstance.UpdatePlayers(parallelOptions);

                    // Update SOG adjustments
                    if (iteration > 0)
                        adjustSOGPlayers.ParallelFor(0, sogListCounter, parallelOptions);

                    // Finally, swap all buffers
                    SwapBuffers();

                    // Evaluate all players
                    runtimeInstance.EvaluatePlayers(parallelOptions);

                    // Check for user-defined break predicates
                    if (breakFunction.Break(
                        runtimeInstance.ResultManager.BestResult,
                        iteration))
                    {
                        break;
                    }
                }
            }

            #region IDisposable

            /// <summary>
            /// Disposes internal parallel cache instances.
            /// </summary>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    ogAndDG.Dispose();
                    adjustSOGPlayers.Dispose();
                    initializePlayers.Dispose();
                }

                base.Dispose(disposing);
            }

            #endregion
        }

        /// <summary>
        /// Creates a new meta optimizer using non-vectorized scalar operations.
        /// </summary>
        /// <typeparam name="TRandom">The random range provider type to use.</typeparam>
        /// <param name="inputRandom">The input random number generator.</param>
        /// <param name="numPlayers">
        /// The number of players to use (must be at least two and an even number).
        /// </param>
        /// <param name="numDimensions">
        /// The number of dimensions (must be greater than one).
        /// </param>
        /// <param name="maxNumParallelThreads">
        /// The maximum number of parallel threads (if any). Not providing a specific
        /// number of threads means using as many threads as possible.
        /// </param>
        /// <returns>The created meta optimizer instance.</returns>
        public static MetaOptimizer<T, TEvalType> CreateScalar<TRandom>(
            System.Random inputRandom,
            int numPlayers,
            int numDimensions,
            int? maxNumParallelThreads = null)
            where TRandom : struct, IRandomRangeProvider<TRandom, T>
        {
            // Creates new random range generators using the scalar type T
            TRandom CreateRandom(MetaOptimizer<T, TEvalType> parent) =>
                TRandom.Create(parent.random, T.Zero, T.One);

            return new Instance<ScalarProcessor, T, TRandom, TRandom>(
                inputRandom,
                numPlayers,
                numDimensions,
                maxNumParallelThreads,
                CreateRandom,
                CreateRandom);
        }

        /// <summary>
        /// Creates a new meta optimizer using vectorized operations.
        /// </summary>
        /// <typeparam name="TRandom">The random range provider type to use.</typeparam>
        /// <param name="inputRandom">The input random number generator.</param>
        /// <param name="numPlayers">
        /// The number of players to use (must be at least two and an even number).
        /// </param>
        /// <param name="numDimensions">
        /// The number of dimensions (must be greater than one).
        /// </param>
        /// <param name="maxNumParallelThreads">
        /// The maximum number of parallel threads (if any). Not providing a specific
        /// number of threads means using as many threads as possible.
        /// </param>
        /// <returns>The created meta optimizer instance.</returns>
        public static MetaOptimizer<T, TEvalType> CreateVectorized<TRandom>(
            System.Random inputRandom,
            int numPlayers,
            int numDimensions,
            int? maxNumParallelThreads = null)
            where TRandom : struct, IRandomRangeProvider<TRandom, T>
        {
            // Creates new random range generators using the scalar type T
            TRandom CreateRandom(MetaOptimizer<T, TEvalType> parent) =>
                TRandom.Create(parent.random, T.Zero, T.One);

            // Creates new random range generators using the vectorized type TType
            RandomRangeVectorProvider<T, TRandom> CreateVectorizedRandom(
                MetaOptimizer<T, TEvalType> parent) =>
                CreateRandom(parent).CreateVectorProvider();

            return new Instance<
                VectorizedProcessor,
                Vector<T>,
                TRandom,
                RandomRangeVectorProvider<T, TRandom>>(
                inputRandom,
                numPlayers,
                numDimensions,
                maxNumParallelThreads,
                CreateRandom,
                CreateVectorizedRandom);
        }
    }

    /// <summary>
    /// A static helper class for <see cref="MetaOptimizer{T,TEvalType}"/> instances.
    /// </summary>
    public static class MetaOptimizer
    {
        #region Static

        /// <summary>
        /// Creates a new meta optimizer using non-vectorized scalar operations.
        /// </summary>
        /// <typeparam name="T">
        /// The main element type for all position vectors.
        /// </typeparam>
        /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
        /// <typeparam name="TRandom">The random range provider type to use.</typeparam>
        /// <param name="inputRandom">The input random number generator.</param>
        /// <param name="numPlayers">
        /// The number of players to use (must be at least two and an even number).
        /// </param>
        /// <param name="numDimensions">
        /// The number of dimensions (must be greater than one).
        /// </param>
        /// <param name="maxNumParallelThreads">
        /// The maximum number of parallel threads (if any). Not providing a specific
        /// number of threads means using as many threads as possible.
        /// </param>
        /// <returns>The created meta optimizer instance.</returns>
        public static MetaOptimizer<T, TEvalType> CreateScalar<T, TEvalType, TRandom>(
            System.Random inputRandom,
            int numPlayers,
            int numDimensions,
            int? maxNumParallelThreads = null)
            where T : unmanaged, INumber<T>
            where TEvalType : struct, IEquatable<TEvalType>
            where TRandom : struct, IRandomRangeProvider<TRandom, T> =>
            MetaOptimizer<T, TEvalType>.CreateScalar<TRandom>(
                inputRandom,
                numPlayers,
                numDimensions,
                maxNumParallelThreads);

        /// <summary>
        /// Creates a new meta optimizer using vectorized operations.
        /// </summary>
        /// <typeparam name="T">
        /// The main element type for all position vectors.
        /// </typeparam>
        /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
        /// <typeparam name="TRandom">The random range provider type to use.</typeparam>
        /// <param name="inputRandom">The input random number generator.</param>
        /// <param name="numPlayers">
        /// The number of players to use (must be at least two and an even number).
        /// </param>
        /// <param name="numDimensions">
        /// The number of dimensions (must be greater than one).
        /// </param>
        /// <param name="maxNumParallelThreads">
        /// The maximum number of parallel threads (if any). Not providing a specific
        /// number of threads means using as many threads as possible.
        /// </param>
        /// <returns>The created meta optimizer instance.</returns>
        public static MetaOptimizer<T, TEvalType> CreateVectorized<
            T,
            TEvalType,
            TRandom>(
            System.Random inputRandom,
            int numPlayers,
            int numDimensions,
            int? maxNumParallelThreads = null)
            where T : unmanaged, INumber<T>
            where TEvalType : struct, IEquatable<TEvalType>
            where TRandom : struct, IRandomRangeProvider<TRandom, T> =>
            MetaOptimizer<T, TEvalType>.CreateVectorized<TRandom>(
                inputRandom,
                numPlayers,
                numDimensions,
                maxNumParallelThreads);

        #endregion
    }
}

#pragma warning restore CA1000

#endif
