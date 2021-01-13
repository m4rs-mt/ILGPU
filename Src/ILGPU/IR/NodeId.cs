// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: NodeId.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents a node id.
    /// </summary>
    public readonly struct NodeId : IEquatable<NodeId>, IComparable<NodeId>
    {
        #region Static

        /// <summary>
        /// Creates a new unique node id.
        /// </summary>
        /// <returns>A new unique node id.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeId CreateNew() => new NodeId(InstanceId.CreateNew());

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new node id.
        /// </summary>
        /// <param name="id">The raw id.</param>
        private NodeId(InstanceId id)
        {
            Id = id;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the encapsulated id.
        /// </summary>
        private InstanceId Id { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given id is equal to this id.
        /// </summary>
        /// <param name="other">The other id.</param>
        /// <returns>True, if the given id is equal to this id.</returns>
        public bool Equals(NodeId other) => this == other;

        #endregion

        #region IComparable

        /// <summary>
        /// Compares this id to the given one.
        /// </summary>
        /// <param name="other">The object to compare to.</param>
        /// <returns>The comparison result.</returns>
        public int CompareTo(NodeId other) => Id.Value.CompareTo(other.Id.Value);

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to this id.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to this id.</returns>
        public override bool Equals(object obj) =>
            obj is NodeId nodeId && nodeId == this;

        /// <summary>
        /// Returns the hash code of this id.
        /// </summary>
        /// <returns>The hash code of this id.</returns>
        public override int GetHashCode() => Id.GetHashCode();

        /// <summary>
        /// Returns the string representation of this id.
        /// </summary>
        /// <returns>The string representation of this id.</returns>
        public override string ToString() => Id.ToString();

        #endregion

        #region Operators

        /// <summary>
        /// Converts the given node id into its underlying long value.
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        public static implicit operator long(NodeId nodeId) => nodeId.Id;

        /// <summary>
        /// Returns true if the first and the second id are the same.
        /// </summary>
        /// <param name="first">The first id.</param>
        /// <param name="second">The second id.</param>
        /// <returns>True, if the first and the second id are the same.</returns>
        public static bool operator ==(NodeId first, NodeId second) =>
            first.Id == second.Id;

        /// <summary>
        /// Returns true if the first and the second id are not the same.
        /// </summary>
        /// <param name="first">The first id.</param>
        /// <param name="second">The second id.</param>
        /// <returns>True, if the first and the second id are not the same.</returns>
        public static bool operator !=(NodeId first, NodeId second) =>
            first.Id != second.Id;

        /// <summary>
        /// Returns true if the first id is smaller than the second one.
        /// </summary>
        /// <param name="first">The first id.</param>
        /// <param name="second">The second id.</param>
        /// <returns>True, if the first id is smaller than the second one.</returns>
        public static bool operator <(NodeId first, NodeId second) =>
            first.Id < second.Id;

        /// <summary>
        /// Returns true if the first id is smaller than or equal to the second one.
        /// </summary>
        /// <param name="first">The first id.</param>
        /// <param name="second">The second id.</param>
        /// <returns>
        /// True, if the first id is smaller than or equal to the second one.
        /// </returns>
        public static bool operator <=(NodeId first, NodeId second) =>
            first.Id <= second.Id;

        /// <summary>
        /// Returns true if the first id is greater than the second one.
        /// </summary>
        /// <param name="first">The first id.</param>
        /// <param name="second">The second id.</param>
        /// <returns>True, if the first id is greater than the second one.</returns>
        public static bool operator >(NodeId first, NodeId second) =>
            first.Id > second.Id;

        /// <summary>
        /// Returns true if the first id is greater than or equal to the second one.
        /// </summary>
        /// <param name="first">The first id.</param>
        /// <param name="second">The second id.</param>
        /// <returns>
        /// True, if the first id is greater than or equal to the second one.
        /// </returns>
        public static bool operator >=(NodeId first, NodeId second) =>
            first.Id >= second.Id;

        #endregion
    }
}
