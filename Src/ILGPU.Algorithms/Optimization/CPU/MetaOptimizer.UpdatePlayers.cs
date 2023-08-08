// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetaOptimizer.UpdatePlayers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Random;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

#if NET7_0_OR_GREATER

namespace ILGPU.Algorithms.Optimization.CPU
{
    partial class MetaOptimizer<T, TEvalType>
    {
        /// <summary>
        /// Represents an intermediate parallel processing state for updating players.
        /// </summary>
        /// <typeparam name="TRandom">The random provider type.</typeparam>
        private sealed class UpdatePlayersState<TRandom> : AdjustSOGPlayersState<TRandom>
            where TRandom : struct, IRandomRangeProvider<T>
        {
            private readonly T[] nextSOG;
            private readonly T[] nextSDG;

            private int nextSOGCounter;
            private int nextSDGCounter;

            /// <summary>
            /// Creates new intermediate state.
            /// </summary>
            /// <param name="provider">The random provider instance.</param>
            /// <param name="numDimensions">The number of dimensions.</param>
            public UpdatePlayersState(TRandom provider, int numDimensions)
                : base(provider)
            {
                nextSOG = new T[numDimensions];
                nextSDG = new T[numDimensions];
            }

            /// <summary>
            /// Resets all internally stored counters.
            /// </summary>
            public void ResetCounters()
            {
                nextSOGCounter = 0;
                nextSDGCounter = 0;
            }

            /// <summary>
            /// Adds a new SOG member.
            /// </summary>
            public void AddSOGMember() => ++nextSOGCounter;

            /// <summary>
            /// Adds a new SDG member.
            /// </summary>
            public void AddSDGMember() => ++nextSDGCounter;

            /// <summary>
            /// Returns a span of the given processing type pointing to the next SOG.
            /// </summary>
            /// <typeparam name="TType">The processing type.</typeparam>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Span<TType> GetNextSOG<TType>() where TType : struct =>
                nextSOG.AsSpan().CastUnsafe<T, TType>();

            /// <summary>
            /// Returns a span of the given processing type pointing to the next SDG.
            /// </summary>
            /// <typeparam name="TType">The processing type.</typeparam>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Span<TType> GetNextSDG<TType>() where TType : struct =>
                nextSDG.AsSpan().CastUnsafe<T, TType>();

            /// <summary>
            /// Accumulates externally provided counters for SOG and SDG members.
            /// </summary>
            public void AccumulateCounters(ref int sogMembers, ref int sdgMembers)
            {
                sogMembers += nextSOGCounter;
                sdgMembers += nextSDGCounter;
            }
        }

        /// <summary>
        /// Updates all players according to defensive and offensive winners.
        /// </summary>
        /// <typeparam name="TFunction">The objective function type to use.</typeparam>
        /// <typeparam name="TProcessor">The processor type being used.</typeparam>
        /// <typeparam name="TType">The processor element type.</typeparam>
        /// <typeparam name="TRandom">The random provider type.</typeparam>
        private sealed class UpdatePlayers<
            TFunction,
            TProcessor,
            TType,
            TRandom> :
            ParallelProcessingCache<
                UpdatePlayersState<TRandom>,
                UpdatePlayers<
                    TFunction,
                    TProcessor,
                    TType,
                    TRandom>>,
            IParallelProcessingBody<UpdatePlayersState<TRandom>>
            where TFunction : IBaseOptimizationFunction<TEvalType>
            where TProcessor : struct, IProcessor<TProcessor, TType>
            where TType : unmanaged
            where TRandom : struct, IRandomRangeProvider<T>
        {
            private readonly MetaOptimizer<T, TEvalType> parent;
            private readonly Func<MetaOptimizer<T, TEvalType>, TRandom> getRandom;
            private readonly TFunction function;

            private volatile bool hasSOGAndSDG;

            /// <summary>
            /// Creates a new player update instance.
            /// </summary>
            /// <param name="optimizer">The parent optimizer instance.</param>
            /// <param name="createRandom">A function creating a new RNG instance.</param>
            /// <param name="optimizationFunction">The objective function.</param>
            public UpdatePlayers(
                MetaOptimizer<T, TEvalType> optimizer,
                Func<MetaOptimizer<T, TEvalType>, TRandom> createRandom,
                in TFunction optimizationFunction)
            {
                parent = optimizer;
                getRandom = createRandom;
                function = optimizationFunction;

                NumDimensionSlices = optimizer.NumDimensionSlices;
            }

            /// <summary>
            /// Returns the current instance.
            /// </summary>
            protected override UpdatePlayers<
                TFunction,
                TProcessor,
                TType,
                TRandom> CreateBody() => this;

            /// <summary>
            /// Returns the number of dimensions per processing step.
            /// </summary>
            public int NumDimensionSlices { get; }

            /// <summary>
            /// Returns true if SOG and SDG information has been available.
            /// </summary>
            public bool HasCurrentSOGAndSDG
            {
                get => hasSOGAndSDG;
                set => hasSOGAndSDG = value;
            }

            /// <summary>
            /// Gets or sets the best known position vector.
            /// </summary>
            public ReadOnlyMemory<T> BestPosition { get; set; }

            /// <summary>
            /// Creates an intermediate temporary state.
            /// </summary>
            protected override UpdatePlayersState<TRandom> CreateIntermediate() =>
                new(getRandom(parent), parent.NumPaddedDimensions);

            /// <summary>
            /// Resets the given intermediate state by resetting all values to T.Zero.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            protected override void InitializeIntermediate(
                UpdatePlayersState<TRandom> intermediateState)
            {
                // Reset next SOG and SDG vectors
                var nextSOG = intermediateState.GetNextSOG<TType>();
                var nextSDG = intermediateState.GetNextSDG<TType>();

                parent.Reset<TProcessor, TType>(nextSOG, nextSDG);

                // Reset SOG and SDG counters
                intermediateState.ResetCounters();
            }

