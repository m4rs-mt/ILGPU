// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                   Copyright (c) 2017 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: XorShift32.cs
//
// Algorithm: https://en.wikipedia.org/wiki/Xorshift
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace ILGPU.Lightning.Random
{
    /// <summary>
    /// Implements a simple and fast xor-shift rng.
    /// </summary>
    /// <remarks>https://en.wikipedia.org/wiki/Xorshift</remarks>
    public struct XorShift32 : IEquatable<XorShift32>
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
            Debug.Assert(state != 0, "State must not be zero");
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
        public uint Next()
        {
            var x = State;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State = x;
            return x;
        }

        /// <summary>
        /// Generates a random double in [0..1].
        /// </summary>
        /// <returns>A random double in [0..1].</returns>
        public double NextDouble()
        {
            return NextInt() * RandomExtensions.InverseIntDoubleRange;
        }

        /// <summary>
        /// Generates a random float in [0..1].
        /// </summary>
        /// <returns>A random float in [0..1].</returns>
        public float NextFloat()
        {
            return NextInt() * RandomExtensions.InverseIntFloatRange;
        }

        /// <summary>
        /// Generates a random int in [0..int.MaxValue].
        /// </summary>
        /// <returns>A random int in [0..int.MaxValue].</returns>
        public int NextInt()
        {
            return (int)(Next() & 0x7FFFFFFFU);
        }

        /// <summary>
        /// Generates a random int in [minValue..maxValue].
        /// </summary>
        /// <param name="minValue">The minimum value (inclusive)</param>
        /// <param name="maxValue">The maximum values (inclusive)</param>
        /// <returns>A random int in [minValue..maxValue].</returns>
        public int Next(int minValue, int maxValue)
        {
            Debug.Assert(minValue < maxValue, "Values out of range");
            return (int)(maxValue * NextFloat()) + minValue;
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given object is equal to the current rng.
        /// </summary>
        /// <param name="other">The other rng to test.</param>
        /// <returns>True, iff the given object is equal to the current rng.</returns>
        public bool Equals(XorShift32 other)
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
            return (int)State;
        }

        /// <summary>
        /// Returns true iff the given object is equal to the current rng.
        /// </summary>
        /// <param name="obj">The other rng to test.</param>
        /// <returns>True, iff the given object is equal to the current rng.</returns>
        public override bool Equals(object obj)
        {
            if (obj is XorShift32 shift)
                return Equals(shift);
            return false;
        }

        /// <summary>
        /// Returns the string representation of this rng.
        /// </summary>
        /// <returns>The string representation of this rng.</returns>
        public override string ToString()
        {
            return State.ToString();
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and second rng are the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, iff the first and second rng are the same.</returns>
        public static bool operator ==(XorShift32 first, XorShift32 second)
        {
            return first.State == second.State;
        }

        /// <summary>
        /// Returns true iff the first and second rng are not the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, iff the first and second rng are not the same.</returns>
        public static bool operator !=(XorShift32 first, XorShift32 second)
        {
            return first.State != second.State;
        }

        #endregion
    }
}
