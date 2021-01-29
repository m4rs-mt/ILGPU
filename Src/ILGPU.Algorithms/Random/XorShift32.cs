// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright (c) 2017-2018 ILGPU Samples Project
//                                    www.ilgpu.net
//
// File: XorShift32.cs
//
// Algorithm: https://en.wikipedia.org/wiki/Xorshift
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
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
    public struct XorShift32 : IEquatable<XorShift32>, IRandomProvider<XorShift32>
    {
        #region Static

        /// <summary>
        /// Creates a new rng instance with the help of a CPU-based rng.
        /// </summary>
        /// <param name="random">The desired rng.</param>
        /// <returns>A new rng instance.</returns>
        public static XorShift32 Create(System.Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            var state = (uint)random.Next(1, int.MaxValue);
            return new XorShift32(state);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new rng instance.
        /// </summary>
        /// <param name="state">The initial state value.</param>
        public XorShift32(uint state)
        {
            Trace.Assert(state != 0, "State must not be zero");
            State = state;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The internal state value.
        /// </summary>
        public uint State { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Generates a random uint in [0..uint.MaxValue].
        /// </summary>
        /// <returns>A random uint in [0..uint.MaxValue].</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt()
        {
            uint x = State;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State = x;
            return x;
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
        public void ShiftPeriod(int shift) => State = ShiftState(State, shift);

        /// <inheritdoc cref="IRandomProvider{TProvider}.NextProvider()"/>
        public XorShift32 NextProvider() => new XorShift32(NextUInt());

        /// <inheritdoc cref="IRandomProvider{TProvider}.CreateProvider(System.Random)"/>
        public readonly XorShift32 CreateProvider(System.Random random) =>
            Create(random);

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given object is equal to the current rng.
        /// </summary>
        /// <param name="other">The other rng to test.</param>
        /// <returns>True, if the given object is equal to the current rng.</returns>
        public readonly bool Equals(XorShift32 other) => this == other;

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
            obj is XorShift32 shift && Equals(shift);

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
        public static bool operator ==(XorShift32 first, XorShift32 second) =>
            first.State == second.State;

        /// <summary>
        /// Returns true if the first and second rng are not the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, if the first and second rng are not the same.</returns>
        public static bool operator !=(XorShift32 first, XorShift32 second) =>
            first.State != second.State;

        #endregion
    }
}
