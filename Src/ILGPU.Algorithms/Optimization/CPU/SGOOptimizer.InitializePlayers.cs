// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: SGOOptimizer.InitializePlayers.cs
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
        /// A parallel processing state for player initialization based on random number
        /// generators used during placement of players.
        /// </summary>
        /// <typeparam name="TRandom">A random provider type.</typeparam>
        /// <typeparam name="TType">A processing type.</typeparam>
        private class InitializePlayersState<TType, TRandom>
            where TType : unmanaged
            where TRandom : struct, IRandomRangeProvider<TType>
        {
            private TRandom randomProvider;

            /// <summary>
            /// Creates a new initialization state.
            /// </summary>
            /// <param name="random">The random provider to use.</param>
            public InitializePlayersState(TRandom random)
            {
                randomProvider = random;
            }

            /// <summary>
            /// Draws a random number using the given CPU-based RNG provider.
            /// </summary>
            /// <returns>The drawn random number.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TType Next() => randomProvider.Next();
        }

        /// <summary>
        /// A player position initializer.
        /// </summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <typeparam name="TType">The processor element type.</typeparam>
        /// <typeparam name="TRandom">The random provider type.</typeparam>
        private sealed class InitializePlayers<TProcessor, TType, TRandom> :
            ParallelProcessingCache<
                InitializePlayersState<TType, TRandom>,
                InitializePlayers<TProcessor, TType, TRandom>>,
            IParallelProcessingBody<InitializePlayersState<TType, TRandom>>
            where TProcessor : struct, IProcessor<TProcessor, TType>
            where TType : unmanaged
            where TRandom : struct, IRandomRangeProvider<TType>
        {
            private readonly SGOOptimizer<T, TEvalType> parent;
            private readonly Func<SGOOptimizer<T, TEvalType>, TRandom> getRandom;

            /// <summary>
            /// Creates a new player initializer.
            /// </summary>
            /// <param name="optimizer">The parent optimizer.</param>
            /// <param name="createRandom">A function creating a new RNG instance.</param>
            public InitializePlayers(
                SGOOptimizer<T, TEvalType> optimizer,
                Func<SGOOptimizer<T, TEvalType>, TRandom> createRandom)
            {
                parent = optimizer;
                getRandom = createRandom;
            }

            /// <summary>
            /// Returns the current instance.
            /// </summary>
            protected override InitializePlayers<TProcessor, TType, TRandom>
                CreateBody() => this;

            /// <summary>
            /// Creates an intermediate state which uses the parent RNG to create fresh
            /// random numbers in parallel.
            /// </summary>
            protected override InitializePlayersState<TType, TRandom>
                CreateIntermediate() => new(getRandom(parent));

            /// <summary>
            /// Does not perform any operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Initialize() { }

            /// <summary>
            /// Accumulates offensive and defensive players into OG and DG vectors.
            /// </summary>
            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public void Process(
                int index,
                ParallelLoopState? loopState,
                InitializePlayersState<TType, TRandom> intermediateState)
            {
                // Get player and the local bounds
                var player = parent.GetPosition(index).CastUnsafe<T, TType>();
                var lower = parent.lowerBounds.AsSpan().CastUnsafe<T, TType>();
                var upper = parent.upperBounds.AsSpan().CastUnsafe<T, TType>();

                // Initialize a new processor
                var processor = TProcessor.New();

                // Initialize all player positions
                for (int i = 0; i < parent.NumDimensionSlices; ++i)
                {
                    // Draw a new random value
                    var randomValue = intermediateState.Next();

                    // Initialize local position
                    var initialPosition = processor.GetRandomPosition(
                        lower.GetItemRef(i),
                        upper.GetItemRef(i),
                        randomValue);
                    player.GetItemRef(i) = initialPosition;
                }
            }

            /// <summary>
            /// Does not perform any operation.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Finalize(
                ReadOnlySpan<InitializePlayersState<TType, TRandom>>
                    intermediateStates)
            { }
        }
    }
}

#endif
