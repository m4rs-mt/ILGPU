﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueReference.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.IO;

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
        #region Nested Types

        /// <summary>
        /// A value reference formatter.
        /// </summary>
        public readonly struct ToReferenceFormatter :
            InlineList.IFormatter<Value>,
            InlineList.IFormatter<ValueReference>
        {
            /// <summary>
            /// Formats a value by returning its reference string.
            /// </summary>
            readonly string InlineList.IFormatter<Value>.Format(Value item) =>
                item.ToReferenceString();

            /// <summary>
            /// Formats a value reference by returning its reference string.
            /// </summary>
            readonly string InlineList.IFormatter<ValueReference>.Format(
                ValueReference item) =>
                item.ToString();
        }

        #endregion

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
        /// Returns the current value kind.
        /// </summary>
        public ValueKind ValueKind => Resolve().ValueKind;

        /// <summary>
        /// Returns the node that is directly stored in the reference structure
        /// without using any replacement information.
        /// </summary>
        public Value DirectTarget { get; private set; }

        /// <summary>
        /// Returns true if the reference points to a valid node.
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
        /// Returns the associated location.
        /// </summary>
        public Location Location => Resolve().Location;

        /// <summary>
        /// Returns all child nodes of the latest node.
        /// </summary>
        public ReadOnlySpan<ValueReference> Nodes => Resolve().Nodes;

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
        /// Returns an enumerator to enumerate all child nodes.
        /// </summary>
        /// <returns>An enumerator to enumerate all child nodes.</returns>
        public ReadOnlySpan<ValueReference>.Enumerator GetEnumerator() =>
            Resolve().GetEnumerator();

        /// <summary>
        /// Accepts a node visitor.
        /// </summary>
        /// <typeparam name="T">The type of the visitor.</typeparam>
        /// <param name="visitor">The visitor.</param>
        /// <returns>The resulting value.</returns>
        public void Accept<T>(T visitor)
            where T : IValueVisitor =>
            Resolve().Accept(visitor);

        /// <summary>
        /// Replaces this node with the given node.
        /// </summary>
        /// <param name="other">The other node.</param>
        public void Replace(Value other) => Resolve().Replace(other);

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

        /// <summary>
        /// Dumps this node to the given text writer.
        /// </summary>
        /// <param name="textWriter">The text writer.</param>
        public void Dump(TextWriter textWriter) => Resolve()?.Dump(textWriter);

        #endregion

        #region ILocation

        /// <summary>
        /// Formats an error message to include specific location information.
        /// </summary>
        string ILocation.FormatErrorMessage(string message) =>
            Location.FormatErrorMessage(message);

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given node reference points to the
        /// same node.
        /// </summary>
        /// <param name="other">The other reference.</param>
        /// <returns>True, if the given reference points to the same node.</returns>
        public bool Equals(ValueReference other) => this == other;

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is a node reference that
        /// points to the same node.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object points to the same node.</returns>
        public override bool Equals(object obj) =>
            obj is ValueReference other && Equals(other);

        /// <summary>
        /// Returns the hash code of the directly associated node.
        /// </summary>
        /// <returns>The hash code of the directly associated node</returns>
        public override int GetHashCode() => DirectTarget.GetHashCode();

        /// <summary>
        /// Returns the string representation of this reference.
        /// </summary>
        /// <returns>The string representation of this reference.</returns>
        public override string ToString() =>
            !IsValid ? "<null>" : Resolve().ToReferenceString();

        #endregion

        #region Operators

        /// <summary>
        /// Converts the given node implicitly to a node reference.
        /// </summary>
        /// <param name="node">The node to convert.</param>
        public static implicit operator ValueReference(Value node) =>
            new ValueReference(node);

        /// <summary>
        /// Converts the given reference to the latest node information.
        /// </summary>
        /// <param name="reference">The reference to convert.</param>
        public static implicit operator Value(ValueReference reference) =>
            reference.Resolve();

        /// <summary>
        /// Returns true if the both node references point to the
        /// same node.
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The first reference.</param>
        /// <returns>True, if both node references point to the same node.</returns>
        public static bool operator ==(ValueReference first, ValueReference second) =>
            first.Resolve() == second.Resolve();

        /// <summary>
        /// Returns true if the both node references point to different nodes.
        /// </summary>
        /// <param name="first">The first reference.</param>
        /// <param name="second">The first reference.</param>
        /// <returns>True, if both node references point to different nodes.</returns>
        public static bool operator !=(ValueReference first, ValueReference second) =>
            first.Resolve() != second.Resolve();

        #endregion
    }
}
