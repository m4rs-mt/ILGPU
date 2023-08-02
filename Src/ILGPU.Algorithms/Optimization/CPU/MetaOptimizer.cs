// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetaOptimizer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

#if NET7_0_OR_GREATER

namespace ILGPU.Algorithms.Optimization.CPU
{
    /// <summary>
    /// This meta optimizer is designed for CPUs and used special .Net features for
    /// improved performance. It implements an optimization-performance and runtime-
    /// performance optimized version of the SGO algorithm:
    /// Squid Game Optimizer (SGO): a novel metaheuristic algorithm
    /// doi: 10.1038/s41598-023-32465-z.
    /// </summary>
    /// <typeparam name="T">The main element type for all position vectors.</typeparam>
    /// <typeparam name="TEvalType">The evaluation data type.</typeparam>
    /// <remarks>
    /// This version *does not* implement the vanilla SGO algorithm from the paper.
    /// Instead, it uses modified update functions and specially tweaked position update
    /// logic using multiple buffers and tuned SGO-winner lists. These modifications of
    /// the original algorithm make this implementation significantly better in terms of
    /// optimization quality and runtime performance. Moreover, this version is fully
    /// parallelized and has the ability to use SIMD vector instructions to improve
    /// runtime performance.
    /// </remarks>
    public abstract partial class MetaOptimizer<T, TEvalType> : DisposeBase
        where T : unmanaged, INumber<T>
        where TEvalType : struct, IEquatable<TEvalType>
    {
        #region Nested Types

        /// <summary>
        /// A scalar or vectorized processor implementing the actual SGO equations.
        /// </summary>
        /// <typeparam name="TSelf">The implementing processor type.</typeparam>
        /// <typeparam name="TType">The operating element type.</typeparam>
        private interface IProcessor<TSelf, TType>
            where TSelf : struct, IProcessor<TSelf, TType>
            where TType : unmanaged
        {
            /// <summary>
            /// Creates a new processor instance.
            /// </summary>
            static abstract TSelf New();

            /// <summary>
            /// Returns the number of elements processed in single step.
            /// </summary>
            static abstract int Length { get; }

            /// <summary>
            /// Resets the given data view.
            /// </summary>
            void Reset(out TType data);

            /// <summary>
            /// Adds the given source to the target view.
            /// </summary>
            /// <param name="target">The target span to accumulate into.</param>
            /// <param name="source">The source span.</param>
            void Accumulate(ref TType target, TType source);

            /// <summary>
            /// Clamps the given value.
            /// </summary>
            /// <param name="lower">The lower bounds part.</param>
            /// <param name="upper">The upper bounds part.</param>
            /// <param name="value">The value to clamp.</param>
            TType Clamp(TType lower, TType upper, TType value);

            /// <summary>
            /// Computes the average by taking the given count into account.
            /// </summary>
            /// <param name="target">The target span to read from and write to.</param>
            /// <param name="count">The number of points to consider.</param>
            void ComputeAverage(ref TType target, T count);

            /// <summary>
            /// Determines a newly sampled random position within the bounds of lower
            /// and upper values.
            /// </summary>
            /// <param name="lower">The lower bounds of the position vector.</param>
            /// <param name="upper">The upper bounds of the position vector.</param>
            /// <param name="randomNumber">The random number to use.</param>
            /// <returns>The newly sampled position.</returns>
            TType GetRandomPosition(
                TType lower,
                TType upper,
                TType randomNumber);

            /// <summary>
            /// Determines a newly sampled position.
            /// </summary>
            /// <param name="position">The source position.</param>
            /// <param name="firstC">The first centroid position.</param>
            /// <param name="secondC">The second centroid position.</param>
            /// <param name="r1">
            /// The factor describing the influence of <paramref name="firstC"/>.
            /// </param>
            /// <param name="r2">
            /// The factor describing the influence of <paramref name="secondC"/>.
            /// </param>
            /// <param name="stepSize">
            /// The step size to use for offset computations.
            /// </param>
            /// <returns>The newly determined position.</returns>
            TType DetermineNewPosition(
                TType position,
                TType firstC,
                TType secondC,
                T r1,
                T r2,
                T stepSize);
        }

        /// <summary>
        /// A specialized function wrapper implementing the required CPUOptimization
        /// interfaces to call delegate functions instead of having inline function
        /// specifications.
        /// </summary>
        /// <param name="EvalFunction">The evaluation function to be used.</param>
        /// <param name="EvaluationComparison">
        /// The function determining whether the first or the second evaluation value
        /// given is considered better for the optimization problem.
        /// </param>
        /// <param name="BreakFunction">
        /// The break function to determine whether to break the solver iteration or not.
        /// </param>
        private readonly record struct FunctionWrapper(
            CPUOptimizationFunction<T, TEvalType> EvalFunction,
            CPUOptimizationBreakFunction<TEvalType> BreakFunction,
            CPUEvaluationComparison<TEvalType> EvaluationComparison) :
            ICPUOptimizationFunction<T, TEvalType>,
            ICPUOptimizationBreakFunction<TEvalType>
        {
            /// <summary>
            /// Immediately calls the given evaluation function.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TEvalType Evaluate(ReadOnlySpan<T> position) =>
                EvalFunction(position);

            /// <summary>
            /// Immediately calls the given break function.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Break(TEvalType evalType, int iteration) =>
                BreakFunction(evalType, iteration);

            /// <summary>
            /// Immediately calls the given result comparison function.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool CurrentIsBetter(TEvalType current, TEvalType proposed) =>
                EvaluationComparison(current, proposed);
        }

        /// <summary>
        /// A specialized function wrapper implementing the required CPUOptimization
        /// interfaces to test whether to break an optimization loop or not.
        /// </summary>
        /// <param name="BreakFunction">
        /// The break function to determine whether to break the solver iteration or not.
        /// </param>
        private readonly record struct BreakFunctionWrapper(
            CPUOptimizationBreakFunction<TEvalType> BreakFunction) :
            ICPUOptimizationBreakFunction<TEvalType>
        {
            /// <summary>
            /// Immediately calls the given break function.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Break(TEvalType evalType, int iteration) =>
                BreakFunction(evalType, iteration);
        }

        /// <summary>
        /// Wraps a non-intermediate-state-based optimization function.
        /// </summary>
        /// <typeparam name="TFunction">The stateless function to wrap.</typeparam>
        private struct CachedOptimizationFunction<TFunction> :
            ICPUOptimizationFunction<T, TEvalType, object>
            where TFunction : ICPUOptimizationFunction<T, TEvalType>
        {
            private TFunction function;

            public CachedOptimizationFunction(TFunction optimizationFunction)
            {
                function = optimizationFunction;
            }

            /// <summary>
            /// Returns a shared intermediate state object.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public object CreateIntermediate() =>
                RawComparisonWrapper.SharedIntermediateState;

            /// <summary>
            /// Does not perform any operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void InitializeIntermediate(object intermediateState) { }

            /// <summary>
            /// Does not perform any operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void FinishProcessing(object intermediateState) { }

            /// <summary>
            /// Invokes the underlying comparison function to compare current and proposed
            /// evaluation instances.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool CurrentIsBetter(TEvalType current, TEvalType proposed) =>
                function.CurrentIsBetter(current, proposed);

            /// <summary>
            /// Evaluates the given position while discarding the given intermediate
            /// state.
            /// </summary>
            /// <returns>The evaluation result.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TEvalType Evaluate(
                ReadOnlySpan<T> position,
                object intermediateState) =>
                function.Evaluate(position);
        }

