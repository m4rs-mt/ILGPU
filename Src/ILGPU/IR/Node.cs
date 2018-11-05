// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Node.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

#if VERIFICATION
using System;
#endif
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
        /// Returns the unique node id.
        /// </summary>
        public NodeId Id { get; internal set; }

#if VERIFICATION

        /// <summary>
        /// Returns true iff the current node is sealed.
        /// </summary>
        public bool IsSealed { get; private set; }

#endif

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
        public bool Mark(NodeMarker newMarker)
        {
            return Interlocked.Exchange(ref markerValue, newMarker.Marker) != newMarker.Marker;
        }

        /// <summary>
        /// Returns true iff the reference marker is equal to the
        /// current marker value.
        /// </summary>
        /// <param name="referenceMarker">The reference marker.</param>
        /// <returns>
        /// True, iff the current marker is equal to the current
        /// marker value.
        /// </returns>
        public bool IsMarked(NodeMarker referenceMarker)
        {
            return Interlocked.Read(ref markerValue) == referenceMarker.Marker;
        }

#if VERIFICATION
        /// <summary>
        /// Seals this node.
        /// </summary>
        protected void SealNode()
        {
            if (IsSealed)
                throw new InvalidOperationException("Cannot modify a sealed node");
            IsSealed = true;
        }
#endif

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
