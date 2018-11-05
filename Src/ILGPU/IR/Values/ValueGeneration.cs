// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: ValueGeneration.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a value node generation.
    /// </summary>
    public readonly struct ValueGeneration : IEquatable<ValueGeneration>
    {
        #region Instance

        /// <summary>
        /// Constructs a new node generation.
        /// </summary>
        /// <param name="generation">The raw generation.</param>
        internal ValueGeneration(long generation)
        {
            Value = generation;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the encapsulated value.
        /// </summary>
        public long Value { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given generation is equal to this generation.
        /// </summary>
        /// <param name="other">The other generation.</param>
        /// <returns>True, iff the given generation is equal to this generation.</returns>
        public bool Equals(ValueGeneration other) => this == other;

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to this generation.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to this generation.</returns>
        public override bool Equals(object obj)
        {
            if (obj is ValueGeneration nodeId)
                return nodeId == this;
            return false;
        }

        /// <summary>
        /// Returns the hash code of this generation.
        /// </summary>
        /// <returns>The hash code of this generation.</returns>
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// Returns the string representation of this generation.
        /// </summary>
        /// <returns>The string representation of this generation.</returns>
        public override string ToString() => Value.ToString();

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and the second generation are the same.
        /// </summary>
        /// <param name="first">The first generation.</param>
        /// <param name="second">The second generation.</param>
        /// <returns>True, iff the first and the second generation are the same.</returns>
        public static bool operator ==(ValueGeneration first, ValueGeneration second)
        {
            return first.Value == second.Value;
        }

        /// <summary>
        /// Returns true iff the first and the second generation are not the same.
        /// </summary>
        /// <param name="first">The first generation.</param>
        /// <param name="second">The second generation.</param>
        /// <returns>True, iff the first and the second generation are not the same.</returns>
        public static bool operator !=(ValueGeneration first, ValueGeneration second)
        {
            return first.Value != second.Value;
        }

        #endregion
    }
}
