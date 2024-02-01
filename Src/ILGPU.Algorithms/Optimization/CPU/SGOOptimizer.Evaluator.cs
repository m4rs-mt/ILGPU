// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: SGOOptimizer.Evaluator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

#if NET7_0_OR_GREATER

namespace ILGPU.Algorithms.Optimization.CPU
{
    partial class SGOOptimizer<T, TEvalType>
    {
        /// <summary>
        /// A parallel evaluation state storing temporary best result and position
        /// information per thread.
        /// </summary>
        /// <typeparam name="TFunction">
        /// The optimization function type to use.
        /// </typeparam>
        /// <typeparam name="TIntermediate">
        /// The intermediate state type for each optimization processing thread.
        /// </typeparam>
        private sealed class EvaluatorState<TFunction, TIntermediate> : DisposeBase
            where TFunction :
                IBaseOptimizationFunction<TEvalType>,
                IParallelCache<TIntermediate>
            where TIntermediate : class
        {
            private TFunction function;
            private TEvalType bestKnownResult;
            private readonly T[] bestPosition;

            /// <summary>
            /// Creates a new evaluation state.
            /// </summary>
            /// <param name="optimizationFunction">
            /// The optimization function to use.
            /// </param>
            /// <param name="numPaddedDimensions">
            /// The number of padded dimensions taking vector lengths into account.
            /// </param>
            public EvaluatorState(TFunction optimizationFunction, int numPaddedDimensions)
            {
                function = optimizationFunction;
                bestPosition = new T[numPaddedDimensions];
                Intermediate = function.CreateIntermediate();
            }

            /// <summary>
            /// Returns the intermediate state of this instance.
            /// </summary>
            public TIntermediate Intermediate { get; }

            /// <summary>
            /// Resets the best known result to the given result value.
            /// </summary>
            /// <param name="bestResult">The best result value to store.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset(TEvalType bestResult)
            {
                bestKnownResult = bestResult;
                Array.Clear(bestPosition);
            }

            /// <summary>
            /// Merges the given result with the internally stored one. If the passed
            /// result value is considered better than the stored one, the passed position
            /// vector will be copied to the internally stored best position.
            /// </summary>
            /// <param name="result">The result value to merge.</param>
            /// <param name="position">
            /// The position that led to the given result value.
            /// </param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void MergeWith(TEvalType result, ReadOnlySpan<T> position)
            {
                if (function.CurrentIsBetter(bestKnownResult, result))
                    return;

                bestKnownResult = result;
                position.CopyTo(bestPosition);
            }

            /// <summary>
            /// Aggregates currently available information into the given result field.
            /// If the objective function determines that the referenced result is worse
            /// than the one stored internally, the referenced result value is updated
            /// and the internally stored position is copied to the given result position
            /// span.
            /// </summary>
            /// <param name="result">
            /// A reference to the currently known best result.
            /// </param>
            /// <param name="resultPosition">
            /// A span pointing to the globally found best result position vector which
            /// will be updated if the internally stored result value is considered
            /// better than the referenced one.
            /// </param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AggregateInto(ref TEvalType result, Span<T> resultPosition)
            {
                if (function.CurrentIsBetter(result, bestKnownResult))
                    return;

                result = bestKnownResult;
                bestPosition.CopyTo(resultPosition);
            }

            /// <summary>
            /// Disposes the intermediate state if required.
            /// </summary>
            protected override void Dispose(bool disposing)
            {
                if (Intermediate is IDisposable disposable)
                    disposable.Dispose();

                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Represents a result manager storing best result values.
        /// </summary>
        private struct ResultManager
        {
            private readonly T[] bestPosition;
            private TEvalType bestResult;

            /// <summary>
            /// Creates a new result manager.
            /// </summary>
            /// <param name="optimizer">The parent optimizer.</param>
            /// <param name="bestUserKnownResult">
            /// The best known result provided by the user.
            /// </param>
            /// <param name="bestKnownPosition">
            /// The best known position provided by the user.
            /// </param>
            public ResultManager(
                SGOOptimizer<T, TEvalType> optimizer,
                in TEvalType bestUserKnownResult,
                ReadOnlyMemory<T>? bestKnownPosition)
            {
                // Validate our best known position vector
                if (bestKnownPosition.HasValue &&
                    bestKnownPosition.Value.Length != NumDimensions)
                {
                    throw new ArgumentOutOfRangeException(nameof(bestKnownPosition));
                }

                bestPosition = new T[optimizer.NumPaddedDimensions];
                bestResult = BestInitialResult = bestUserKnownResult;

                NumDimensions = optimizer.NumDimensions;

                // Check for a valid best known result
                if (!bestKnownPosition.HasValue)
                {
                    // Reset best known position
                    for (int i = 0; i < bestPosition.Length; ++i)
                        bestPosition[i] = T.Zero;
                }
                else
                {
                    // Copy known position
                    bestKnownPosition.Value.CopyTo(bestPosition);

                    // Reset remaining parts
                    for (int i = NumDimensions; i < bestPosition.Length; ++i)
                        bestPosition[i] = T.Zero;
                }
            }

            /// <summary>
            /// Returns the number of dimensions.
            /// </summary>
            public int NumDimensions { get; }

            /// <summary>
            /// Returns the best found result.
            /// </summary>
            public readonly TEvalType BestResult => bestResult;

            /// <summary>
            /// Returns the best known initial result.
            /// </summary>
            public TEvalType BestInitialResult { get; }

            /// <summary>
            /// Returns the best found position (not padded).
            /// </summary>
            public readonly Memory<T> BestPosition =>
                new(bestPosition, 0, NumDimensions);

            /// <summary>
            /// Returns the best found internal position (padded).
            /// </summary>
            public readonly ReadOnlyMemory<T> BestInternalPosition => bestPosition;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Aggregate<TFunction, TIntermediate>(
                EvaluatorState<TFunction, TIntermediate> state)
                where TFunction :
                    IBaseOptimizationFunction<TEvalType>,
                    IParallelCache<TIntermediate>
                where TIntermediate : class =>
                state.AggregateInto(ref bestResult, bestPosition);
        }

        /// <summary>
        /// Represents an abstract evaluator.
        /// </summary>
        private interface IEvaluator : IDisposable
        {
            /// <summary>
            /// Returns the underlying result manager.
            /// </summary>
            ResultManager ResultManager { get; }

            /// <summary>
            /// Evaluates all players.
            /// </summary>
            /// <param name="options">The parallel processing options.</param>
            void EvaluatePlayers(ParallelOptions options);
        }

        /// <summary>
        /// Represents an objective function evaluator that applies the user-defined
        /// function to each player position in every step.
        /// </summary>
        /// <typeparam name="TFunction">The objective function type.</typeparam>
        /// <typeparam name="TIntermediate">
        /// The intermediate state type for each evaluator thread.
        /// </typeparam>
        /// <typeparam name="TModifier">The position modifier type.</typeparam>
        private sealed class Evaluator<TFunction, TIntermediate, TModifier> :
            ParallelProcessingCache<
                EvaluatorState<TFunction, TIntermediate>,
                Evaluator<TFunction, TIntermediate, TModifier>>,
            IParallelProcessingBody<EvaluatorState<TFunction, TIntermediate>>,
            IEvaluator
            where TFunction : ICPUOptimizationFunction<T, TEvalType, TIntermediate>
            where TIntermediate : class
            where TModifier : ICPUPositionModifier<T>
        {
            private readonly SGOOptimizer<T, TEvalType> parent;
            private TFunction function;
            private TModifier modifier;

            private readonly int numPaddedDimensions;
            private ResultManager resultManager;

            /// <summary>
            /// Creates a new evaluator.
            /// </summary>
            /// <param name="optimizer">The parent optimizer.</param>
            /// <param name="optimizationFunction">The optimization function.</param>
            /// <param name="positionModifier">The position modifier.</param>
            /// <param name="bestUserKnownResult">
            /// The best known result provided by the user.
            /// </param>
            /// <param name="bestKnownPosition">
            /// The best known position provided by the user.
            /// </param>
            public Evaluator(
                SGOOptimizer<T, TEvalType> optimizer,
                in TFunction optimizationFunction,
                in TModifier positionModifier,
                in TEvalType bestUserKnownResult,
                ReadOnlyMemory<T>? bestKnownPosition)
            {
                parent = optimizer;
                function = optimizationFunction;
                modifier = positionModifier;

                numPaddedDimensions = optimizer.NumPaddedDimensions;
                resultManager = new(optimizer, bestUserKnownResult, bestKnownPosition);
            }

            /// <summary>
            /// Returns the result manager.
            /// </summary>
            public ResultManager ResultManager => resultManager;

            /// <summary>
            /// Returns the current instance.
            /// </summary>
            protected override Evaluator<
                TFunction,
                TIntermediate,
                TModifier> CreateBody() => this;

            /// <summary>
            /// Creates an intermediate temporary state.
            /// </summary>
            protected override EvaluatorState<TFunction, TIntermediate>
                CreateIntermediate() =>
                new(function, numPaddedDimensions);

            /// <summary>
            /// Resets the given intermediate state by using the best known result
            /// provided by the user.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            protected override void InitializeIntermediate(
                EvaluatorState<TFunction, TIntermediate> intermediateState)
            {
                intermediateState.Reset(resultManager.BestInitialResult);
                function.InitializeIntermediate(intermediateState.Intermediate);
            }

            /// <summary>
            /// Does not perform any operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Initialize() { }

            /// <summary>
            /// Evaluates all players and accumulates intermediate results.
            /// </summary>
            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public void Process(
                int index,
                ParallelLoopState? loopState,
                EvaluatorState<TFunction, TIntermediate> intermediateState)
            {
                // Get the source position and evaluate
                var positionMemory = parent.GetPositionMemory(index);

                // Adjust position
                modifier.AdjustPosition(
                    index,
                    positionMemory,
                    resultManager.NumDimensions,
                    numPaddedDimensions);

                // Convert into a span and evaluate
                var position = positionMemory.Span;
                var result = function.Evaluate(position, intermediateState.Intermediate);

                // Store evaluation result
                parent.evaluations[index] = result;

                // Merge intermediate state
                intermediateState.MergeWith(result, position);
            }

            /// <summary>
            /// Aggregates all temporarily found best results into a globally shared
            /// state to find the best solution taking all solutions into account.
            /// </summary>
            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public void Finalize(
                ReadOnlySpan<EvaluatorState<TFunction, TIntermediate>> intermediateStates)
            {
                // Iterate over all states and aggregate all information
                foreach (var state in intermediateStates)
                {
                    function.FinishProcessing(state.Intermediate);
                    resultManager.Aggregate(state);
                }
            }

            /// <summary>
            /// Evaluates all players in parallel using the underlying modifier, eval
            /// function, and comparison functions.
            /// </summary>
            /// <param name="options">The parallel processing options.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EvaluatePlayers(ParallelOptions options) =>
                ParallelFor(0, parent.NumPlayers, options);
        }
    }
}

#endif
