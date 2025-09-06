// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Fields.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// An index into to a scalar structure field.
/// </summary>
readonly struct FieldAccess : IEquatable<FieldAccess>
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
    public override string ToString() => $"{Index}";

    #endregion

    #region Operators

    /// <summary>
    /// Converts a field index into a field access instance.
    /// </summary>
    /// <param name="fieldIndex">The field index to convert.</param>
    public static implicit operator FieldAccess(int fieldIndex) =>
        new(fieldIndex);

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
/// <param name="fieldIndex">The field access.</param>
/// <param name="span">The number of fields to span.</param>
readonly struct FieldSpan(FieldAccess fieldIndex, int span) : IEquatable<FieldSpan>
{
    #region Instance

    /// <summary>
    /// Constructs a new field span.
    /// </summary>
    /// <param name="fieldIndex">The field access.</param>
    public FieldSpan(FieldAccess fieldIndex)
        : this(fieldIndex, 1)
    { }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the field index.
    /// </summary>
    public FieldAccess Access { get; } = fieldIndex;

    /// <summary>
    /// Returns the field index.
    /// </summary>
    public int Index => Access.Index;

    /// <summary>
    /// The number of fields to span.
    /// </summary>
    public int Span { get; } = Math.Max(span, 1);

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
    public FieldAccess GetLastAccess() => Access.Add(XMath.Max(Span - 1, 0));

    /// <summary>
    /// Returns true if the given field span is contained in this span.
    /// </summary>
    /// <param name="fieldSpan">The field span.</param>
    /// <returns>True, if the given field span is contained in this span.</returns>
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
    public static implicit operator FieldSpan(FieldAccess access) => new(access);

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
readonly struct FieldAccessChain : IEquatable<FieldAccessChain>
{
    #region Static

    /// <summary>
    /// An empty access chain.
    /// </summary>
    public static readonly FieldAccessChain Empty = new([]);

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
    /// Returns true if this access chain is a sub-chain of the given one.
    /// </summary>
    /// <param name="other">The other sub-chain.</param>
    /// <returns>
    /// True if this access chain is a sub-chain of the given one.
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
readonly struct FieldRef : IEquatable<FieldRef>
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
