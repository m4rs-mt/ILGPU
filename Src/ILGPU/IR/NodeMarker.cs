// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: NodeMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents a node marker.
    /// </summary>
    public readonly struct NodeMarker : IEquatable<NodeMarker>
    {
        #region Instance

        /// <summary>
        /// Constructs a new node marker.
        /// </summary>
        /// <param name="marker">The raw marker.</param>
        internal NodeMarker(long marker)
        {
            Marker = marker;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the encapsulated value.
        /// </summary>
        public long Marker { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given marker is equal to this marker.
        /// </summary>
        /// <param name="other">The other marker.</param>
        /// <returns>True, if the given marker is equal to this marker.</returns>
        public bool Equals(NodeMarker other) => this == other;

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to this marker.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to this marker.</returns>
        public override bool Equals(object obj) =>
            obj is NodeMarker nodeMarker && nodeMarker == this;

        /// <summary>
        /// Returns the hash code of this marker.
        /// </summary>
        /// <returns>The hash code of this marker.</returns>
        public override int GetHashCode() => Marker.GetHashCode();

        /// <summary>
        /// Returns the string representation of this marker.
        /// </summary>
        /// <returns>The string representation of this marker.</returns>
        public override string ToString() => Marker.ToString();

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the first and the second marker are the same.
        /// </summary>
        /// <param name="first">The first marker.</param>
        /// <param name="second">The second marker.</param>
        /// <returns>True, if the first and the second marker are the same.</returns>
        public static bool operator ==(NodeMarker first, NodeMarker second) =>
            first.Marker == second.Marker;

        /// <summary>
        /// Returns true if the first and the second marker are not the same.
        /// </summary>
        /// <param name="first">The first marker.</param>
        /// <param name="second">The second marker.</param>
        /// <returns>
        /// True, if the first and the second marker are not the same.
        /// </returns>
        public static bool operator !=(NodeMarker first, NodeMarker second) =>
            !(first == second);

        #endregion
    }
}
