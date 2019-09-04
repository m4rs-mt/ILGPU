// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                 Copyright (c) 2017-2018 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: XorShift128Plus.cs
//
// Algorithm: https://en.wikipedia.org/wiki/Xorshift
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace ILGPU.Algorithms.Random
{
    /// <summary>
    /// Implements a simple and fast xor-shift rng.
    /// </summary>
    /// <remarks>https://en.wikipedia.org/wiki/Xorshift</remarks>
    [CLSCompliant(false)]
    public struct XorShift128Plus : IEquatable<XorShift128Plus>
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
            Debug.Assert(state0 != 0 || state1 != 0, "State must not be zero");
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
        /// Generates a random uint in [0..ulong.MaxValue].
        /// </summary>
        /// <returns>A random uint in [0..ulong.MaxValue].</returns>
        public ulong Next()
        {
            var x = State0;
            var y = State1;
            State0 = y;
            x ^= x << 23;
            State1 = x ^ y ^ (x >> 17) ^ (y >> 26);
            return State1 + y;
        }

        /// <summary>
        /// Generates a random double in [0..1].
        /// </summary>
        /// <returns>A random double in [0..1].</returns>
        public double NextDouble()
        {
            return NextLong() * RandomExtensions.InverseLongDoubleRange;
        }

        /// <summary>
        /// Generates a random float in [0..1].
        /// </summary>
        /// <returns>A random float in [0..1].</returns>
        public float NextFloat()
        {
            return NextLong() * RandomExtensions.InverseLongFloatRange;
        }

        /// <summary>
        /// Generates a random int in [0..long.MaxValue].
        /// </summary>
        /// <returns>A random int in [0..long.MaxValue].</returns>
        public long NextLong()
        {
            return (long)(Next() & 0x7FFFFFFFFFFFFFFFUL);
        }

        /// <summary>
        /// Generates a random int in [minValue..maxValue[.
        /// </summary>
        /// <param name="minValue">The minimum value (inclusive)</param>
        /// <param name="maxValue">The maximum values (exclusive)</param>
        /// <returns>A random int in [minValue..maxValue[.</returns>
        public long Next(long minValue, long maxValue)
        {
            Debug.Assert(minValue < maxValue, "Values out of range");
            var dist = maxValue - minValue;
            return Math.Min((int)(NextFloat() * dist) + minValue, maxValue - 1);
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given object is equal to the current rng.
        /// </summary>
        /// <param name="other">The other rng to test.</param>
        /// <returns>True, iff the given object is equal to the current rng.</returns>
        public bool Equals(XorShift128Plus other)
        {
            return this == other;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the hash code of this rng.
        /// </summary>
        /// <returns>The hash code of this rng.</returns>
        public override int GetHashCode()
        {
            return (int)(State0 ^ State1);
        }

        /// <summary>
        /// Returns true iff the given object is equal to the current rng.
        /// </summary>
        /// <param name="obj">The other rng to test.</param>
        /// <returns>True, iff the given object is equal to the current rng.</returns>
        public override bool Equals(object obj)
        {
            if (obj is XorShift128Plus shift)
                return Equals(shift);
            return false;
        }

        /// <summary>
        /// Returns the string representation of this rng.
        /// </summary>
        /// <returns>The string representation of this rng.</returns>
        public override string ToString()
        {
            return $"[{State0}, {State1}]";
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and second rng are the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, iff the first and second rng are the same.</returns>
        public static bool operator ==(XorShift128Plus first, XorShift128Plus second)
        {
            return first.State0 == second.State0 && first.State1 == second.State1;
        }

        /// <summary>
        /// Returns true iff the first and second rng are not the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, iff the first and second rng are not the same.</returns>
        public static bool operator !=(XorShift128Plus first, XorShift128Plus second)
        {
            return first.State0 != second.State0 || first.State1 != second.State1;
        }

        #endregion
    }
}
