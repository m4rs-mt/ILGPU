// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: XorShift128Plus.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static ILGPU.Algorithms.Random.RandomExtensions;

namespace ILGPU.Algorithms.Random
{
    /// <summary>
    /// Implements a simple and fast xor-shift rng.
    /// </summary>
    /// <remarks>https://en.wikipedia.org/wiki/Xorshift</remarks>
    public struct XorShift128Plus :
        IEquatable<XorShift128Plus>,
        IRandomProvider<XorShift128Plus>
    {
        #region Static

        /// <summary>
        /// Creates a new rng instance with the help of a CPU-based rng.
        /// </summary>
        /// <param name="random">The desired rng.</param>
        /// <returns>A new rng instance.</returns>
        public static XorShift128Plus Create(System.Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            var state0 = (ulong)random.Next(1, int.MaxValue) << 32;
            var state1 = (ulong)random.Next();
            var state2 = (ulong)random.Next() << 32;
            var state3 = (ulong)random.Next();
            return new XorShift128Plus(state0 | state1, state2 | state3);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new rng instance.
        /// </summary>
        /// <param name="state0">The initial state value 0.</param>
        /// <param name="state1">The initial state value 1.</param>
        public XorShift128Plus(ulong state0, ulong state1)
        {
            Trace.Assert(state0 != 0 || state1 != 0, "State must not be zero");
            State0 = state0;
            State1 = state1;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The internal state value 0.
        /// </summary>
        public ulong State0 { get; private set; }

        /// <summary>
        /// The internal state value 1.
        /// </summary>
        public ulong State1 { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Generates a random ulong in [0..ulong.MaxValue].
        /// </summary>
        /// <returns>A random ulong in [0..ulong.MaxValue].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong NextULong()
        {
            var x = State0;
            var y = State1;
            State0 = y;
            x ^= x << 23;
            State1 = x ^ y ^ (x >> 17) ^ (y >> 26);
            return State1 + y;
        }

        /// <summary>
        /// Generates a random uint in [0..uint.MaxValue].
        /// </summary>
        /// <returns>A random uint in [0..uint.MaxValue].</returns>
        public uint NextUInt() => MergeULong(NextULong());

        /// <inheritdoc cref="IRandomProvider.Next"/>
        public int Next() => ToInt(NextUInt());

        /// <inheritdoc cref="IRandomProvider.NextLong"/>
        public long NextLong() => ToLong(NextULong());

        /// <inheritdoc cref="IRandomProvider.NextFloat"/>
        public float NextFloat() => NextLong() * InverseLongFloatRange;

        /// <inheritdoc cref="IRandomProvider.NextDouble"/>
        public double NextDouble() => NextLong() * InverseLongDoubleRange;

        /// <inheritdoc cref="IRandomProvider{TProvider}.ShiftPeriod(int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ShiftPeriod(int shift)
        {
            var localShift = new XorShift128(
                NextUInt(),
                NextUInt(),
                NextUInt(),
                NextUInt());
            localShift.ShiftPeriod(shift);

            State0 = localShift.NextULong();
            State1 = localShift.NextULong();
        }

        /// <inheritdoc cref="IRandomProvider{TProvider}.NextProvider()"/>
        public XorShift128Plus NextProvider() =>
            new XorShift128Plus(
                NextULong(),
                NextULong());

        /// <inheritdoc cref="IRandomProvider{TProvider}.CreateProvider(System.Random)"/>
        public readonly XorShift128Plus CreateProvider(System.Random random) =>
            Create(random);

        /// <summary>
        /// Creates a new provider based on the input instance.
        /// </summary>
        /// <param name="random">The random instance.</param>
        /// <returns>The created provider.</returns>
        public readonly XorShift128Plus CreateProvider<TRandomProvider>(
            ref TRandomProvider random)
            where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
            new XorShift128Plus(
                (ulong)random.NextLong() + 1UL,
                (ulong)random.NextLong() + 1UL);

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given object is equal to the current rng.
        /// </summary>
        /// <param name="other">The other rng to test.</param>
        /// <returns>True, if the given object is equal to the current rng.</returns>
        public readonly bool Equals(XorShift128Plus other) => this == other;

        #endregion

        #region Object

        /// <summary>
        /// Returns the hash code of this rng.
        /// </summary>
        /// <returns>The hash code of this rng.</returns>
        public readonly override int GetHashCode() => (int)(State0 ^ State1);

        /// <summary>
        /// Returns true if the given object is equal to the current rng.
        /// </summary>
        /// <param name="obj">The other rng to test.</param>
        /// <returns>True, if the given object is equal to the current rng.</returns>
        public readonly override bool Equals(object? obj) =>
            obj is XorShift128Plus shift && Equals(shift);

        /// <summary>
        /// Returns the string representation of this rng.
        /// </summary>
        /// <returns>The string representation of this rng.</returns>
        public readonly override string ToString() => $"[{State0}, {State1}]";

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the first and second rng are the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, if the first and second rng are the same.</returns>
        public static bool operator ==(XorShift128Plus first, XorShift128Plus second) =>
            first.State0 == second.State0 && first.State1 == second.State1;

        /// <summary>
        /// Returns true if the first and second rng are not the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, if the first and second rng are not the same.</returns>
        public static bool operator !=(XorShift128Plus first, XorShift128Plus second) =>
            first.State0 != second.State0 || first.State1 != second.State1;

        #endregion
    }
}
