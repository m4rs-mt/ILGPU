// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: RandomRanges.tt/RandomRanges.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeInformation.ttinclude
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeInformation.ttinclude
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

#pragma warning disable CA1000 // No static members on generic types
#pragma warning disable IDE0004 // Cast is redundant

#if NET7_0_OR_GREATER

namespace ILGPU.Algorithms.Random
{
    /// <summary>
    /// A generic random number range operating on a generic type
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The element type to operate on.</typeparam>
    public interface IBasicRandomRange<out T>
        where T : struct
    {
        /// <summary>
        /// Returns the min value of this range (inclusive).
        /// </summary>
        T MinValue { get; }

        /// <summary>
        /// Returns the max value of this range (exclusive).
        /// </summary>
        T MaxValue { get; }
    }

    /// <summary>
    /// A generic random number range operating on a generic type
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The element type to operate on.</typeparam>
    public interface IRandomRange<out T> : IBasicRandomRange<T>
        where T : struct
    {
        /// <summary>
        /// Generates a new random value by taking min and max value ranges into account.
        /// </summary>
        /// <typeparam name="TRandomProvider">The random provider type.</typeparam>
        /// <param name="randomProvider">The random provider instance.</param>
        /// <returns>The retrieved random value.</returns>
        /// <remarks>
        /// CAUTION: This function implementation is meant to be thread safe in general to
        /// support massively parallel evaluations on CPU and GPU.
        /// </remarks>
        [SuppressMessage(
            "Naming",
            "CA1716:Identifiers should not match keywords",
            Justification = "Like the method System.Random.Next()")]
        T Next<TRandomProvider>(ref TRandomProvider randomProvider)
            where TRandomProvider : struct, IRandomProvider;
    }

    /// <summary>
    /// A generic random number range provider operating on a generic type
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The element type to operate on.</typeparam>
    /// <remarks>
    /// CAUTION: A type implementing this interface is meant to be thread safe in general
    /// to support massively parallel evaluations on CPU and GPU.
    /// </remarks>
    public interface IRandomRangeProvider<T>
        where T : struct
    {
        /// <summary>
        /// Generates a new random value by taking min and max value ranges into account.
        /// </summary>
        /// <returns>The retrieved random value.</returns>
        [SuppressMessage(
            "Naming",
            "CA1716:Identifiers should not match keywords",
            Justification = "Like the method System.Random.Next()")]
        T Next();
    }

    /// <summary>
    /// A generic random number range provider operating on a generic type
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="TSelf">The type implementing this interface.</typeparam>
    /// <typeparam name="T">The element type to operate on.</typeparam>
    /// <remarks>
    /// CAUTION: A type implementing this interface is meant to be thread safe in general
    /// to support massively parallel evaluations on CPU and GPU.
    /// </remarks>
    [CLSCompliant(false)]
    public interface IRandomRangeProvider<TSelf, T> :
        IRandomRangeProvider<T>, IBasicRandomRange<T>
        where TSelf : struct, IRandomRangeProvider<TSelf, T>
        where T : unmanaged
    {
        /// <summary>
        /// Instantiates a new random range using the given random provider.
        /// </summary>
        /// <param name="random">The parent RNG instance.</param>
        /// <param name="minValue">The minimum value (inclusive).</param>
        /// <param name="maxValue">The maximum value (exclusive).</param>
        static abstract TSelf Create(System.Random random, T minValue, T maxValue);

        /// <summary>
        /// Instantiates a new random range using the given random provider.
        /// </summary>
        /// <param name="random">The parent RNG instance.</param>
        /// <param name="minValue">The minimum value (inclusive).</param>
        /// <param name="maxValue">The maximum value (exclusive).</param>
        static abstract TSelf Create<TOtherProvider>(
            ref TOtherProvider random,
            T minValue,
            T maxValue)
            where TOtherProvider : struct, IRandomProvider<TOtherProvider>;

        /// <summary>
        /// Creates a new random range vector provider compatible with this provider.
        /// </summary>
        RandomRangeVectorProvider<T, TSelf> CreateVectorProvider();
    }

