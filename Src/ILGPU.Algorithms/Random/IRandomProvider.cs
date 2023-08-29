// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRandomProvider.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.Random
{
    /// <summary>
    /// Specifies an abstract RNG provider.
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>
        /// Generates a random int in [0..int.MaxValue].
        /// </summary>
        /// <returns>A random int in [0..int.MaxValue].</returns>
        [SuppressMessage(
            "Naming",
            "CA1716:Identifiers should not match keywords",
            Justification = "Like the method System.Random.Next()")]
        int Next();

        /// <summary>
        /// Generates a random long in [0..long.MaxValue].
        /// </summary>
        /// <returns>A random long in [0..long.MaxValue].</returns>
        long NextLong();

        /// <summary>
        /// Generates a random float in [0..1).
        /// </summary>
        /// <returns>A random float in [0..1).</returns>
        float NextFloat();

        /// <summary>
        /// Generates a random double in [0..1).
        /// </summary>
        /// <returns>A random double in [0..1).</returns>
        double NextDouble();
    }

    /// <summary>
    /// An abstract RNG provider that supports period shifts.
    /// </summary>
    /// <typeparam name="TProvider">The implementing provider type.</typeparam>
    public interface IRandomProvider<TProvider> : IRandomProvider
        where TProvider : struct, IRandomProvider<TProvider>
    {
        /// <summary>
        /// Shifts the current period.
        /// </summary>
        /// <param name="shift">The shift amount.</param>
        void ShiftPeriod(int shift);

        /// <summary>
        /// Instantiates a new provider using the internal random state.
        /// </summary>
        /// <returns>The next provider instance.</returns>
        TProvider NextProvider();

        /// <summary>
        /// Instantiates a new provider using the given random.
        /// </summary>
        /// <param name="random">The parent RNG instance.</param>
        /// <returns>The next provider instance.</returns>
        TProvider CreateProvider(System.Random random);

        /// <summary>
        /// Instantiates a new provider using the given random.
        /// </summary>
        /// <param name="random">The parent RNG instance.</param>
        /// <returns>The next provider instance.</returns>
        TProvider CreateProvider<TRandomProvider>(ref TRandomProvider random)
            where TRandomProvider : struct, IRandomProvider<TRandomProvider>;
    }

    namespace Operations
    {
        /// <summary>
        /// An operation that can be executed using a provider instance.
        /// </summary>
        /// <typeparam name="T">The operation return type.</typeparam>
        /// <typeparam name="TRandomProvider">The provider type.</typeparam>
        public interface IRandomProviderOperation<T, TRandomProvider>
            where T : struct
            where TRandomProvider : struct, IRandomProvider
        {
            /// <summary>
            /// Applies the current operation to the RNG provider.
            /// </summary>
            /// <param name="randomProvider">The RNG provider to use.</param>
            /// <returns>The return value.</returns>
            T Apply(ref TRandomProvider randomProvider);
        }

        /// <summary>
        /// Invokes <see cref="IRandomProvider.Next"/> on a RNG state.
        /// </summary>
        /// <typeparam name="TRandomProvider">The provider type.</typeparam>
        public readonly struct NextIntOperation<TRandomProvider> :
            IRandomProviderOperation<int, TRandomProvider>
            where TRandomProvider : struct, IRandomProvider
        {
            /// <inheritdoc cref="IRandomProviderOperation{T, TRandomProvider}.
            /// Apply(ref TRandomProvider)"/>"
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Apply(ref TRandomProvider randomProvider) =>
                randomProvider.Next();
        }

        /// <summary>
        /// Invokes <see cref="IRandomProvider.NextLong"/> on a RNG state.
        /// </summary>
        /// <typeparam name="TRandomProvider">The provider type.</typeparam>
        public readonly struct NextLongOperation<TRandomProvider> :
            IRandomProviderOperation<long, TRandomProvider>
            where TRandomProvider : struct, IRandomProvider
        {
            /// <inheritdoc cref="IRandomProviderOperation{T, TRandomProvider}.
            /// Apply(ref TRandomProvider)"/>"
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public long Apply(ref TRandomProvider randomProvider) =>
                randomProvider.NextLong();
        }

        /// <summary>
        /// Invokes <see cref="IRandomProvider.NextFloat"/> on a RNG state.
        /// </summary>
        /// <typeparam name="TRandomProvider">The provider type.</typeparam>
        public readonly struct NextFloatOperation<TRandomProvider> :
            IRandomProviderOperation<float, TRandomProvider>
            where TRandomProvider : struct, IRandomProvider
        {
            /// <inheritdoc cref="IRandomProviderOperation{T, TRandomProvider}.
            /// Apply(ref TRandomProvider)"/>"
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float Apply(ref TRandomProvider randomProvider) =>
                randomProvider.NextFloat();
        }

        /// <summary>
        /// Invokes <see cref="IRandomProvider.NextDouble"/> on a RNG state.
        /// </summary>
        /// <typeparam name="TRandomProvider">The provider type.</typeparam>
        public readonly struct NextDoubleOperation<TRandomProvider> :
            IRandomProviderOperation<double, TRandomProvider>
            where TRandomProvider : struct, IRandomProvider
        {
            /// <inheritdoc cref="IRandomProviderOperation{T, TRandomProvider}.
            /// Apply(ref TRandomProvider)"/>"
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public double Apply(ref TRandomProvider randomProvider) =>
                randomProvider.NextDouble();
        }
    }
}