        #endregion

        #region Instance

        private readonly System.Random random;
        private readonly int[] indices;

        private readonly int[] randomOffensiveIndices;
        private readonly int[] randomDefensiveIndices;

        private readonly T[] lowerBounds;
        private readonly T[] upperBounds;

        private readonly T[] og;
        private readonly T[] dg;

        private T[] sog;
        private T[] sdg;

        private T[] nextSOG;
        private T[] nextSDG;

        private readonly int[] sogList;
        private int sogListCounter;

        private T[] positions;
        private T[] nextPositions;

        private readonly TEvalType[] evaluations;

        /// <summary>
        /// Creates a new meta optimizer instance.
        /// </summary>
        /// <param name="inputRandom">The input random instance.</param>
        /// <param name="numPlayers">The number of players.</param>
        /// <param name="numDimensions">The dimensionality of the problem.</param>
        /// <param name="maxNumParallelThreads">
        /// The maximum number of processing threads (if any).
        /// </param>
        /// <param name="numDimensionsPerStep">
        /// The number of dimension values per batched step.
        /// </param>
        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Catch is used to initialize step sizes to logical 0.5 " +
                "which may lead to exceptions depending on the value type")]
        protected MetaOptimizer(
            System.Random inputRandom,
            int numPlayers,
            int numDimensions,
            int? maxNumParallelThreads,
            int numDimensionsPerStep)
        {
            if (numPlayers < 1)
                throw new ArgumentOutOfRangeException(nameof(numPlayers));
            if (numDimensionsPerStep < 1)
                throw new ArgumentOutOfRangeException(nameof(numDimensionsPerStep));

            numPlayers = Math.Max(numPlayers, 4);
            numPlayers += numPlayers % 2;

            NumPlayers = numPlayers;
            MaxNumWorkers = maxNumParallelThreads.HasValue
                ? maxNumParallelThreads.Value < 1
                    ? Environment.ProcessorCount
                    : maxNumParallelThreads.Value
                : -1;

            // Update the number of dimensions to ensure valid padding to multiples of
            // the vector size
            NumDimensions = numDimensions;
            NumPaddedDimensions =
                XMath.DivRoundUp(numDimensions, numDimensionsPerStep) *
                numDimensionsPerStep;
            NumDimensionSlices = NumPaddedDimensions / numDimensionsPerStep;

            random = new System.Random(inputRandom.Next());

            lowerBounds = new T[NumPaddedDimensions];
            upperBounds = new T[NumPaddedDimensions];

            og = new T[NumPaddedDimensions];
            dg = new T[NumPaddedDimensions];

            sog = new T[NumPaddedDimensions];
            sdg = new T[NumPaddedDimensions];

            nextSOG = new T[NumPaddedDimensions];
            nextSDG = new T[NumPaddedDimensions];

            M = numPlayers / 2;
            randomOffensiveIndices = new int[M];
            randomDefensiveIndices = new int[M];

            indices = new int[numPlayers];
            sogList = new int[numPlayers];
            positions = new T[numPlayers * NumPaddedDimensions];
            nextPositions = new T[numPlayers * NumPaddedDimensions];
            evaluations = new TEvalType[numPlayers];

            for (int i = 0; i < numPlayers; ++i)
            {
                indices[i] = i;
                if (i < M)
                {
                    randomOffensiveIndices[i] = i;
                    randomDefensiveIndices[i] = i + M;
                }
            }

            // Try to initialize the basic step sizes
            try
            {
                var value2 = T.CreateSaturating(2);
                DefensiveStepSize = T.One / value2;
                OffensiveStepSize = T.One / value2;
                OffensiveSOGStepSize = T.One / value2;
            }
            catch (Exception)
            {
                // We actually ignore the initialization of step sizes in this case
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of dimensions.
        /// </summary>
        public int NumDimensions { get; }

        /// <summary>
        /// Returns the number of padded dimensions.
        /// </summary>
        public int NumPaddedDimensions { get; }

        /// <summary>
        /// Returns the number of players.
        /// </summary>
        public int NumPlayers { get; }

        /// <summary>
        /// Returns the number of dimensions per processing step.
        /// </summary>
        private int NumDimensionSlices { get; }

        /// <summary>
        /// Returns the maximum number of parallel processing threads.
        /// </summary>
        private int MaxNumWorkers { get; }

        /// <summary>
        /// Returns half the number of players (referred to as M in the scope of the SGO
        /// algorithm paper).
        /// </summary>
        protected int M { get; }

        /// <summary>
        /// Gets or sets lower bounds of this optimizer.
        /// </summary>
        public ReadOnlySpan<T> LowerBounds
        {
            get => lowerBounds.AsSpan()[..NumDimensions];
            set
            {
                if (value.Length != NumDimensions)
                    throw new ArgumentOutOfRangeException(nameof(value));
                value.CopyTo(lowerBounds);
            }
        }

        /// <summary>
        /// Gets or sets upper bounds of this optimizer.
        /// </summary>
        public ReadOnlySpan<T> UpperBounds
        {
            get => upperBounds.AsSpan()[..NumDimensions];
            set
            {
                if (value.Length != NumDimensions)
                    throw new ArgumentOutOfRangeException(nameof(value));
                value.CopyTo(upperBounds);
            }
        }

        /// <summary>
        /// Gets or sets the step size of the defensive players.
        /// </summary>
        public T DefensiveStepSize { get; set; }

        /// <summary>
        /// Gets or sets the step size of the offensive players.
        /// </summary>
        public T OffensiveStepSize { get; set; }

        /// <summary>
        /// Gets or sets the step size of the offensive players in the SOG.
        /// </summary>
        public T OffensiveSOGStepSize { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the current player position memory to operate on source values in the
        /// current iteration.
        /// </summary>
        /// <param name="playerIndex">The player index.</param>
        /// <returns>
        /// A memory instance holding all multidimensional position information for the
        /// given player.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Memory<T> GetPositionMemory(int playerIndex) =>
            positions.AsMemory(
                playerIndex * NumPaddedDimensions,
                NumPaddedDimensions);

        /// <summary>
        /// Gets the current player position span to operate on source values in the
        /// current iteration.
        /// </summary>
        /// <param name="playerIndex">The player index.</param>
        /// <returns>
        /// A span holding all multidimensional position information for the given player.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe Span<T> GetPosition(int playerIndex)
        {
            ref var baseRef = ref positions.AsSpan().GetItemRef(
                playerIndex * NumPaddedDimensions);
            return new Span<T>(Unsafe.AsPointer(ref baseRef), NumPaddedDimensions);
        }

        /// <summary>
        /// Gets the next position span for value updates in the next iteration.
        /// </summary>
        /// <param name="playerIndex">The player index.</param>
        /// <returns>
        /// A span holding all multidimensional position information for the given player.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe Span<T> GetNextPosition(int playerIndex)
        {
            ref var baseRef = ref nextPositions.AsSpan().GetItemRef(
                playerIndex * NumPaddedDimensions);
            return new Span<T>(Unsafe.AsPointer(ref baseRef), NumPaddedDimensions);
        }

        /// <summary>
        /// Gets the random offensive index corresponding to the given relative player
        /// index.
        /// </summary>
        /// <param name="playerIndex">The relative input player index.</param>
        /// <returns>An absolute random offensive index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetRandomOffensiveIndex(int playerIndex) =>
            randomOffensiveIndices.AsSpan().GetItemRef(playerIndex);

        /// <summary>
        /// Gets the random defensive index corresponding to the given relative player
        /// index.
        /// </summary>
        /// <param name="playerIndex">The relative input player index.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetRandomDefensiveIndex(int playerIndex) =>
            randomDefensiveIndices.AsSpan().GetItemRef(playerIndex);

        /// <summary>
        /// Resets the contents of the two given spans.
        /// </summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <typeparam name="TType">The processing type.</typeparam>
        /// <param name="first">The first span to reset.</param>
        /// <param name="second">The second span to reset.</param>
        [MethodImpl(
            MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        private void Reset<TProcessor, TType>(Span<TType> first, Span<TType> second)
            where TProcessor : struct, IProcessor<TProcessor, TType>
            where TType : unmanaged
        {
            // Reset first and second vectors
            var processor = TProcessor.New();
            for (int i = 0; i < NumDimensionSlices; ++i)
            {
                processor.Reset(out first.GetItemRef(i));
                processor.Reset(out second.GetItemRef(i));
            }
        }

        /// <summary>
        /// Accumulates information from the first source into the first target span and
        /// from the second source into the second target span.
        /// </summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <typeparam name="TType">The processing type.</typeparam>
        /// <param name="firstTarget">The first target span to accumulate into.</param>
        /// <param name="secondTarget">The second target span to accumulate into.</param>
        /// <param name="firstSource">
        /// The first source span to get the intermediate results from.
        /// </param>
        /// <param name="secondSource">
        /// The second source span to get the intermediate results from.
        /// </param>
        [MethodImpl(
            MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        private void Accumulate<TProcessor, TType>(
            Span<TType> firstTarget,
            Span<TType> secondTarget,
            ReadOnlySpan<TType> firstSource,
            ReadOnlySpan<TType> secondSource)
            where TProcessor : struct, IProcessor<TProcessor, TType>
            where TType : unmanaged
        {
            // Create new processor
            var processor = TProcessor.New();

            // Accumulate first and second vectors
            for (int i = 0; i < NumDimensionSlices; ++i)
            {
                processor.Accumulate(
                    ref firstTarget.GetItemRef(i),
                    firstSource.GetItemRef(i));
                processor.Accumulate(
                    ref secondTarget.GetItemRef(i),
                    secondSource.GetItemRef(i));
            }
        }

        /// <summary>
        /// Computes the average position vectors based on the given first and second
        /// spans holding all multidimensional information.
        /// </summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <typeparam name="TType">The processing type.</typeparam>
        /// <param name="first">The first span to compute the average for.</param>
        /// <param name="second">The second span to compute the average for.</param>
        /// <param name="numContributors">
        /// The number of contributors representing the denominator of the first span.
        /// </param>
        /// <param name="numContributorsSecond">
        /// The (optional) number of contributors representing the denominator of the
        /// second span. If the number is not provided, the number will be equal to the
        /// first number of contributors.
        /// </param>
        [MethodImpl(
            MethodImplOptions.AggressiveInlining |
            MethodImplOptions.AggressiveOptimization)]
        private void ComputeAverage<TProcessor, TType>(
            Span<TType> first,
            Span<TType> second,
            T numContributors,
            T? numContributorsSecond = null)
            where TProcessor : struct, IProcessor<TProcessor, TType>
            where TType : unmanaged
        {
            // Create new processor
            var processor = TProcessor.New();

            // Determine second contributors
            numContributors = T.Max(numContributors, T.One);
            T secondContributors = T.Max(
                numContributorsSecond ?? numContributors,
                T.One);

            // Iterate over all dimension slices
            for (int i = 0; i < NumDimensionSlices; ++i)
            {
                processor.ComputeAverage(ref first.GetItemRef(i), numContributors);
                processor.ComputeAverage(ref second.GetItemRef(i), secondContributors);
            }
        }

        /// <summary>
        /// Optimize the given objective function using delegates.
        /// </summary>
        /// <param name="evalFunction">The evaluation function.</param>
        /// <param name="breakFunction">The break function.</param>
        /// <param name="comparison">
        /// The comparison functionality comparing evaluation results.
        /// </param>
        /// <param name="bestResult">The best known input result.</param>
        /// <param name="bestKnownPosition">The best known position span.</param>
        /// <returns>
        /// A tuple consisting of the best found result and position vector.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (TEvalType Result, Memory<T> Position) Optimize(
            CPUOptimizationFunction<T, TEvalType> evalFunction,
            CPUOptimizationBreakFunction<TEvalType> breakFunction,
            Comparison<TEvalType> comparison,
            TEvalType bestResult,
            ReadOnlyMemory<T>? bestKnownPosition = default)
        {
            var wrapper = new FunctionWrapper(
                evalFunction,
                breakFunction,
                (first, second) =>
                    comparison(first, second) >= 0);
            return Optimize(wrapper, wrapper, bestResult, bestKnownPosition);
        }

        /// <summary>
        /// Optimize the given objective function using delegates.
        /// </summary>
        /// <param name="evalFunction">The evaluation function.</param>
        /// <param name="evaluationComparison">
        /// The comparison function comparing evaluation results.
        /// </param>
        /// <param name="breakFunction">The break function.</param>
        /// <param name="bestResult">The best known input result.</param>
        /// <param name="bestKnownPosition">The best known position span.</param>
        /// <returns>
        /// A tuple consisting of the best found result and position vector.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (TEvalType Result, Memory<T> Position) Optimize(
            CPUOptimizationFunction<T, TEvalType> evalFunction,
            CPUOptimizationBreakFunction<TEvalType> breakFunction,
            CPUEvaluationComparison<TEvalType> evaluationComparison,
            TEvalType bestResult,
            ReadOnlyMemory<T>? bestKnownPosition = default)
        {
            var wrapper = new FunctionWrapper(
                evalFunction,
                breakFunction,
                evaluationComparison);
            return Optimize(wrapper, wrapper, bestResult, bestKnownPosition);
        }

        /// <summary>
        /// Optimize the given objective function using specialized optimization function
        /// types.
        /// </summary>
        /// <param name="optimizationFunction">The optimization function.</param>
        /// <param name="breakFunction">The break function.</param>
        /// <param name="bestResult">The best known input result.</param>
        /// <param name="bestKnownPosition">The best known position span.</param>
        /// <returns>
        /// A tuple consisting of the best found result and position vector.
        /// </returns>
        public (TEvalType Result, Memory<T> Position) Optimize<
            TFunction,
            TBreakFunction>(
            in TFunction optimizationFunction,
            in TBreakFunction breakFunction,
            TEvalType bestResult,
            ReadOnlyMemory<T>? bestKnownPosition = default)
            where TFunction : ICPUOptimizationFunction<T, TEvalType>
            where TBreakFunction : ICPUOptimizationBreakFunction<TEvalType> =>
            Optimize(
                optimizationFunction,
                breakFunction,
                CPUPositionModifier.GetNop<T>(),
                bestResult,
                bestKnownPosition);

        /// <summary>
        /// Optimize the given objective function using specialized optimization function
        /// types.
        /// </summary>
        /// <param name="optimizationFunction">The optimization function.</param>
        /// <param name="breakFunction">The break function.</param>
        /// <param name="positionModifier">
        /// The position modifier to apply to all position updates during optimization.
        /// </param>
        /// <param name="bestResult">The best known input result.</param>
        /// <param name="bestKnownPosition">The best known position span.</param>
        /// <returns>
        /// A tuple consisting of the best found result and position vector.
        /// </returns>
        public (TEvalType Result, Memory<T> Position) Optimize<
            TFunction,
            TBreakFunction,
            TModifier>(
            in TFunction optimizationFunction,
            in TBreakFunction breakFunction,
            in TModifier positionModifier,
            TEvalType bestResult,
            ReadOnlyMemory<T>? bestKnownPosition = default)
            where TFunction : ICPUOptimizationFunction<T, TEvalType>
            where TBreakFunction : ICPUOptimizationBreakFunction<TEvalType>
            where TModifier : ICPUPositionModifier<T>
        {
            var cachedFunctionWrapper = new CachedOptimizationFunction<TFunction>(
                optimizationFunction);
            return Optimize<
                CachedOptimizationFunction<TFunction>,
                object,
                TBreakFunction,
                TModifier>(
                cachedFunctionWrapper,
                breakFunction,
                positionModifier,
                bestResult,
                bestKnownPosition);
        }

        /// <summary>
        /// Optimize the given objective function using specialized optimization function
        /// types.
        /// </summary>
        /// <typeparam name="TFunction">The optimization function type.</typeparam>
        /// <typeparam name="TIntermediate">
        /// The intermediate optimization state type.
        /// </typeparam>
        /// <typeparam name="TBreakFunction">The break function type.</typeparam>
        /// <typeparam name="TModifier">The position modifier type.</typeparam>
        /// <param name="optimizationFunction">The optimization function.</param>
        /// <param name="breakFunction">The break function.</param>
        /// <param name="positionModifier">
        /// The position modifier to apply to all position updates during optimization.
        /// </param>
        /// <param name="bestResult">The best known input result.</param>
        /// <param name="bestKnownPosition">The best known position span.</param>
        /// <returns>
        /// A tuple consisting of the best found result and position vector.
        /// </returns>
        public abstract (TEvalType Result, Memory<T> Position) Optimize<
            TFunction,
            TIntermediate,
            TBreakFunction,
            TModifier>(
            in TFunction optimizationFunction,
            in TBreakFunction breakFunction,
            in TModifier positionModifier,
            TEvalType bestResult,
            ReadOnlyMemory<T>? bestKnownPosition = default)
            where TFunction : ICPUOptimizationFunction<T, TEvalType, TIntermediate>
            where TIntermediate : class
            where TBreakFunction : ICPUOptimizationBreakFunction<TEvalType>
            where TModifier : ICPUPositionModifier<T>;

        /// <summary>
        /// Optimize the given objective function using specialized optimization function
        /// types. This overload uses raw optimization function callbacks to implement
        /// extremely customizable optimization functions on top of the current stack.
        /// </summary>
        /// <param name="optimizationFunction">The optimization function.</param>
        /// <param name="breakFunction">The break function.</param>
        /// <param name="evaluationComparison">
        /// The comparison function comparing evaluation results.
        /// </param>
        /// <param name="bestResult">The best known input result.</param>
        /// <param name="bestKnownPosition">The best known position span.</param>
        /// <returns>
        /// A tuple consisting of the best found result and position vector.
        /// </returns>
        public abstract (TEvalType Result, Memory<T> Position) OptimizeRaw(
            RawCPUOptimizationFunction<T, TEvalType> optimizationFunction,
            CPUOptimizationBreakFunction<TEvalType> breakFunction,
            CPUEvaluationComparison<TEvalType> evaluationComparison,
            TEvalType bestResult,
            ReadOnlyMemory<T>? bestKnownPosition = default);

        /// <summary>
        /// Copies all current positions to all next positions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyPositions()
        {
            var positionsSpans = positions.AsSpan();
            var nextPositionsSpan = nextPositions.AsSpan();
            positionsSpans.CopyTo(nextPositionsSpan);
        }

        /// <summary>
        /// Permutes internal index arrays.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Permute()
        {
            for (int i = NumPlayers - 1; i > 1; --i)
            {
                int j = random.Next(i + 1);
                Utilities.Swap(ref indices[i], ref indices[j]);
            }

            for (int i = M - 1; i > 1; --i)
            {
                int j = random.Next(i + 1);
                Utilities.Swap(
                    ref randomOffensiveIndices[i],
                    ref randomOffensiveIndices[j]);

                int k = random.Next(i + 1);
                Utilities.Swap(
                    ref randomDefensiveIndices[i],
                    ref randomDefensiveIndices[k]);
            }
        }

        /// <summary>
        /// Initializes the internal SOG list for the current iteration.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitSOGList()
        {
            // Reset SOG list
            Interlocked.Exchange(ref sogListCounter, 0);
#if DEBUG
            Array.Clear(sogList);
#endif
        }

        /// <summary>
        /// Swaps all intermediate buffers for the next iteration.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SwapBuffers()
        {
            // Swap current and next positions
            Utilities.Swap(ref positions, ref nextPositions);

            // Swap current SOG and SDG vectors
            Utilities.Swap(ref sog, ref nextSOG);
            Utilities.Swap(ref sdg, ref nextSDG);
        }

        #endregion
    }
}

#endif
