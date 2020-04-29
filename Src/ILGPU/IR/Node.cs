// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Node.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.DebugInformation;
using System;

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

        /// <summary>
        /// Constructs a new node that is marked as replaceable.
        /// </summary>
        protected Node()
        {
            Id = NodeId.CreateNew();
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
        public NodeId Id { get; }

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
        public string ToReferenceString() => ToPrefixString() + "_" + Id.ToString();

        /// <summary>
        /// Returns the string representation of this node.
        /// </summary>
        /// <returns>The string representation of this node.</returns>
        public override string ToString() => ToReferenceString();

        #endregion
    }
}
