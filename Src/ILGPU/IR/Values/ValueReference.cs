// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: ValueReference.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a reference to a node that can be resolved
    /// automatically to the latest node information by following
    /// the replacement relation on nodes.
    /// </summary>
    /// <remarks>
    /// Use the <see cref="DirectTarget"/> property to resolve the
    /// directly associated node.
    /// </remarks>
    public struct ValueReference : IValue, IEquatable<ValueReference>
    {
        #region Instance

        /// <summary>
        /// Constructs a new node reference.
        /// </summary>
        /// <param name="node"></param>
        public ValueReference(Value node)
        {
            Debug.Assert(node != null, "Invalid node");
            DirectTarget = node;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the node that is directly stored in the reference struct
        /// without using any replacement information.
        /// </summary>
        public Value DirectTarget { get; private set; }

        /// <summary>
        /// Returns true iff the reference points to a valid node.
        /// </summary>
        public bool IsValid => DirectTarget != null;

        /// <summary>
        /// Returns true if the direct target has been replaced.
        /// </summary>
        public bool IsReplaced => IsValid && DirectTarget.IsReplaced;

        /// <summary>
        /// Returns the parent basic block.
        /// </summary>
        public BasicBlock BasicBlock => Resolve().BasicBlock;

        /// <summary>
        /// Returns the unique node id of the latest node.
        /// </summary>
        public NodeId Id => Resolve().Id;

        /// <summary>
        /// Returns all child nodes of the latest node.
        /// </summary>
        public ImmutableArray<ValueReference> Nodes => Resolve().Nodes;

        /// <summary>
        /// Returns all uses of the latest node.
        /// </summary>
        public UseCollection Uses => Resolve().Uses;

        /// <summary>
        /// Returns the associated type of the latest node.
        /// </summary>
        public TypeNode Type => Resolve().Type;

        /// <summary>
        /// Returns the associated basic value type.
        /// </summary>
        public BasicValueType BasicValueType => Resolve().BasicValueType;

        #endregion

        #region Methods

        /// <summary>
        /// Refreshes the current reference and returns the new one.
        /// </summary>
        /// <returns>The refreshed reference.</returns>
        public ValueReference Refresh() => new ValueReference(Resolve());

        /// <summary>
        /// Marks the current node with the new marker value.
        /// </summary>
        /// <param name="newMarker">The new value to apply.</param>
        /// <returns>
        /// True, iff the old marker was not equal to the new marker
        /// (the node was not marked with the new marker value).
        /// </returns>
        public bool Mark(NodeMarker newMarker) => Resolve().Mark(newMarker);

        /// <summary>
        /// Returns true iff the reference marker is less or equal to the
        /// current marker value.
        /// </summary>
        /// <param name="referenceMarker">The reference marker.</param>
        /// <returns>
        /// True, iff the reference marker is less or equal to
        /// the current marker value.
        /// </returns>
        public bool IsMarked(NodeMarker referenceMarker) => Resolve().IsMarked(referenceMarker);

        /// <summary>
        /// Returns an enumerator to enumerate all child nodes.
        /// </summary>
        /// <returns>An enumerator to enumerate all child nodes.</returns>
        public Value.Enumerator GetEnumerator() => Resolve().GetEnumerator();

        /// <summary>
        /// Accepts a node visitor.
        /// </summary>
        /// <typeparam name="T">The type of the visitor.</typeparam>
        /// <param name="visitor">The visitor.</param>
        /// <returns>The resulting value.</returns>
        public void Accept<T>(T visitor)
            where T : IValueVisitor
        {
            Resolve().Accept(visitor);
        }

        /// <summary>
        /// Replaces this node with the given node.
        /// </summary>
        /// <param name="other">The other node.</param>
        public void Replace(Value other)
        {
            Resolve().Replace(other);
        }

        /// <summary>
        /// Resolves the actual node with respect to
        /// replacement information.
        /// </summary>
        /// <returns>The actual node.</returns>
        public Value Resolve()
        {
            Debug.Assert(IsValid, "Get operation on invalid node");
            return DirectTarget = DirectTarget.Resolve();
        }

        /// <summary>
        /// Resolves the actual node with respect to replacement information.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The actual node.</returns>
        public T ResolveAs<T>() where T : Value => Resolve() as T;

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given node reference points to the
        /// same node.
        /// </summary>
        /// <param name="other">The other reference.</param>
        /// <returns>True, iff the given reference points to the same node.</returns>
        public bool Equals(ValueReference other) => this == other;

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is a node reference that
        /// points to the same node.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object points to the same node.</returns>
        public override bool Equals(object obj)
        {
            if (obj is ValueReference other)
                return Equals(other);
            return false;
        }

        /// <summary>
        /// Returns the hash code of the directly associated node.
        /// </summary>
        /// <returns>The hash code of the directly associated node</returns>
        public override int GetHashCode()
        {
            return DirectTarget.GetHashCode();
        }

        /// <summary>
        /// Returns the string represention of this reference.
        /// </summary>
        /// <returns>The string representation of this reference.</returns>
        public override string ToString()
        {
            if (!IsValid)
                return "<null>";
            return Resolve().ToReferenceString();
        }

        #endregion

        #region Operators

        /// <summary>
        /// Converts the given node implicitly to a node reference.
        /// </summary>
        /// <param name="node">The node to convert.</param>
        public static implicit operator ValueReference(Value node)
        {
            return new ValueReference(node);
        }

        /// <summary>
        /// Converts the given reference to the latest node information.
        /// </summary>
        /// <param name="reference">The reference to convert.</param>
        public static implicit operator Value(ValueReference reference)
        {
            return reference.Resolve();
        }

        /// <summary>
        /// Returns true iff the both node references point to the
        /// same node.
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The first reference.</param>
        /// <returns>True, iff both node references point to the same node.</returns>
        public static bool operator ==(ValueReference first, ValueReference second)
        {
            return first.Resolve() == second.Resolve();
        }

        /// <summary>
        /// Returns true iff the both node references point to different nodes.
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The first reference.</param>
        /// <returns>True, iff both node references point to different nodes.</returns>
        public static bool operator !=(ValueReference first, ValueReference second)
        {
            return first.Resolve() != second.Resolve();
        }

        #endregion
    }
}
