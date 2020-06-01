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

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ILGPU.IR
{
    /// <summary>
    /// The base interface of all nodes.
    /// </summary>
    public interface INode : ILocation, IDumpable
    {
        /// <summary>
        /// Returns the unique node id.
        /// </summary>
        NodeId Id { get; }

        /// <summary>
        /// Returns the associated location.
        /// </summary>
        Location Location { get; }
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
        /// <param name="location">The current location.</param>
        protected Node(Location location)
        {
            // Ensure that we have a valid location at this location
            Locations.AssertNotNull(location, location);

            Location = location;
            Id = NodeId.CreateNew();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated location.
        /// </summary>
        public Location Location { get; private set; }

        /// <summary>
        /// Returns the unique node id.
        /// </summary>
        public NodeId Id { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Infers the location (if required) of the current node.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="elements">Elements we can infer the location from.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void InferLocation<T>(ReadOnlySpan<T> elements)
            where T : INode
        {
            if (Location.IsKnown)
                return;
            foreach (var element in elements)
                Location = Location.Merge(Location, element.Location);
        }

        /// <summary>
        /// Formats an error message to include specific exception information.
        /// </summary>
        /// <param name="message">The source error message.</param>
        /// <returns>The formatted error message.</returns>
        public virtual string FormatErrorMessage(string message) =>
            Location.FormatErrorMessage(message);

        /// <summary>
        /// Dumps this method to the given text writer.
        /// </summary>
        /// <param name="textWriter">The text writer.</param>
        public virtual void Dump(TextWriter textWriter)
        {
            if (textWriter == null)
                throw new ArgumentNullException(nameof(textWriter));
            textWriter.WriteLine(ToString());
        }

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
