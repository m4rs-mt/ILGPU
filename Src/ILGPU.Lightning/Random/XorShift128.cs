// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                   Copyright (c) 2017 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: XorShift128.cs
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
    public struct XorShift128 : IEquatable<XorShift128>
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1025:ReplaceRepetitiveArgumentsWithParamsArray", Justification = "Performance reasons")]
        public XorShift128(uint state0, uint state1, uint state2, uint state3)
        {
            Debug.Assert(state0 != 0 || state1 != 0 ||
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
        public uint Next()
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
        /// Generates a random int in [minValue..maxValue[.
        /// </summary>
        /// <param name="minValue">The minimum value (inclusive)</param>
        /// <param name="maxValue">The maximum values (exclusive)</param>
        /// <returns>A random int in [minValue..maxValue[.</returns>
        public int Next(int minValue, int maxValue)
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
        public bool Equals(XorShift128 other)
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
            return (int)(State0 ^ State1 ^ State2 ^ State3);
        }

        /// <summary>
        /// Returns true iff the given object is equal to the current rng.
        /// </summary>
        /// <param name="obj">The other rng to test.</param>
        /// <returns>True, iff the given object is equal to the current rng.</returns>
        public override bool Equals(object obj)
        {
            if (obj is XorShift128 shift)
                return Equals(shift);
            return false;
        }

        /// <summary>
        /// Returns the string representation of this rng.
        /// </summary>
        /// <returns>The string representation of this rng.</returns>
        public override string ToString()
        {
            return $"[{State0}, {State1}, {State2}, {State3}]";
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and second rng are the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, iff the first and second rng are the same.</returns>
        public static bool operator ==(XorShift128 first, XorShift128 second)
        {
            return first.State0 == second.State0 &&
                first.State1 == second.State1 &&
                first.State2 == second.State2 &&
                first.State3 == second.State3;
        }

        /// <summary>
        /// Returns true iff the first and second rng are not the same.
        /// </summary>
        /// <param name="first">The first rng.</param>
        /// <param name="second">The second rng.</param>
        /// <returns>True, iff the first and second rng are not the same.</returns>
        public static bool operator !=(XorShift128 first, XorShift128 second)
        {
            return first.State0 != second.State0 ||
                first.State1 != second.State1 ||
                first.State2 != second.State2 ||
                first.State3 != second.State3;
        }

        #endregion
    }
}
