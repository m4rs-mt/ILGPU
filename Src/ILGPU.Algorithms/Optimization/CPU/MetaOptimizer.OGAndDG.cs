// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetaOptimizer.OGAndDG.cs
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
    partial class MetaOptimizer<T, TEvalType>
    {
        /// <summary>
        /// Represents an intermediate parallel processing state for OG and DG state.
        /// </summary>
        private sealed class OGAndDGState
        {
            private readonly T[] nextOG;
            private readonly T[] nextDG;

            /// <summary>
            /// Creates a new intermediate state.
            /// </summary>
            /// <param name="numDimensions">The number of dimensions.</param>
            public OGAndDGState(int numDimensions)
            {
                nextOG = new T[numDimensions];
                nextDG = new T[numDimensions];
            }

            /// <summary>
            /// Returns a span of the given processing type pointing to the next OG.
            /// </summary>
            /// <typeparam name="TType">The processing type.</typeparam>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Span<TType> GetNextOG<TType>() where TType : struct =>
                nextOG.AsSpan().CastUnsafe<T, TType>();

            /// <summary>
            /// Returns a span of the given processing type pointing to the next DG.
            /// </summary>
            /// <typeparam name="TType">The processing type.</typeparam>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Span<TType> GetNextDG<TType>() where TType : struct =>
                nextDG.AsSpan().CastUnsafe<T, TType>();
        }

        /// <summary>
        /// Computes OG and DG information.
        /// </summary>
        /// <typeparam name="TProcessor">The processor type.</typeparam>
        /// <typeparam name="TType">The processor element type.</typeparam>
        private sealed class OGAndDG<TProcessor, TType> :
            ParallelProcessingCache<OGAndDGState, OGAndDG<TProcessor, TType>>,
            IParallelProcessingBody<OGAndDGState>
            where TProcessor : struct, IProcessor<TProcessor, TType>
            where TType : unmanaged
        {
            private readonly MetaOptimizer<T, TEvalType> parent;
            private readonly T convertedM;

            /// <summary>
            /// Creates a new OG and DG computer.
            /// </summary>
            /// <param name="optimizer">The parent optimizer.</param>
            public OGAndDG(MetaOptimizer<T, TEvalType> optimizer)
            {
                parent = optimizer;
                convertedM = T.CreateTruncating(optimizer.M);
            }

            /// <summary>
            /// Returns the current instance.
            /// </summary>
            protected override OGAndDG<TProcessor, TType> CreateBody() => this;

            /// <summary>
            /// Creates an intermediate temporary accumulation array of two times the
            /// dimension size.
            /// </summary>
            protected override OGAndDGState CreateIntermediate() =>
                new(parent.NumPaddedDimensions);

            /// <summary>
            /// Resets the given intermediate state by resetting all values to T.Zero.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            protected override void InitializeIntermediate(
                OGAndDGState intermediateState)
            {
                var nextOG = intermediateState.GetNextOG<TType>();
                var nextDG = intermediateState.GetNextDG<TType>();

                parent.Reset<TProcessor, TType>(nextOG, nextDG);
            }

            /// <summary>
            /// Resets parent OG and DG vectors for accumulation purposes.
            /// </summary>
            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public void Initialize()
            {
                // Reset OG and DG vectors
                var og = parent.og.AsSpan().CastUnsafe<T, TType>();
                var dg = parent.dg.AsSpan().CastUnsafe<T, TType>();

                parent.Reset<TProcessor, TType>(og, dg);
            }

            /// <summary>
            /// Accumulates offensive and defensive players into OG and DG vectors.
            /// </summary>
            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public void Process(
                int index,
                ParallelLoopState? loopState,
                OGAndDGState intermediateState)
            {
                // Get offsets and spans for offensive and defensive players
                var indices = parent.indices.AsSpan();
                int offensiveIndex = indices.GetItemRef(index);
                int defensiveIndex = indices.GetItemRef(index + parent.M);

                // Get the actual source views
                var offensive = parent
                    .GetPosition(offensiveIndex)
                    .CastUnsafe<T, TType>();
                var defensive = parent
                    .GetPosition(defensiveIndex)
                    .CastUnsafe<T, TType>();

                // Get the actual target views
                var og = intermediateState.GetNextOG<TType>();
                var dg = intermediateState.GetNextDG<TType>();

                // Accumulate all intermediates
                parent.Accumulate<TProcessor, TType>(
                    og,
                    dg,
                    offensive,
                    defensive);
            }

            /// <summary>
            /// Accumulates all intermediate OG and DG states while averaging the result.
            /// </summary>
            [MethodImpl(
                MethodImplOptions.AggressiveInlining |
                MethodImplOptions.AggressiveOptimization)]
            public void Finalize(ReadOnlySpan<OGAndDGState> intermediateStates)
            {
                var og = parent.og.AsSpan().CastUnsafe<T, TType>();
                var dg = parent.dg.AsSpan().CastUnsafe<T, TType>();

                // Iterate over all dimensions and states accumulate results
                foreach (var state in intermediateStates)
                {
                    var sourceOG = state.GetNextOG<TType>();
                    var sourceDG = state.GetNextDG<TType>();

                    parent.Accumulate<TProcessor, TType>(
                        og,
                        dg,
                        sourceOG,
                        sourceDG);
                }

                // Compute averages over all dimension slices
                parent.ComputeAverage<TProcessor, TType>(
                    og,
                    dg,
                    convertedM);
            }
        }
    }
}

#endif
