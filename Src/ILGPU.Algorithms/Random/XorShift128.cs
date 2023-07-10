// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: XorShift128.cs
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
    [CLSCompliant(false)]
    public struct XorShift128 : IEquatable<XorShift128>, IRandomProvider<XorShift128>
    {
        #region Static

        /// <summary>
        /// Creates a new rng instance with the help of a CPU-based rng.
        /// </summary>
        /// <param name="random">The desired rng.</param>
        /// <returns>A new rng instance.</returns>
        public static XorShift128 Create(System.Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            var state0 = (uint)random.Next(1, int.MaxValue);
            var state1 = (uint)random.Next();
            var state2 = (uint)random.Next();
            var state3 = (uint)random.Next();
            return new XorShift128(state0, state1, state2, state3);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new rng instance.
        /// </summary>
        /// <param name="state0">The initial state value 0.</param>
        /// <param name="state1">The initial state value 1.</param>
        /// <param name="state2">The initial state value 2.</param>
        /// <param name="state3">The initial state value 3.</param>
        public XorShift128(uint state0, uint state1, uint state2, uint state3)
        {
            Trace.Assert(state0 != 0 || state1 != 0 ||
                state2 != 0 || state3 != 0, "State must not be zero");
            State0 = state0;
            State1 = state1;
            State2 = state2;
            State3 = state3;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The internal state value 0.
        /// </summary>
        public uint State0 { get; private set; }

        /// <summary>
        /// The internal state value 1.
        /// </summary>
        public uint State1 { get; private set; }

        /// <summary>
        /// The internal state value 2.
        /// </summary>
        public uint State2 { get; private set; }

        /// <summary>
        /// The internal state value 3.
        /// </summary>
        public uint State3 { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Generates a random uint in [0..uint.MaxValue].
        /// </summary>
        /// <returns>A random uint in [0..uint.MaxValue].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt()
        {
            var t = State3;
            t ^= t << 11;
            t ^= t >> 8;
            State3 = State2;
            State2 = State1;
            State1 = State0;
            t ^= State0;
            t ^= State0 >> 19;
            State0 = t;
            return t;
        }

        /// <summary>
        /// Generates a random ulong in [0..ulong.MaxValue].
        /// </summary>
        /// <returns>A random ulong in [0..ulong.MaxValue].</returns>
        public ulong NextULong() => SeperateUInt(NextUInt());

        /// <inheritdoc cref="IRandomProvider.Next"/>
        public int Next() => ToInt(NextUInt());

        /// <inheritdoc cref="IRandomProvider.NextLong"/>
        public long NextLong() => ToLong(NextULong());

        /// <inheritdoc cref="IRandomProvider.NextFloat"/>
        public float NextFloat() => Next() * InverseIntFloatRange;

        /// <inheritdoc cref="IRandomProvider.NextDouble"/>
        public double NextDouble() => NextLong() * InverseLongDoubleRange;

        /// <inheritdoc cref="IRandomProvider{TProvider}.ShiftPeriod(int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ShiftPeriod(int shift)
        {
            var localShift = new XorShift64Star(((ulong)NextUInt() << 32) | NextUInt());
            localShift.ShiftPeriod(shift);

            State0 = localShift.NextUInt();
            State1 = localShift.NextUInt();
            State2 = localShift.NextUInt();
            State3 = localShift.NextUInt();
        }

        /// <inheritdoc cref="IRandomProvider{TProvider}.NextProvider()"/>
        public XorShift128 NextProvider() =>
            new XorShift128(
                NextUInt(),
                NextUInt(),
                NextUInt(),
                NextUInt());

        /// <inheritdoc cref="IRandomProvider{TProvider}.CreateProvider(System.Random)"/>
        public readonly XorShift128 CreateProvider(System.Random random) =>
            Create(random);

        /// <summary>
        /// Creates a new provider based on the input instance.
        /// </summary>
        /// <param name="random">The random instance.</param>
        /// <returns>The created provider.</returns>
        public readonly XorShift128 CreateProvider<TRandomProvider>(
            ref TRandomProvider random)
            where TRandomProvider : struct, IRandomProvider<TRandomProvider> =>
            new XorShift128(
                (uint)random.Next() + 1U,
                (uint)random.Next() + 1U,
                (uint)random.Next() + 1U,
                (uint)random.Next() + 1U);

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given object is equal to the current rng.
        /// </summary>
        /// <param name="other">The other rng to test.</param>
        /// <returns>True, if the given object is equal to the current rng.</returns>
        public readonly bool Equals(XorShift128 other) => this == other;

        #endregion

        #region Object

        /// <summary>
        /// Returns the hash code of this rng.
        /// </summary>
        /// <returns>The hash code of this rng.</returns>
        public readonly override int GetHashCode() =>
            (int)(State0 ^ State1 ^ State2 ^ State3);

        /// <summary>
        /// Returns true if the given object is equal to the current rng.
        /// </summary>
        /// <param name="obj">The other rng to test.</param>
        /// <returns>True, if the given object is equal to the current rng.</returns>
        public readonly override bool Equals(object obj) =>
            obj is XorShift128 shift && Equals(shift);

        /// <summary>
        /// Returns the string representation of this rng.
        /// </summary>
        /// <returns>The string representation of this rng.</returns>
        public readonly override string ToString() =>
            $"[{State0}, {State1}, {State2}, {State3}]";

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the first and second rng are the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, if the first and second rng are the same.</returns>
        public static bool operator ==(XorShift128 first, XorShift128 second) =>
            first.State0 == second.State0 &&
            first.State1 == second.State1 &&
            first.State2 == second.State2 &&
            first.State3 == second.State3;

        /// <summary>
        /// Returns true if the first and second rng are not the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, if the first and second rng are not the same.</returns>
        public static bool operator !=(XorShift128 first, XorShift128 second) =>
            first.State0 != second.State0 ||
            first.State1 != second.State1 ||
            first.State2 != second.State2 ||
            first.State3 != second.State3;

        #endregion
    }
}
