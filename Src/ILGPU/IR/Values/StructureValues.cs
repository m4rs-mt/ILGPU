// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: StructureValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Serialization;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// An index into to a scalar structure field.
    /// </summary>
    public readonly struct FieldAccess : IEquatable<FieldAccess>
    {
        #region Instance

        /// <summary>
        /// Constructs a new field access.
        /// </summary>
        /// <param name="fieldIndex">The field access.</param>
        public FieldAccess(int fieldIndex)
        {
            Debug.Assert(fieldIndex >= 0, "Invalid field index");
            Index = fieldIndex;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the field index.
        /// </summary>
        public int Index { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Adds the given field offset to the current index.
        /// </summary>
        /// <param name="offset">The offset to add.</param>
        /// <returns>The adapted field access.</returns>
        public FieldAccess Add(int offset)
        {
            Debug.Assert(offset >= 0, "Invalid offset");
            return new FieldAccess(Index + offset);
        }

        /// <summary>
        /// Subtracts the given field offset from the current index.
        /// </summary>
        /// <param name="offset">The offset to subtract.</param>
        /// <returns>The adapted field access.</returns>
        public FieldAccess Subtract(int offset)
        {
            Debug.Assert(offset >= 0, "Invalid offset");
            return new FieldAccess(Index - offset);
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given field access is equal to the current one.
        /// </summary>
        /// <param name="other">The other field reference.</param>
        /// <returns>
        /// True, if the given field access is equal to the current one.
        /// </returns>
        public bool Equals(FieldAccess other) => Index == other.Index;

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current one.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>
        /// True, if the given field access is equal to the current one.
        /// </returns>
        public override bool Equals(object? obj) =>
            obj is FieldAccess other && Equals(other);

        /// <summary>
        /// Returns the hash code of this field access.
        /// </summary>
        /// <returns>The hash code of this field access.</returns>
        public override int GetHashCode() => Index.GetHashCode();

        /// <summary>
        /// Returns the string representation of this field access.
        /// </summary>
        /// <returns>The string representation of this field access.</returns>
        public override string ToString() => Index.ToString();

        #endregion

        #region Operators

        /// <summary>
        /// Converts a field index into a field access instance.
        /// </summary>
        /// <param name="fieldIndex">The field index to convert.</param>
        public static implicit operator FieldAccess(int fieldIndex) =>
            new FieldAccess(fieldIndex);

        /// <summary>
        /// Converts a field index access into its underlying field index.
        /// </summary>
        /// <param name="access">The field access to convert.</param>
        public static explicit operator int(FieldAccess access) =>
            access.Index;

        /// <summary>
        /// Returns true if the first and second field access are the same.
        /// </summary>
        /// <param name="first">The first field access.</param>
        /// <param name="second">The second field access.</param>
        /// <returns>
        /// True, if the first and second field access are the same.
        /// </returns>
        public static bool operator ==(FieldAccess first, FieldAccess second) =>
            first.Equals(second);

        /// <summary>
        /// Returns true if the first and second field access are not the same.
        /// </summary>
        /// <param name="first">The first field access.</param>
        /// <param name="second">The second field access.</param>
        /// <returns>
        /// True, if the first and second field access are not the same.
        /// </returns>
        public static bool operator !=(FieldAccess first, FieldAccess second) =>
            !first.Equals(second);

        #endregion
    }

    /// <summary>
    /// An index into to a scalar structure field that can span multiple fields.
    /// </summary>
    public readonly struct FieldSpan : IEquatable<FieldSpan>
    {
        #region Instance

        /// <summary>
        /// Constructs a new field span.
        /// </summary>
        /// <param name="fieldIndex">The field access.</param>
        public FieldSpan(FieldAccess fieldIndex)
            : this(fieldIndex, 1)
        { }

        /// <summary>
        /// Constructs a new field reference.
        /// </summary>
        /// <param name="fieldIndex">The field access.</param>
        /// <param name="span">The number of fields to span.</param>
        public FieldSpan(FieldAccess fieldIndex, int span)
        {
            Access = fieldIndex;
            Span = Math.Max(span, 1);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the field index.
        /// </summary>
        public FieldAccess Access { get; }

        /// <summary>
        /// Returns the field index.
        /// </summary>
        public int Index => Access.Index;

        /// <summary>
        /// The number of fields to span.
        /// </summary>
        public int Span { get; }

        /// <summary>
        /// Returns true if this instance spans over multiple fields.
        /// </summary>
        public bool HasSpan => Span > 1;

        #endregion

        #region Methods

        /// <summary>
        /// Returns the last inclusive field access.
        /// </summary>
        /// <returns>The last inclusive field access.</returns>
        public FieldAccess GetLastAccess() =>
            Access.Add(IntrinsicMath.Max(Span - 1, 0));

        /// <summary>
        /// Returns true if the given field span is contained in this span.
        /// </summary>
        /// <param name="fieldSpan">The field span.</param>
        /// <returns>True, if the given field span is contained in this span.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(FieldSpan fieldSpan)
        {
            int sourceIndex = fieldSpan.Index;
            return Index <= sourceIndex &&
                sourceIndex + fieldSpan.Span <= Index + Span;
        }

        /// <summary>
        /// Checks whether the current field span is distinct from the given one.
        /// </summary>
        /// <param name="fieldSpan">The other field span.</param>
        /// <returns>
        /// True, if the given field span is distinct from the given one.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Distinct(FieldSpan fieldSpan)
        {
            int lastIndex = GetLastAccess().Index;
            return lastIndex < fieldSpan.Index ||
                Index > fieldSpan.GetLastAccess().Index;
        }

        /// <summary>
        /// Checks whether the current field span overlaps with the given one.
        /// </summary>
        /// <param name="fieldSpan">The other field span.</param>
        /// <returns>True, if the given field span overlaps with the given one.</returns>
        public bool Overlaps(FieldSpan fieldSpan) => !Distinct(fieldSpan);

        /// <summary>
        /// Narrows the current span by accessing a nested span.
        /// </summary>
        /// <param name="fieldSpan">The nested span.</param>
        /// <returns>A new nested span that has an adjusted field index.</returns>
        public FieldSpan Narrow(FieldSpan fieldSpan) =>
            new FieldSpan(
                Index + fieldSpan.Index,
                fieldSpan.Span);

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given field access is equal to the current one.
        /// </summary>
        /// <param name="other">The other field reference.</param>
        /// <returns>
        /// True, if the given field access is equal to the current one.
        /// </returns>
        public bool Equals(FieldSpan other) =>
            Access.Equals(other.Access) && Span == other.Span;

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current one.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>
        /// True, if the given field access is equal to the current one.
        /// </returns>
        public override bool Equals(object? obj) =>
            obj is FieldSpan other && Equals(other);

        /// <summary>
        /// Returns the hash code of this field access.
        /// </summary>
        /// <returns>The hash code of this field access.</returns>
        public override int GetHashCode() => Access.GetHashCode() ^ Span;

        /// <summary>
        /// Returns the string representation of this field access.
        /// </summary>
        /// <returns>The string representation of this field access.</returns>
        public override string ToString()
        {
            var baseString = Access.ToString();
            if (HasSpan)
                baseString += " [Span: " + Span + "]";
            return baseString;
        }

        #endregion

        #region Operators

        /// <summary>
        /// Converts a field access into a field span.
        /// </summary>
        /// <param name="access">The access to convert.</param>
        public static implicit operator FieldSpan(FieldAccess access) =>
            new FieldSpan(access);

        /// <summary>
        /// Returns true if the first and second field access are the same.
        /// </summary>
        /// <param name="first">The first field access.</param>
        /// <param name="second">The second field access.</param>
        /// <returns>
        /// True, if the first and second field access are the same.
        /// </returns>
        public static bool operator ==(FieldSpan first, FieldSpan second) =>
            first.Equals(second);

        /// <summary>
        /// Returns true if the first and second field access are not the same.
        /// </summary>
        /// <param name="first">The first field access.</param>
        /// <param name="second">The second field access.</param>
        /// <returns>
        /// True, if the first and second field access are not the same.
        /// </returns>
        public static bool operator !=(FieldSpan first, FieldSpan second) =>
            !first.Equals(second);

        #endregion
    }

    /// <summary>
    /// Represents a chain of field indices that is used to point to a particular
    /// structure field.
    /// </summary>
    public readonly struct FieldAccessChain : IEquatable<FieldAccessChain>
    {
        #region Static

        /// <summary>
        /// An empty access chain.
        /// </summary>
        public static readonly FieldAccessChain Empty =
            new FieldAccessChain(ImmutableArray<FieldAccess>.Empty);

        #endregion

        #region Instance

        /// <summary>
        /// The cached hash code.
        /// </summary>
        private readonly int hashCode;

        /// <summary>
        /// Constructs a new access chain using the given index.
        /// </summary>
        /// <param name="index">The index of this reference.</param>
        public FieldAccessChain(FieldAccess index)
            : this(ImmutableArray.Create(index))
        { }

        /// <summary>
        /// Constructs a new access chain using the given indices.
        /// </summary>
        /// <param name="accessChain">The indices of this reference.</param>
        public FieldAccessChain(ImmutableArray<FieldAccess> accessChain)
        {
            AccessChain = accessChain;

            hashCode = accessChain.Length;
            foreach (var chainEntry in accessChain)
                hashCode ^= chainEntry.GetHashCode();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the access chain element for the given index.
        /// </summary>
        /// <param name="index">The access chain index.</param>
        /// <returns>The resolved chain element.</returns>
        public FieldAccess this[int index] => AccessChain[index];

        /// <summary>
        /// Returns the number of chain elements.
        /// </summary>
        public int Length => AccessChain.Length;

        /// <summary>
        /// Returns the list of index elements.
        /// </summary>
        public ImmutableArray<FieldAccess> AccessChain { get; }

        /// <summary>
        /// Returns true if this chain is empty.
        /// </summary>
        public bool IsEmpty => AccessChain.IsDefaultOrEmpty;

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if this access chain is a subchain of the given one.
        /// </summary>
        /// <param name="other">The other subchain.</param>
        /// <returns>
        /// True if this access chain is a subchain of the given one.
        /// </returns>
        public bool IsSubChainOf(FieldAccessChain other)
        {
            if (Length >= other.Length)
                return false;
            for (int i = 0; i < Length; ++i)
            {
                if (this[i] != other[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Returns an enumerate to iterate over all chain elements.
        /// </summary>
        /// <returns>An enumerate to iterate over all chain elements.</returns>
        public ImmutableArray<FieldAccess>.Enumerator GetEnumerator() =>
            AccessChain.GetEnumerator();

        /// <summary>
        /// Realizes an additional access operation to the given field indices.
        /// </summary>
        /// <param name="accessChain">The next access chain.</param>
        /// <returns>The extended field reference.</returns>
        public FieldAccessChain Append(FieldAccessChain accessChain) =>
            new FieldAccessChain(AccessChain.AddRange(accessChain.AccessChain));

        /// <summary>
        /// Realizes an additional access operation to the given field index.
        /// </summary>
        /// <param name="fieldAccess">The next field access.</param>
        /// <returns>The extended field reference.</returns>
        public FieldAccessChain Append(FieldAccess fieldAccess) =>
            new FieldAccessChain(AccessChain.Add(fieldAccess));

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given field ref is equal to the current one.
        /// </summary>
        /// <param name="other">The other field reference.</param>
        /// <returns>
        /// True, if the given field ref is equal to the current one.
        /// </returns>
        public bool Equals(FieldAccessChain other)
        {
            var chain = AccessChain;
            var otherChain = other.AccessChain;
            if (chain.Length != otherChain.Length)
                return false;
            for (int i = 0, e = chain.Length; i < e; ++i)
            {
                if (chain[i] != otherChain[i])
                    return false;
            }
            return true;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current one.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>
        /// True, if the given field ref is equal to the current one.
        /// </returns>
        public override bool Equals(object? obj) =>
            obj is FieldAccessChain other && Equals(other);

        /// <summary>
        /// Returns the hash code of this field reference.
        /// </summary>
        /// <returns>The hash code of this field reference.</returns>
        public override int GetHashCode() => hashCode;

        /// <summary>
        /// Returns the string representation of this field reference.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var result = new StringBuilder();
            foreach (var entry in AccessChain)
            {
                result.Append('[');
                result.Append(entry);
                result.Append(']');
            }
            return result.ToString();
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the first and second field ref are the same.
        /// </summary>
        /// <param name="first">The first field ref.</param>
        /// <param name="second">The second field ref.</param>
        /// <returns>
        /// True, if the first and second field ref are the same.
        /// </returns>
        public static bool operator ==(
            FieldAccessChain first,
            FieldAccessChain second) =>
            first.Equals(second);

        /// <summary>
        /// Returns true if the first and second field ref are not the same.
        /// </summary>
        /// <param name="first">The first field ref.</param>
        /// <param name="second">The second field ref.</param>
        /// <returns>
        /// True, if the first and second field ref are not the same.
        /// </returns>
        public static bool operator !=(
            FieldAccessChain first,
            FieldAccessChain second) =>
            !first.Equals(second);

        #endregion
    }

    /// <summary>
    /// A reference to a scalar structure field.
    /// </summary>
    public readonly struct FieldRef : IEquatable<FieldRef>
    {
        #region Instance

        private readonly FieldSpan? span;

        /// <summary>
        /// Constructs a new direct reference to the given node.
        /// </summary>
        /// <param name="source">The main source.</param>
        public FieldRef(Value source)
        {
            Source = source;
            span = null;
        }

        /// <summary>
        /// Constructs a new direct reference to the given node.
        /// </summary>
        /// <param name="source">The main source.</param>
        /// <param name="fieldSpan">The field span.</param>
        public FieldRef(Value source, FieldSpan fieldSpan)
        {
            Source = source;
            span = fieldSpan;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if this field reference points to a valid field.
        /// </summary>
        public bool IsValid => Source != null;

        /// <summary>
        /// Returns the source node (the main structure value).
        /// </summary>
        public Value Source { get; }

        /// <summary>
        /// Returns the field span.
        /// </summary>
        public FieldSpan FieldSpan => span.GetValueOrDefault();

        /// <summary>
        /// Returns true if this instances references the whole source object.
        /// </summary>
        public bool IsDirect => !span.HasValue;

        #endregion

        #region Methods

        /// <summary>
        /// Accesses the given field span.
        /// </summary>
        /// <param name="fieldSpan">The field span.</param>
        /// <returns>The new field reference.</returns>
        public FieldRef Access(FieldSpan fieldSpan) =>
            IsDirect
            ? new FieldRef(Source, fieldSpan)
            : new FieldRef(Source, FieldSpan.Narrow(fieldSpan));

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given field ref is equal to the current one.
        /// </summary>
        /// <param name="other">The other field reference.</param>
        /// <returns>
        /// True, if the given field ref is equal to the current one.
        /// </returns>
        [SuppressMessage(
            "Style",
            "IDE0046:Convert to conditional expression",
            Justification = "Avoid nested if conditionals")]
        public bool Equals(FieldRef other)
        {
            if (Source != other.Source || IsDirect != other.IsDirect)
                return false;
            return IsDirect || FieldSpan.Equals(other.FieldSpan);
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current one.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>
        /// True, if the given field ref is equal to the current one.
        /// </returns>
        public override bool Equals(object? obj) =>
            obj is FieldRef other && Equals(other);

        /// <summary>
        /// Returns the hash code of this field reference.
        /// </summary>
        /// <returns>The hash code of this field reference.</returns>
        public override int GetHashCode() => Source.GetHashCode() ^ span.GetHashCode();

        /// <summary>
        /// Returns the string representation of this field reference.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var baseString = Source.ToReferenceString();
            return IsDirect
                ? baseString
                : baseString + '[' + FieldSpan.ToString() + ']';
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the first and second field ref are the same.
        /// </summary>
        /// <param name="first">The first field ref.</param>
        /// <param name="second">The second field ref.</param>
        /// <returns>True, if the first and second field ref are the same.</returns>
        public static bool operator ==(FieldRef first, FieldRef second) =>
            first.Equals(second);

        /// <summary>
        /// Returns true if the first and second field ref are not the same.
        /// </summary>
        /// <param name="first">The first field ref.</param>
        /// <param name="second">The second field ref.</param>
        /// <returns>True, if the first and second field ref are not the same.</returns>
        public static bool operator !=(FieldRef first, FieldRef second) =>
            !first.Equals(second);

        #endregion
    }

    /// <summary>
    /// Represents an immutable structure value.
    /// </summary>
    [ValueKind(ValueKind.Structure)]
    public sealed class StructureValue : Value
    {
        #region Nested Types

        /// <summary>
        /// An abstract structure value builder.
        /// </summary>
        public interface IBuilder : IValueBuilder { }

        /// <summary>
        /// An internal instance builder.
        /// </summary>
        internal interface IInternalBuilder : IBuilder
        {
            /// <summary>
            /// Moves the underlying array builder to a target list and outputs an
            /// assembled structure type.
            /// </summary>
            /// <param name="values">The resulting array of value references.</param>
            /// <returns>The resulting structure type.</returns>
            StructureType Seal(ref ValueList values);
        }

        /// <summary>
        /// An instance builder for structure instances.
        /// </summary>
        public struct Builder : IInternalBuilder
        {
            #region Instance

            private ValueList builder;

            /// <summary>
            /// Initializes a new instance builder.
            /// </summary>
            /// <param name="irBuilder">The current IR builder.</param>
            /// <param name="location">The current location.</param>
            /// <param name="parent">The parent type.</param>
            internal Builder(
                IRBuilder irBuilder,
                Location location,
                StructureType parent)
            {
                location.AssertNotNull(parent);
                builder = ValueList.Create(parent.NumFields);
                IRBuilder = irBuilder;
                Location = location;
                Parent = parent;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent builder.
            /// </summary>
            public IRBuilder IRBuilder { get; }

            /// <summary>
            /// Returns the current location.
            /// </summary>
            public Location Location { get; }

            /// <summary>
            /// Returns the corresponding parent type.
            /// </summary>
            public StructureType Parent { get; }

            /// <summary>
            /// The number of field values.
            /// </summary>
            public int Count => builder.Count;

            /// <summary>
            /// Returns the value that corresponds to the given field access.
            /// </summary>
            /// <param name="access">The field access.</param>
            /// <returns>The resolved field type.</returns>
            public ValueReference this[FieldAccess access]
            {
                get => builder[access.Index];
                set
                {
                    Location.Assert(!value.Type.IsStructureType);
                    Location.Assert(
                        value.Type == Parent[access.Index] ||
                        value.ResolveAs<UndefinedValue>() != null);

                    builder[access.Index] = value;
                }
            }

            /// <summary>
            /// Returns the next expected type to be added.
            /// </summary>
            public TypeNode? NextExpectedType =>
                Count < Parent.NumFields ? Parent[Count] : null;

            #endregion

            #region Methods

            /// <summary>
            /// Adds the given value to the instance builder.
            /// </summary>
            /// <param name="value">The value to add.</param>
            public void Add(Value value)
            {
                Location.AssertNotNull(value);
                Location.Assert(
                    !value.Type.IsStructureType &&
                    Count + 1 <= Parent.NumFields);
                Location.Assert(
                    value.Type == Parent[Count] ||
                    value is UndefinedValue ||
                    Parent[Count].IsPaddingType);

                builder.Add(value);
            }

            /// <summary>
            /// Constructs a new value that represents the current value builder.
            /// </summary>
            /// <returns>The resulting value reference.</returns>
            public ValueReference Seal() => IRBuilder.FinishStructureBuilder(ref this);

            /// <summary>
            /// Moves the underlying array builder to a target list and outputs an
            /// assembled structure type.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            StructureType IInternalBuilder.Seal(ref ValueList values)
            {
                Parent.Assert(Count == Parent.NumFields);
                builder.MoveTo(ref values);
                return Parent;
            }

            #endregion
        }

        /// <summary>
        /// An instance builder for dynamically typed structure instances.
        /// </summary>
        public struct DynamicBuilder : IInternalBuilder
        {
            #region Instance

            private ValueList builder;

            /// <summary>
            /// Initializes a new instance builder.
            /// </summary>
            /// <param name="irBuilder">The current IR builder.</param>
            /// <param name="location">The current location.</param>
            /// <param name="capacity">The initial capacity.</param>
            internal DynamicBuilder(
                IRBuilder irBuilder,
                Location location,
                int capacity)
            {
                builder = ValueList.Create(capacity);
                IRBuilder = irBuilder;
                Location = location;
            }

            /// <summary>
            /// Initializes a new instance builder.
            /// </summary>
            /// <param name="irBuilder">The current IR builder.</param>
            /// <param name="location">The current location.</param>
            /// <param name="values">The initial capacity.</param>
            internal DynamicBuilder(
                IRBuilder irBuilder,
                Location location,
                ref ValueList values)
            {
                builder = ValueList.Empty;
                values.MoveTo(ref builder);
                IRBuilder = irBuilder;
                Location = location;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent builder.
            /// </summary>
            public IRBuilder IRBuilder { get; }

            /// <summary>
            /// Returns the current location.
            /// </summary>
            public Location Location { get; }

            /// <summary>
            /// The number of field values.
            /// </summary>
            public int Count => builder.Count;

            /// <summary>
            /// Returns the value that corresponds to the given field access.
            /// </summary>
            /// <param name="access">The field access.</param>
            /// <returns>The resolved field type.</returns>
            public ValueReference this[FieldAccess access]
            {
                get => builder[access.Index];
                set => builder[access.Index] = value;
            }

            #endregion

            #region Methods

            /// <summary>
            /// Adds the given value to the instance builder.
            /// </summary>
            /// <param name="value">The value to add.</param>
            public void Add(Value value)
            {
                Location.AssertNotNull(value);
                Location.Assert(!value.Type.IsStructureType);

                builder.Add(value);
            }

            /// <summary>
            /// Constructs a new value that represents the current value builder.
            /// </summary>
            /// <returns>The resulting value reference.</returns>
            public ValueReference Seal() => IRBuilder.FinishStructureBuilder(ref this);

            /// <summary>
            /// Moves the underlying array builder to a target list and outputs an
            /// assembled structure type.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            StructureType IInternalBuilder.Seal(ref ValueList values)
            {
                // Create a new structure type that corresponds to all value types
                var typeBuilder = IRBuilder.CreateStructureType(Count);
                foreach (var value in builder)
                    typeBuilder.Add(value.Type);
                builder.MoveTo(ref values);
                return typeBuilder.Seal().As<StructureType>(Location);
            }

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new structure value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="structureType">The associated structure type.</param>
        /// <param name="fieldValues">The field values.</param>
        internal StructureValue(
            in ValueInitializer initializer,
            StructureType structureType,
            ref ValueList fieldValues)
            : base(initializer)
        {
            StructureType = structureType;
            Seal(ref fieldValues);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Structure;

        /// <summary>
        /// Returns the structure type.
        /// </summary>
        public StructureType StructureType { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a new nested structure value.
        /// </summary>
        /// <param name="builder">The parent builder.</param>
        /// <param name="location">The current location.</param>
        /// <param name="fieldSpan">The field span.</param>
        /// <returns>The resolved structure value.</returns>
        public ValueReference Get(
            IRBuilder builder,
            Location location,
            FieldSpan fieldSpan)
        {
            if (!fieldSpan.HasSpan)
                return this[fieldSpan.Index];
            else if (fieldSpan.Index == 0 && fieldSpan.Span == Count)
                return this;

            var resultType = StructureType.Get(builder, fieldSpan)
                .As<StructureType>(location);
            var instance = builder.CreateStructure(location, resultType);
            for (int i = 0; i < fieldSpan.Span; ++i)
                instance.Add(this[fieldSpan.Index + i]);
            return instance.Seal();
        }

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            StructureType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder)
        {
            var instance = builder.CreateStructure(
                Location,
                StructureType);
            foreach (var value in Nodes)
                instance.Add(rebuilder.Rebuild(value));
            return instance.Seal();
        }

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer) { }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "struct";

        #endregion
    }

    /// <summary>
    /// Represents an operation on structure values.
    /// </summary>
    public abstract class StructureOperationValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new abstract structure operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="fieldSpan">The field span.</param>
        internal StructureOperationValue(
            in ValueInitializer initializer,
            FieldSpan fieldSpan)
            : base(initializer)
        {
            FieldSpan = fieldSpan;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the object value to load from.
        /// </summary>
        public ValueReference ObjectValue => this[0];

        /// <summary>
        /// Returns the structure type.
        /// </summary>
        public StructureType StructureType =>
            ObjectValue.Type.AsNotNullCast<StructureType>();

        /// <summary>(
        /// Returns the field span.
        /// </summary>
        public FieldSpan FieldSpan { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer)
        {
            writer.Write(nameof(FieldSpan.Index), FieldSpan.Index);
            writer.Write(nameof(FieldSpan.Span), FieldSpan.Span);
        }

        #endregion

        #region Object

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{ObjectValue}[{FieldSpan}]";

        #endregion
    }

    /// <summary>
    /// Represents an operation to load a single field from an object.
    /// </summary>
    [ValueKind(ValueKind.GetField)]
    public sealed class GetField : StructureOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new field load.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="structValue">The structure value.</param>
        /// <param name="fieldSpan">The field span.</param>
        internal GetField(
            in ValueInitializer initializer,
            ValueReference structValue,
            FieldSpan fieldSpan)
            : base(initializer, fieldSpan)
        {
            Seal(structValue);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GetField;

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            (ObjectValue.Type as StructureType)
            .AsNotNull()
            .Get(initializer.Context, FieldSpan);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGetField(
                Location,
                rebuilder.Rebuild(ObjectValue),
                FieldSpan);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "gfld";

        #endregion
    }

    /// <summary>
    /// Represents an operation to store a single field of an object.
    /// </summary>
    [ValueKind(ValueKind.SetField)]
    public sealed class SetField : StructureOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new field store.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="structValue">The structure value.</param>
        /// <param name="fieldSpan">The field access.</param>
        /// <param name="value">The value to store.</param>
        internal SetField(
            in ValueInitializer initializer,
            ValueReference structValue,
            FieldSpan fieldSpan,
            ValueReference value)
            : base(initializer, fieldSpan)
        {
            Seal(structValue, value);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.SetField;

        /// <summary>
        /// Returns the value to store.
        /// </summary>
        public ValueReference Value => this[1];

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            ObjectValue.Type;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateSetField(
                Location,
                rebuilder.Rebuild(ObjectValue),
                FieldSpan,
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "sfld";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            base.ToArgString() + " -> " + Value;

        #endregion
    }
}