            /// <summary>
            /// Resets the next SOG and SDG vectors.
            /// </summary>
            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public void Initialize()
            {
                // Reset parent next SOG and SDG vectors
                var nextSOG = parent.nextSOG.AsSpan().CastUnsafe<T, TType>();
                var nextSDG = parent.nextSDG.AsSpan().CastUnsafe<T, TType>();

                parent.Reset<TProcessor, TType>(nextSOG, nextSDG);
            }

            /// <summary>
            /// Accumulates offensive and defensive players into OG and DG vectors.
            /// </summary>
            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public void Process(
                int index,
                ParallelLoopState? _,
                UpdatePlayersState<TRandom> state)
            {
                // Get offsets and spans for offensive and defensive players
                var indices = parent.indices.AsSpan();
                int offensiveIndex = indices.GetItemRef(index);
                int defensiveIndex = indices.GetItemRef(index + parent.M);

                // Get the actual source views
                var offensive = parent.GetPosition(offensiveIndex);
                var defensive = parent.GetPosition(defensiveIndex);

                // Evaluate both positions and test whether the offensive or the defensive
                // player wins this competition
                var evaluatedOffensive = parent.evaluations[offensiveIndex];
                var evaluatedDefensive = parent.evaluations[defensiveIndex];
                bool offensiveWins = function.CurrentIsBetter(
                    evaluatedOffensive,
                    evaluatedDefensive);

                // Get lower and upper bounds
                var lowerBounds = parent.lowerBounds.AsSpan().CastUnsafe<T, TType>();
                var upperBounds = parent.upperBounds.AsSpan().CastUnsafe<T, TType>();

                // Get the current players
                var currentOffensive = offensive.CastUnsafe<T, TType>();
                var currentDefensive = defensive.CastUnsafe<T, TType>();

                // Create new processor for this iteration
                var processor = TProcessor.New();
                if (offensiveWins)
                {
                    // Get two random numbers
                    var r1 = state.Next();
                    var r2 = state.Next();

                    // Get OG vector
                    var og = parent.og.AsSpan().CastUnsafe<T, TType>();

                    // Get a random offensive player
                    int randomOffensiveIndex = parent.GetRandomOffensiveIndex(index);
                    var randomOffensive = parent
                        .GetPosition(randomOffensiveIndex)
                        .CastUnsafe<T, TType>();

                    // Fetch next vector references
                    var nextSOG = state.GetNextSOG<TType>();
                    var nextDefensive = parent
                        .GetNextPosition(defensiveIndex)
                        .CastUnsafe<T, TType>();
                    for (int i = 0; i < NumDimensionSlices; ++i)
                    {
                        // Compute new position and set new vector of defensive player
                        var xDefNew1 = processor.DetermineNewPosition(
                            currentDefensive.GetItemRef(i),
                            og.GetItemRef(i),
                            randomOffensive.GetItemRef(i),
                            r1,
                            r2,
                            parent.DefensiveStepSize);

                        // Clamp new defensive position and store result
                        var clamped = processor.Clamp(
                            lowerBounds.GetItemRef(i),
                            upperBounds.GetItemRef(i),
                            xDefNew1);
                        nextDefensive.GetItemRef(i) = clamped;

                        // Accumulate SOG result
                        processor.Accumulate(
                            ref nextSOG.GetItemRef(i),
                            currentOffensive.GetItemRef(i));
                    }

                    // Add new SOG member to state
                    state.AddSOGMember();

                    // Add offensive player to next sog
                    int sogIndex = Interlocked.Add(ref parent.sogListCounter, 1);
                    parent.sogList[sogIndex] = offensiveIndex;
                }
                else
                {
                    // Get four random numbers
                    var r1 = state.Next();
                    var r2 = state.Next();
                    var r3 = state.Next();
                    var r4 = state.Next();

                    // Get DG vector
                    var dg = parent.dg.AsSpan().CastUnsafe<T, TType>();

                    // Get random defensive player
                    int randomDefensiveIndex = parent.GetRandomDefensiveIndex(index);
                    var randomDefensive = parent
                        .GetPosition(randomDefensiveIndex)
                        .CastUnsafe<T, TType>();

                    // Get SOG and best position data
                    var sog = parent.sog.AsSpan().CastUnsafe<T, TType>();
                    var bestPosition = BestPosition.Span.CastUnsafe<T, TType>();

                    // Fetch next vector references
                    var nextSDG = state.GetNextSDG<TType>();
                    var nextOffensive = parent
                        .GetNextPosition(offensiveIndex)
                        .CastUnsafe<T, TType>();
                    for (int i = 0; i < NumDimensionSlices; ++i)
                    {
                        // Compute new position and set new vector of offensive player
                        var xOffNew1 = processor.DetermineNewPosition(
                            currentOffensive.GetItemRef(i),
                            dg.GetItemRef(i),
                            randomDefensive.GetItemRef(i),
                            r1,
                            r2,
                            parent.OffensiveStepSize);

                        // Check whether we can apply SOG adjustments
                        var xOffNew2 = xOffNew1;
                        if (HasCurrentSOGAndSDG)
                        {
                            xOffNew2 = processor.DetermineNewPosition(
                                xOffNew1,
                                sog.GetItemRef(i),
                                bestPosition.GetItemRef(i),
                                r3,
                                r4,
                                parent.OffensiveSOGStepSize);
                        }

                        // Clamp new offensive position and store result
                        var clamped = processor.Clamp(
                            lowerBounds.GetItemRef(i),
                            upperBounds.GetItemRef(i),
                            xOffNew2);
                        nextOffensive.GetItemRef(i) = clamped;

                        // Accumulate SDG result
                        processor.Accumulate(
                            ref nextSDG.GetItemRef(i),
                            currentDefensive.GetItemRef(i));
                    }

                    // Add new SDG member to state
                    state.AddSDGMember();
                }
            }