    /// <summary>
    /// Represents a default RNG range for vectors types returning specified value
    /// intervals for type Vector.
    /// </summary>
    /// <typeparam name="T">The vector element type.</typeparam>
    /// <typeparam name="TRangeProvider">The underlying range provider.</typeparam>
    [CLSCompliant(false)]
    public struct RandomRangeVectorProvider<T, TRangeProvider> :
        IRandomRangeProvider<Vector<T>>,
        IRandomRangeProvider<T>,
        IBasicRandomRange<T>
        where T : unmanaged
        where TRangeProvider : struct, IRandomRangeProvider<TRangeProvider, T>
    {
        private TRangeProvider rangeProvider;

        /// <summary>
        /// Instantiates a new random range provider using the given random provider.
        /// </summary>
        /// <param name="provider">The RNG provider to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RandomRangeVectorProvider(TRangeProvider provider)
        {
            rangeProvider = provider;
        }

        /// <summary>
        /// Returns the min value of this range (inclusive).
        /// </summary>
        public readonly T MinValue => rangeProvider.MinValue;

        /// <summary>
        /// Returns the max value of this range (exclusive).
        /// </summary>
        public readonly T MaxValue => rangeProvider.MaxValue;

        /// <summary>
        /// Generates a new random value using the given min and max values.
        /// </summary>
        [SuppressMessage(
            "Naming",
            "CA1716:Identifiers should not match keywords",
            Justification = "Like the method System.Random.Next()")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector<T> Next() =>
            RandomExtensions.NextVector<T, TRangeProvider>(ref rangeProvider);

        /// <summary>
        /// Generates a new random value using the given min and max values.
        /// </summary>
        [SuppressMessage(
            "Naming",
            "CA1716:Identifiers should not match keywords",
            Justification = "Like the method System.Random.Next()")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        T IRandomRangeProvider<T>.Next() => rangeProvider.Next();
    }

    /// <summary>
    /// A container class holding specialized random range instances while providing
    /// specialized extension methods for different RNG providers.
    /// </summary>
    public static class RandomRanges
    {
        /// <summary>
        /// Represents a default RNG range for type Int8 returning
        /// specified value intervals for type Int8 (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <param name="MinValue">The minimum value (inclusive).</param>
        /// <param name="MaxValue">The maximum values (exclusive).</param>
        [CLSCompliant(false)]
        public readonly record struct RandomRangeInt8(
            sbyte MinValue,
            sbyte MaxValue) :
            IRandomRange<sbyte>
        {
            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeInt8Provider<TRandomProvider>
                CreateProvider<TRandomProvider>(System.Random random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeInt8Provider<TRandomProvider>.Create(
                    random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeInt8Provider<TRandomProvider>
                CreateProvider<TRandomProvider>(ref TRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeInt8Provider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeInt8Provider<TRandomProvider>
                CreateProvider<TRandomProvider, TOtherRandomProvider>(
                ref TOtherRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider>
                where TOtherRandomProvider :
                    struct, IRandomProvider<TOtherRandomProvider> =>
                RandomRangeInt8Provider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public sbyte Next<TRandomProvider>(
                ref TRandomProvider randomProvider)
                where TRandomProvider : struct, IRandomProvider =>
                (sbyte)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Int8 returning
        /// specified value intervals for type Int8 (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <typeparam name="TRandomProvider">The underlying random provider.</typeparam>
        [CLSCompliant(false)]
        public struct RandomRangeInt8Provider<TRandomProvider> :
            IRandomRangeProvider<
                RandomRangeInt8Provider<TRandomProvider>,
                sbyte>
            where TRandomProvider : struct, IRandomProvider<TRandomProvider>
        {
            private TRandomProvider randomProvider;

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The RNG instance to use.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            public RandomRangeInt8Provider(
                TRandomProvider random,
                sbyte minValue,
                sbyte maxValue)
            {
                randomProvider = random;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            /// <summary>
            /// Returns the min value of this range (inclusive).
            /// </summary>
            public sbyte MinValue { get; }

            /// <summary>
            /// Returns the max value of this range (exclusive).
            /// </summary>
            public sbyte MaxValue { get; }

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeInt8Provider<TRandomProvider>
                Create(
                System.Random random,
                sbyte minValue,
                sbyte maxValue) =>
                new(default(TRandomProvider).CreateProvider(random), minValue, maxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeInt8Provider<TRandomProvider>
                Create<TOtherProvider>(
                ref TOtherProvider random,
                sbyte minValue,
                sbyte maxValue)
                where TOtherProvider : struct, IRandomProvider<TOtherProvider> =>
                new(
                    default(TRandomProvider).CreateProvider(ref random),
                    minValue,
                    maxValue);

            /// <summary>
            /// Creates a new random range vector provider compatible with this provider.
            /// </summary>
            public readonly RandomRangeVectorProvider<
                sbyte,
                RandomRangeInt8Provider<TRandomProvider>> CreateVectorProvider() =>
                new(this);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public sbyte Next() =>
                (sbyte)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Int16 returning
        /// specified value intervals for type Int16 (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <param name="MinValue">The minimum value (inclusive).</param>
        /// <param name="MaxValue">The maximum values (exclusive).</param>
        [CLSCompliant(false)]
        public readonly record struct RandomRangeInt16(
            short MinValue,
            short MaxValue) :
            IRandomRange<short>
        {
            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeInt16Provider<TRandomProvider>
                CreateProvider<TRandomProvider>(System.Random random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeInt16Provider<TRandomProvider>.Create(
                    random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeInt16Provider<TRandomProvider>
                CreateProvider<TRandomProvider>(ref TRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeInt16Provider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeInt16Provider<TRandomProvider>
                CreateProvider<TRandomProvider, TOtherRandomProvider>(
                ref TOtherRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider>
                where TOtherRandomProvider :
                    struct, IRandomProvider<TOtherRandomProvider> =>
                RandomRangeInt16Provider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public short Next<TRandomProvider>(
                ref TRandomProvider randomProvider)
                where TRandomProvider : struct, IRandomProvider =>
                (short)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Int16 returning
        /// specified value intervals for type Int16 (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <typeparam name="TRandomProvider">The underlying random provider.</typeparam>
        [CLSCompliant(false)]
        public struct RandomRangeInt16Provider<TRandomProvider> :
            IRandomRangeProvider<
                RandomRangeInt16Provider<TRandomProvider>,
                short>
            where TRandomProvider : struct, IRandomProvider<TRandomProvider>
        {
            private TRandomProvider randomProvider;

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The RNG instance to use.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            public RandomRangeInt16Provider(
                TRandomProvider random,
                short minValue,
                short maxValue)
            {
                randomProvider = random;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            /// <summary>
            /// Returns the min value of this range (inclusive).
            /// </summary>
            public short MinValue { get; }

            /// <summary>
            /// Returns the max value of this range (exclusive).
            /// </summary>
            public short MaxValue { get; }

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeInt16Provider<TRandomProvider>
                Create(
                System.Random random,
                short minValue,
                short maxValue) =>
                new(default(TRandomProvider).CreateProvider(random), minValue, maxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeInt16Provider<TRandomProvider>
                Create<TOtherProvider>(
                ref TOtherProvider random,
                short minValue,
                short maxValue)
                where TOtherProvider : struct, IRandomProvider<TOtherProvider> =>
                new(
                    default(TRandomProvider).CreateProvider(ref random),
                    minValue,
                    maxValue);

            /// <summary>
            /// Creates a new random range vector provider compatible with this provider.
            /// </summary>
            public readonly RandomRangeVectorProvider<
                short,
                RandomRangeInt16Provider<TRandomProvider>> CreateVectorProvider() =>
                new(this);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public short Next() =>
                (short)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Int32 returning
        /// specified value intervals for type Int32 (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <param name="MinValue">The minimum value (inclusive).</param>
        /// <param name="MaxValue">The maximum values (exclusive).</param>
        [CLSCompliant(false)]
        public readonly record struct RandomRangeInt32(
            int MinValue,
            int MaxValue) :
            IRandomRange<int>
        {
            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeInt32Provider<TRandomProvider>
                CreateProvider<TRandomProvider>(System.Random random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeInt32Provider<TRandomProvider>.Create(
                    random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeInt32Provider<TRandomProvider>
                CreateProvider<TRandomProvider>(ref TRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeInt32Provider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeInt32Provider<TRandomProvider>
                CreateProvider<TRandomProvider, TOtherRandomProvider>(
                ref TOtherRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider>
                where TOtherRandomProvider :
                    struct, IRandomProvider<TOtherRandomProvider> =>
                RandomRangeInt32Provider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Next<TRandomProvider>(
                ref TRandomProvider randomProvider)
                where TRandomProvider : struct, IRandomProvider =>
                (int)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Int32 returning
        /// specified value intervals for type Int32 (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <typeparam name="TRandomProvider">The underlying random provider.</typeparam>
        [CLSCompliant(false)]
        public struct RandomRangeInt32Provider<TRandomProvider> :
            IRandomRangeProvider<
                RandomRangeInt32Provider<TRandomProvider>,
                int>
            where TRandomProvider : struct, IRandomProvider<TRandomProvider>
        {
            private TRandomProvider randomProvider;

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The RNG instance to use.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            public RandomRangeInt32Provider(
                TRandomProvider random,
                int minValue,
                int maxValue)
            {
                randomProvider = random;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            /// <summary>
            /// Returns the min value of this range (inclusive).
            /// </summary>
            public int MinValue { get; }

            /// <summary>
            /// Returns the max value of this range (exclusive).
            /// </summary>
            public int MaxValue { get; }

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeInt32Provider<TRandomProvider>
                Create(
                System.Random random,
                int minValue,
                int maxValue) =>
                new(default(TRandomProvider).CreateProvider(random), minValue, maxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeInt32Provider<TRandomProvider>
                Create<TOtherProvider>(
                ref TOtherProvider random,
                int minValue,
                int maxValue)
                where TOtherProvider : struct, IRandomProvider<TOtherProvider> =>
                new(
                    default(TRandomProvider).CreateProvider(ref random),
                    minValue,
                    maxValue);

            /// <summary>
            /// Creates a new random range vector provider compatible with this provider.
            /// </summary>
            public readonly RandomRangeVectorProvider<
                int,
                RandomRangeInt32Provider<TRandomProvider>> CreateVectorProvider() =>
                new(this);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Next() =>
                (int)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Int64 returning
        /// specified value intervals for type Int64 (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <param name="MinValue">The minimum value (inclusive).</param>
        /// <param name="MaxValue">The maximum values (exclusive).</param>
        [CLSCompliant(false)]
        public readonly record struct RandomRangeInt64(
            long MinValue,
            long MaxValue) :
            IRandomRange<long>
        {
            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeInt64Provider<TRandomProvider>
                CreateProvider<TRandomProvider>(System.Random random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeInt64Provider<TRandomProvider>.Create(
                    random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeInt64Provider<TRandomProvider>
                CreateProvider<TRandomProvider>(ref TRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeInt64Provider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeInt64Provider<TRandomProvider>
                CreateProvider<TRandomProvider, TOtherRandomProvider>(
                ref TOtherRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider>
                where TOtherRandomProvider :
                    struct, IRandomProvider<TOtherRandomProvider> =>
                RandomRangeInt64Provider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public long Next<TRandomProvider>(
                ref TRandomProvider randomProvider)
                where TRandomProvider : struct, IRandomProvider =>
                (long)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Int64 returning
        /// specified value intervals for type Int64 (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <typeparam name="TRandomProvider">The underlying random provider.</typeparam>
        [CLSCompliant(false)]
        public struct RandomRangeInt64Provider<TRandomProvider> :
            IRandomRangeProvider<
                RandomRangeInt64Provider<TRandomProvider>,
                long>
            where TRandomProvider : struct, IRandomProvider<TRandomProvider>
        {
            private TRandomProvider randomProvider;

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The RNG instance to use.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            public RandomRangeInt64Provider(
                TRandomProvider random,
                long minValue,
                long maxValue)
            {
                randomProvider = random;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            /// <summary>
            /// Returns the min value of this range (inclusive).
            /// </summary>
            public long MinValue { get; }

            /// <summary>
            /// Returns the max value of this range (exclusive).
            /// </summary>
            public long MaxValue { get; }

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeInt64Provider<TRandomProvider>
                Create(
                System.Random random,
                long minValue,
                long maxValue) =>
                new(default(TRandomProvider).CreateProvider(random), minValue, maxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeInt64Provider<TRandomProvider>
                Create<TOtherProvider>(
                ref TOtherProvider random,
                long minValue,
                long maxValue)
                where TOtherProvider : struct, IRandomProvider<TOtherProvider> =>
                new(
                    default(TRandomProvider).CreateProvider(ref random),
                    minValue,
                    maxValue);

            /// <summary>
            /// Creates a new random range vector provider compatible with this provider.
            /// </summary>
            public readonly RandomRangeVectorProvider<
                long,
                RandomRangeInt64Provider<TRandomProvider>> CreateVectorProvider() =>
                new(this);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public long Next() =>
                (long)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Half returning
        /// specified value intervals for type Half (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <param name="MinValue">The minimum value (inclusive).</param>
        /// <param name="MaxValue">The maximum values (exclusive).</param>
        [CLSCompliant(false)]
        public readonly record struct RandomRangeHalf(
            Half MinValue,
            Half MaxValue) :
            IRandomRange<Half>
        {
            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeHalfProvider<TRandomProvider>
                CreateProvider<TRandomProvider>(System.Random random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeHalfProvider<TRandomProvider>.Create(
                    random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeHalfProvider<TRandomProvider>
                CreateProvider<TRandomProvider>(ref TRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeHalfProvider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeHalfProvider<TRandomProvider>
                CreateProvider<TRandomProvider, TOtherRandomProvider>(
                ref TOtherRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider>
                where TOtherRandomProvider :
                    struct, IRandomProvider<TOtherRandomProvider> =>
                RandomRangeHalfProvider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Half Next<TRandomProvider>(
                ref TRandomProvider randomProvider)
                where TRandomProvider : struct, IRandomProvider =>
                (Half)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Half returning
        /// specified value intervals for type Half (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <typeparam name="TRandomProvider">The underlying random provider.</typeparam>
        [CLSCompliant(false)]
        public struct RandomRangeHalfProvider<TRandomProvider> :
            IRandomRangeProvider<
                RandomRangeHalfProvider<TRandomProvider>,
                Half>
            where TRandomProvider : struct, IRandomProvider<TRandomProvider>
        {
            private TRandomProvider randomProvider;

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The RNG instance to use.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            public RandomRangeHalfProvider(
                TRandomProvider random,
                Half minValue,
                Half maxValue)
            {
                randomProvider = random;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            /// <summary>
            /// Returns the min value of this range (inclusive).
            /// </summary>
            public Half MinValue { get; }

            /// <summary>
            /// Returns the max value of this range (exclusive).
            /// </summary>
            public Half MaxValue { get; }

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeHalfProvider<TRandomProvider>
                Create(
                System.Random random,
                Half minValue,
                Half maxValue) =>
                new(default(TRandomProvider).CreateProvider(random), minValue, maxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeHalfProvider<TRandomProvider>
                Create<TOtherProvider>(
                ref TOtherProvider random,
                Half minValue,
                Half maxValue)
                where TOtherProvider : struct, IRandomProvider<TOtherProvider> =>
                new(
                    default(TRandomProvider).CreateProvider(ref random),
                    minValue,
                    maxValue);

            /// <summary>
            /// Creates a new random range vector provider compatible with this provider.
            /// </summary>
            public readonly RandomRangeVectorProvider<
                Half,
                RandomRangeHalfProvider<TRandomProvider>> CreateVectorProvider() =>
                new(this);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Half Next() =>
                (Half)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Float returning
        /// specified value intervals for type Float (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <param name="MinValue">The minimum value (inclusive).</param>
        /// <param name="MaxValue">The maximum values (exclusive).</param>
        [CLSCompliant(false)]
        public readonly record struct RandomRangeFloat(
            float MinValue,
            float MaxValue) :
            IRandomRange<float>
        {
            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeFloatProvider<TRandomProvider>
                CreateProvider<TRandomProvider>(System.Random random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeFloatProvider<TRandomProvider>.Create(
                    random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeFloatProvider<TRandomProvider>
                CreateProvider<TRandomProvider>(ref TRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeFloatProvider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeFloatProvider<TRandomProvider>
                CreateProvider<TRandomProvider, TOtherRandomProvider>(
                ref TOtherRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider>
                where TOtherRandomProvider :
                    struct, IRandomProvider<TOtherRandomProvider> =>
                RandomRangeFloatProvider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float Next<TRandomProvider>(
                ref TRandomProvider randomProvider)
                where TRandomProvider : struct, IRandomProvider =>
                (float)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Float returning
        /// specified value intervals for type Float (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <typeparam name="TRandomProvider">The underlying random provider.</typeparam>
        [CLSCompliant(false)]
        public struct RandomRangeFloatProvider<TRandomProvider> :
            IRandomRangeProvider<
                RandomRangeFloatProvider<TRandomProvider>,
                float>
            where TRandomProvider : struct, IRandomProvider<TRandomProvider>
        {
            private TRandomProvider randomProvider;

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The RNG instance to use.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            public RandomRangeFloatProvider(
                TRandomProvider random,
                float minValue,
                float maxValue)
            {
                randomProvider = random;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            /// <summary>
            /// Returns the min value of this range (inclusive).
            /// </summary>
            public float MinValue { get; }

            /// <summary>
            /// Returns the max value of this range (exclusive).
            /// </summary>
            public float MaxValue { get; }

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeFloatProvider<TRandomProvider>
                Create(
                System.Random random,
                float minValue,
                float maxValue) =>
                new(default(TRandomProvider).CreateProvider(random), minValue, maxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeFloatProvider<TRandomProvider>
                Create<TOtherProvider>(
                ref TOtherProvider random,
                float minValue,
                float maxValue)
                where TOtherProvider : struct, IRandomProvider<TOtherProvider> =>
                new(
                    default(TRandomProvider).CreateProvider(ref random),
                    minValue,
                    maxValue);

            /// <summary>
            /// Creates a new random range vector provider compatible with this provider.
            /// </summary>
            public readonly RandomRangeVectorProvider<
                float,
                RandomRangeFloatProvider<TRandomProvider>> CreateVectorProvider() =>
                new(this);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float Next() =>
                (float)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Double returning
        /// specified value intervals for type Double (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <param name="MinValue">The minimum value (inclusive).</param>
        /// <param name="MaxValue">The maximum values (exclusive).</param>
        [CLSCompliant(false)]
        public readonly record struct RandomRangeDouble(
            double MinValue,
            double MaxValue) :
            IRandomRange<double>
        {
            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeDoubleProvider<TRandomProvider>
                CreateProvider<TRandomProvider>(System.Random random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeDoubleProvider<TRandomProvider>.Create(
                    random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeDoubleProvider<TRandomProvider>
                CreateProvider<TRandomProvider>(ref TRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
                RandomRangeDoubleProvider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RandomRangeDoubleProvider<TRandomProvider>
                CreateProvider<TRandomProvider, TOtherRandomProvider>(
                ref TOtherRandomProvider random)
                where TRandomProvider : struct, IRandomProvider<TRandomProvider>
                where TOtherRandomProvider :
                    struct, IRandomProvider<TOtherRandomProvider> =>
                RandomRangeDoubleProvider<TRandomProvider>.Create(
                    ref random,
                    MinValue,
                    MaxValue);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public double Next<TRandomProvider>(
                ref TRandomProvider randomProvider)
                where TRandomProvider : struct, IRandomProvider =>
                (double)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

        /// <summary>
        /// Represents a default RNG range for type Double returning
        /// specified value intervals for type Double (in analogy to calling
        /// the appropriate NextXYZ method on the random provider given using min and
        /// max values).
        /// </summary>
        /// <typeparam name="TRandomProvider">The underlying random provider.</typeparam>
        [CLSCompliant(false)]
        public struct RandomRangeDoubleProvider<TRandomProvider> :
            IRandomRangeProvider<
                RandomRangeDoubleProvider<TRandomProvider>,
                double>
            where TRandomProvider : struct, IRandomProvider<TRandomProvider>
        {
            private TRandomProvider randomProvider;

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The RNG instance to use.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            public RandomRangeDoubleProvider(
                TRandomProvider random,
                double minValue,
                double maxValue)
            {
                randomProvider = random;
                MinValue = minValue;
                MaxValue = maxValue;
            }

            /// <summary>
            /// Returns the min value of this range (inclusive).
            /// </summary>
            public double MinValue { get; }

            /// <summary>
            /// Returns the max value of this range (exclusive).
            /// </summary>
            public double MaxValue { get; }

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeDoubleProvider<TRandomProvider>
                Create(
                System.Random random,
                double minValue,
                double maxValue) =>
                new(default(TRandomProvider).CreateProvider(random), minValue, maxValue);

            /// <summary>
            /// Instantiates a new random range provider using the given random provider.
            /// </summary>
            /// <param name="random">The parent RNG instance.</param>
            /// <param name="minValue">The minimum value (inclusive).</param>
            /// <param name="maxValue">The maximum value (exclusive).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static RandomRangeDoubleProvider<TRandomProvider>
                Create<TOtherProvider>(
                ref TOtherProvider random,
                double minValue,
                double maxValue)
                where TOtherProvider : struct, IRandomProvider<TOtherProvider> =>
                new(
                    default(TRandomProvider).CreateProvider(ref random),
                    minValue,
                    maxValue);

            /// <summary>
            /// Creates a new random range vector provider compatible with this provider.
            /// </summary>
            public readonly RandomRangeVectorProvider<
                double,
                RandomRangeDoubleProvider<TRandomProvider>> CreateVectorProvider() =>
                new(this);

            /// <summary>
            /// Generates a new random value using the given min and max values.
            /// </summary>
            [SuppressMessage(
                "Naming",
                "CA1716:Identifiers should not match keywords",
                Justification = "Like the method System.Random.Next()")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public double Next() =>
                (double)RandomExtensions.Next(
                    ref randomProvider,
                    MinValue,
                    MaxValue);
        }

    }
}

#endif

#pragma warning restore IDE0004
#pragma warning restore CA1000