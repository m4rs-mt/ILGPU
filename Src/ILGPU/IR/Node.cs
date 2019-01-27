// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Node.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.DebugInformation;
using System;
using System.Diagnostics;
using System.Threading;

namespace ILGPU.IR
{
    /// <summary>
    /// The base interface of all nodes.
    /// </summary>
    public interface INode
    {
        /// <summary>
        /// Returns the unique node id.
        /// </summary>
        NodeId Id { get; }

        /// <summary>
        /// Returns the associated sequence point.
        /// </summary>
        SequencePoint SequencePoint { get; }

        /// <summary>
        /// Marks the current node with the new marker value.
        /// </summary>
        /// <param name="newMarker">The new value to apply.</param>
        /// <returns>
        /// True, iff the old marker was not equal to the new marker
        /// (the node was not marked with the new marker value).
        /// </returns>
        bool Mark(NodeMarker newMarker);

        /// <summary>
        /// Returns true iff the reference marker is less or equal to the
        /// current marker value.
        /// </summary>
        /// <param name="referenceMarker">The reference marker.</param>
        /// <returns>
        /// True, iff the reference marker is less or equal to
        /// the current marker value.
        /// </returns>
        bool IsMarked(NodeMarker referenceMarker);
    }

    /// <summary>
    /// Represents a basic intermediate-representation node.
    /// It is the base class for all nodes in the scope of this IR.
    /// </summary>
    public abstract class Node : INode
    {
        #region Static

        /// <summary>
        /// Compares two nodes according to their id.
        /// </summary>
        internal static readonly Comparison<Node> Comparison =
            (first, second) => first.Id.CompareTo(second.Id);

        #endregion

        #region Instance

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long markerValue;

        /// <summary>
        /// Constructs a new node that is marked as replacable.
        /// </summary>
        protected Node()
        {
            Id = NodeId.Empty;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated sequence point.
        /// </summary>
        public SequencePoint SequencePoint { get; internal set; }

        /// <summary>
        /// Returns the unique node id.
        /// </summary>
        public NodeId Id { get; internal set; }

        #endregion

        #region Methods

        /// <summary>
        /// Marks the current node with the new marker value.
        /// </summary>
        /// <param name="newMarker">The new value to apply.</param>
        /// <returns>
        /// True, iff the old marker was not equal to the new marker
        /// (the node was not marked with the new marker value).
        /// </returns>
        public bool Mark(NodeMarker newMarker) =>
            Interlocked.Exchange(ref markerValue, newMarker.Marker) != newMarker.Marker;

        /// <summary>
        /// Returns true iff the reference marker is equal to the
        /// current marker value.
        /// </summary>
        /// <param name="referenceMarker">The reference marker.</param>
        /// <returns>
        /// True, iff the current marker is equal to the current
        /// marker value.
        /// </returns>
        public bool IsMarked(NodeMarker referenceMarker) =>
            Interlocked.Read(ref markerValue) == referenceMarker.Marker;

        #endregion

        #region Object

        /// <summary>
        /// Returns the prefix string (operation name) of this node.
        /// </summary>
        /// <returns>The prefix string.</returns>
        protected abstract string ToPrefixString();

        /// <summary>
        /// Returns the string representation of this node as reference.
        /// </summary>
        /// <returns>The string representation of this node as reference.</returns>
        public string ToReferenceString()
        {
            return ToPrefixString() + "_" + Id.ToString();
        }

        /// <summary>
        /// Returns the string represetation of this node.
        /// </summary>
        /// <returns>The string representation of this node.</returns>
        public override string ToString() => ToReferenceString();

        #endregion
    }
}