            /// <summary>
            /// Accumulates next SOG and SDG values based on all previous intermediate
            /// update states.
            /// </summary>
            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public void Finalize(
                ReadOnlySpan<UpdatePlayersState<TRandom>> intermediateStates)
            {
                var sog = parent.nextSOG.AsSpan().CastUnsafe<T, TType>();
                var sdg = parent.nextSDG.AsSpan().CastUnsafe<T, TType>();

                // Store total counters
                int sogMembers = 0;
                int sdgMembers = 0;

                // Iterate over all dimensions and states accumulate results
                foreach (var state in intermediateStates)
                {
                    var sourceSOG = state.GetNextSOG<TType>();
                    var sourceSDG = state.GetNextSDG<TType>();

                    parent.Accumulate<TProcessor, TType>(
                        sog,
                        sdg,
                        sourceSOG,
                        sourceSDG);

                    state.AccumulateCounters(ref sogMembers, ref sdgMembers);
                }

                // Ensure that we have not lost a single particle
                Debug.Assert(sogMembers + sdgMembers == parent.M);

                // Compute averages over all dimension slices
                parent.ComputeAverage<TProcessor, TType>(
                    sog,
                    sdg,
                    T.CreateSaturating(sogMembers),
                    T.CreateSaturating(sdgMembers));
            }
        }
    }
}

#endif
