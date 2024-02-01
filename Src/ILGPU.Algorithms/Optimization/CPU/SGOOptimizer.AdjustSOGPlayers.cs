// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: SGOOptimizer.AdjustSOGPlayers.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Random;
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
        /// A parallel processing state to adjust SOG-based information for all winning
        /// offensive players from the current solver iteration.
        /// </summary>
        /// <typeparam name="TRandom">The random range provider type.</typeparam>
        private class AdjustSOGPlayersState<TRandom> : InitializePlayersState<T, TRandom>
            where TRandom : struct, IRandomRangeProvider<T>
        {
            /// <summary>
            /// Creates a new SOG players state.
            /// </summary>
            /// <param name="random">The random to use.</param>
            public AdjustSOGPlayersState(TRandom random)
                : base(random)
            { }
        }

        /// <summary>
        /// Updates all players according to defensive and offensive winners.
        /// </summary>
        /// <typeparam name="TProcessor">The processor type being used.</typeparam>
        /// <typeparam name="TType">The processing type.</typeparam>
        /// <typeparam name="TRandom">The random range provider type.</typeparam>
        private sealed class AdjustSOGPlayers<TProcessor, TType, TRandom> :
            ParallelProcessingCache<
                AdjustSOGPlayersState<TRandom>,
                AdjustSOGPlayers<TProcessor, TType, TRandom>>,
            IParallelProcessingBody<AdjustSOGPlayersState<TRandom>>
            where TProcessor : struct, IProcessor<TProcessor, TType>
            where TType : unmanaged
            where TRandom : struct, IRandomRangeProvider<T>
        {
            private readonly SGOOptimizer<T, TEvalType> parent;
            private readonly Func<SGOOptimizer<T, TEvalType>, TRandom> getRandom;

            /// <summary>
            /// Creates a new player update instance.
            /// </summary>
            /// <param name="instance">The parent optimizer instance.</param>
            /// <param name="createRandom">A function creating a new RNG instance.</param>
            public AdjustSOGPlayers(
                SGOOptimizer<T, TEvalType> instance,
                Func<SGOOptimizer<T, TEvalType>, TRandom> createRandom)
            {
                parent = instance;
                getRandom = createRandom;
            }

            /// <summary>
            /// Gets or sets the best known position vector.
            /// </summary>
            public ReadOnlyMemory<T> BestPosition { get; set; }

            /// <summary>
            /// Returns the current instance.
            /// </summary>
            protected override AdjustSOGPlayers<TProcessor, TType, TRandom>
                CreateBody() => this;

            /// <summary>
            /// Creates an intermediate accumulation state.
            /// </summary>
            protected override AdjustSOGPlayersState<TRandom>
                CreateIntermediate() => new(getRandom(parent));

            /// <summary>
            /// Does not perform any operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Initialize() { }

            /// <summary>
            /// Adjusts all SOG-player positions from the current iteration while taking
            /// SDG and best positions into account.
            /// </summary>
            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public void Process(
                int index,
                ParallelLoopState? _,
                AdjustSOGPlayersState<TRandom> state)
            {
                // Load sog index and associated position vector
                var offensiveIndex = parent.sogList[index];
                var offensive = parent
                    .GetNextPosition(offensiveIndex)
                    .CastUnsafe<T, TType>();

                // Get two fresh random numbers
                var r1 = state.Next();
                var r2 = state.Next();

                // Get lower and upper bounds
                var lowerBounds = parent.lowerBounds.AsSpan().CastUnsafe<T, TType>();
                var upperBounds = parent.upperBounds.AsSpan().CastUnsafe<T, TType>();

                // Get best position and SDG
                var bestPosition = BestPosition.Span.CastUnsafe<T, TType>();
                var sdg = parent.sdg.AsSpan().CastUnsafe<T, TType>();

                // Create new processor for this step
                var processor = TProcessor.New();
                for (int i = 0; i < offensive.Length; ++i)
                {
                    // Get local offensive item ref
                    ref var offensiveVec = ref offensive.GetItemRef(i);

                    // Compute new position and set new vector of offensive SOG player
                    var xOffNew3 = processor.DetermineNewPosition(
                        offensiveVec,
                        bestPosition.GetItemRef(i),
                        sdg.GetItemRef(1),
                        r1,
                        r2,
                        parent.OffensiveSOGStepSize);

                    // Clamp new defensive position and store result
                    var clamped = processor.Clamp(
                        lowerBounds.GetItemRef(i),
                        upperBounds.GetItemRef(i),
                        xOffNew3);
                    offensiveVec = clamped;
                }
            }

            /// <summary>
            /// Does not perform any operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Finalize(
                ReadOnlySpan<AdjustSOGPlayersState<TRandom>> intermediateStates)
            { }

        }
    }
}

#endif
