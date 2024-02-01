// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: SGOOptimizer.RawEvaluator.cs
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
        /// Represents a comparison interface wrapper around a delegate comparison
        /// function used to compare evaluation results.
        /// </summary>
        /// <param name="EvaluationComparison">The evaluation delegate.</param>
        private readonly record struct RawComparisonWrapper(
            CPUEvaluationComparison<TEvalType> EvaluationComparison) :
            IBaseOptimizationFunction<TEvalType>,
            IParallelCache<object>
        {
            /// <summary>
            /// Represents a shared intermediate state holding a valid object instance.
            /// </summary>
            public static readonly object SharedIntermediateState = new();

            /// <summary>
            /// Invokes the underlying comparison delegate to compare current and proposed
            /// evaluation instances.
            /// </summary>
            public bool CurrentIsBetter(TEvalType current, TEvalType proposed) =>
                EvaluationComparison(current, proposed);

            /// <summary>
            /// Returns the shared intermediate state object.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public object CreateIntermediate() => SharedIntermediateState;

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
        }

        /// <summary>
        /// Represents an objective function evaluator that applies the user-defined
        /// function to each player position in every step.
        /// </summary>
        private sealed class RawEvaluator :
            ParallelProcessingCache<
                EvaluatorState<RawComparisonWrapper, object>,
                RawEvaluator>,
            IParallelProcessingBody<EvaluatorState<RawComparisonWrapper, object>>,
            IEvaluator
        {
            private readonly SGOOptimizer<T, TEvalType> parent;
            private readonly RawCPUOptimizationFunction<T, TEvalType> function;
            private readonly CPUEvaluationComparison<TEvalType> comparison;

            private readonly int numPaddedDimensions;
            private ResultManager resultManager;

            /// <summary>
            /// Creates a new evaluator.
            /// </summary>
            /// <param name="optimizer">The parent optimizer.</param>
            /// <param name="optimizationFunction">The optimization function.</param>
            /// <param name="evaluationComparison">The eval comparision function.</param>
            /// <param name="bestUserKnownResult">
            /// The best known result provided by the user.
            /// </param>
            /// <param name="bestKnownPosition">
            /// The best known position provided by the user.
            /// </param>
            public RawEvaluator(
                SGOOptimizer<T, TEvalType> optimizer,
                RawCPUOptimizationFunction<T, TEvalType> optimizationFunction,
                CPUEvaluationComparison<TEvalType> evaluationComparison,
                in TEvalType bestUserKnownResult,
                ReadOnlyMemory<T>? bestKnownPosition)
            {
                parent = optimizer;
                function = optimizationFunction;
                comparison = evaluationComparison;

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
            protected override RawEvaluator CreateBody() => this;

            /// <summary>
            /// Creates an intermediate temporary state.
            /// </summary>
            protected override EvaluatorState<
                RawComparisonWrapper,
                object> CreateIntermediate() =>
                new(new(comparison), numPaddedDimensions);

            /// <summary>
            /// Resets the given intermediate state by using the best known result
            /// provided by the user.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            protected override void InitializeIntermediate(
                EvaluatorState<RawComparisonWrapper, object> intermediateState) =>
                intermediateState.Reset(resultManager.BestInitialResult);

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
                EvaluatorState<RawComparisonWrapper, object> intermediateState)
            {
                // Get the source position
                var position = parent.GetPosition(index);

                // Get the evaluation result
                var result = parent.evaluations[index];

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
                ReadOnlySpan<
                    EvaluatorState<RawComparisonWrapper, object>> intermediateStates)
            {
                // Iterate over all states and aggregate all information
                foreach (var state in intermediateStates)
                    resultManager.Aggregate(state);
            }

            /// <summary>
            /// Evaluates all players using the given raw evaluation function first.
            /// After having evaluated all particle positions, it reduces all results
            /// in parallel.
            /// </summary>
            /// <param name="options">The parallel processing options.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EvaluatePlayers(ParallelOptions options)
            {
                // Evaluate all players using the provided raw function
                function(
                    parent.positions.AsMemory(),
                    parent.evaluations.AsMemory(),
                    ResultManager.NumDimensions,
                    parent.NumPaddedDimensions,
                    parent.NumPlayers,
                    new(parent.NumPaddedDimensions),
                    options);

                // Reduce all results in parallel
                ParallelFor(0, parent.NumPlayers, options);
            }
        }
    }
}

#endif
