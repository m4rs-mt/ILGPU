// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: XorShift64Star.cs
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
    public struct XorShift64Star :
        IEquatable<XorShift64Star>,
        IRandomProvider<XorShift64Star>
    {
        #region Static

        /// <summary>
        /// Creates a new rng instance with the help of a CPU-based rng.
        /// </summary>
        /// <param name="random">The desired rng.</param>
        /// <returns>A new rng instance.</returns>
        public static XorShift64Star Create(System.Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            var state0 = (ulong)random.Next(1, int.MaxValue) << 32;
            var state1 = (ulong)random.Next();
            return new XorShift64Star(state0 | state1);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new rng instance.
        /// </summary>
        /// <param name="state">The initial state value.</param>
        public XorShift64Star(ulong state)
        {
            Trace.Assert(state != 0, "State must not be zero");
            State = state;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The internal state value.
        /// </summary>
        public ulong State { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Generates a random ulong in [0..ulong.MaxValue].
        /// </summary>
        /// <returns>A random ulong in [0..ulong.MaxValue].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong NextULong()
        {
            var x = State;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            State = x;
            return x * 0x2545F4914F6CDD1DUL;
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
        public void ShiftPeriod(int shift) => State = ShiftState(State, shift);

        /// <inheritdoc cref="IRandomProvider{TProvider}.NextProvider()"/>
        public XorShift64Star NextProvider() => new XorShift64Star(NextULong());

        /// <inheritdoc cref="IRandomProvider{TProvider}.CreateProvider(System.Random)"/>
        public readonly XorShift64Star CreateProvider(System.Random random) =>
            Create(random);

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given object is equal to the current rng.
        /// </summary>
        /// <param name="other">The other rng to test.</param>
        /// <returns>True, if the given object is equal to the current rng.</returns>
        public readonly bool Equals(XorShift64Star other) => this == other;

        #endregion

        #region Object

        /// <summary>
        /// Returns the hash code of this rng.
        /// </summary>
        /// <returns>The hash code of this rng.</returns>
        public readonly override int GetHashCode() => (int)State;

        /// <summary>
        /// Returns true if the given object is equal to the current rng.
        /// </summary>
        /// <param name="obj">The other rng to test.</param>
        /// <returns>True, if the given object is equal to the current rng.</returns>
        public readonly override bool Equals(object obj) =>
            obj is XorShift64Star shift && Equals(shift);

        /// <summary>
        /// Returns the string representation of this rng.
        /// </summary>
        /// <returns>The string representation of this rng.</returns>
        public readonly override string ToString() => State.ToString();

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the first and second rng are the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, if the first and second rng are the same.</returns>
        public static bool operator ==(XorShift64Star first, XorShift64Star second) =>
            first.State == second.State;

        /// <summary>
        /// Returns true if the first and second rng are not the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, if the first and second rng are not the same.</returns>
        public static bool operator !=(XorShift64Star first, XorShift64Star second) =>
            first.State != second.State;

        #endregion
    }
}
