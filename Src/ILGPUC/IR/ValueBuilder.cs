// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;

namespace ILGPUC.IR;

/// <summary>
/// A specialized builder list for values.
/// </summary>
/// <param name="generation">The current generation.</param>
/// <param name="capacity">The expected capacity.</param>
struct ValueBuilderList(Generation generation, int capacity) : IGenerationObject
{
    /// <summary>
    /// Creates an empty value list.
    /// </summary>
    /// <param name="generation">The current generation.</param>
    /// <returns>The created value list.</returns>
    public static ValueBuilderList Empty(Generation generation) =>
        new(generation, 0);

    /// <summary>
    /// Creates a new empty value list.
    /// </summary>
    /// <param name="generation">The current generation.</param>
    /// <param name="capacity">The initial capacity.</param>
    /// <returns>The created value list.</returns>
    public static ValueBuilderList Create(Generation generation, int capacity) =>
        new(generation, capacity);

    private InlineList<Value> _values = InlineList<Value>.Create(capacity);

    /// <summary>
    /// Creates a new value builder list.
    /// </summary>
    /// <param name="generation">The current generation.</param>
    /// <param name="value1">The initial value to include.</param>
    public ValueBuilderList(Generation generation, Value? value1) : this(generation, 1)
    {
        Add(value1);
    }

    /// <summary>
    /// Creates a new value builder list.
    /// </summary>
    /// <param name="generation">The current generation.</param>
    /// <param name="value1">The first value to include.</param>
    /// <param name="value2">The second value to include.</param>
    public ValueBuilderList(
        Generation generation,
        Value? value1,
        Value? value2) : this(generation, 2)
    {
        Add(value1);
        Add(value2);
    }

    /// <summary>
    /// Creates a new value builder list.
    /// </summary>
    /// <param name="generation">The current generation.</param>
    /// <param name="value1">The first value to include.</param>
    /// <param name="value2">The second value to include.</param>
    /// <param name="value3">The third value to include.</param>
    public ValueBuilderList(
        Generation generation,
        Value? value1,
        Value? value2,
        Value? value3) : this(generation, 3)
    {
        Add(value1);
        Add(value2);
        Add(value3);
    }

    /// <summary>
    /// Creates a new value builder list.
    /// </summary>
    /// <param name="generation">The current generation.</param>
    /// <param name="values">The initial values to include.</param>
    public ValueBuilderList(Generation generation, params ReadOnlySpan<Value?> values)
        : this(generation, values.Length)
    {
        foreach (var value in values)
            Add(value);
    }

    /// <summary>
    /// Returns the current generation.
    /// </summary>
    public Generation Generation { get; } = generation;

    /// <summary>
    /// Returns the number of items.
    /// </summary>
    public readonly int Count => _values.Count;

    /// <summary>
    /// Converts this inline list into a span.
    /// </summary>
    /// <returns>The span.</returns>
    public readonly Span<Value> AsSpan() => _values.AsSpan();

    /// <summary>
    /// Converts this inline list into a read-only span.
    /// </summary>
    /// <returns>The read-only span.</returns>
    public readonly ReadOnlySpan<Value> AsReadOnlySpan() => AsSpan();

    /// <summary>
    /// Adds a new value which may be null.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <returns>True if the value is not null</returns>
    public bool AddFront(Value? value)
    {
        if (value is not null)
        {
            Generation.ValidateCurrentOrPreviousGeneration(value);
            _values.Insert(0, value);
        }
        return value is not null;
    }

    /// <summary>
    /// Adds a new value which may be null.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <returns>True if the value is not null</returns>
    public bool Add(Value? value)
    {
        if (value is not null)
        {
            Generation.ValidateCurrentOrPreviousGeneration(value);
            _values.Add(value);
        }
        return value is not null;
    }

    /// <summary>
    /// Reserves the given additional capacity.
    /// </summary>
    /// <param name="additionalCapacity">The additional capacity to reserve.</param>
    public void ReserveAdditional(int additionalCapacity) =>
        _values.Reserve(_values.Count + additionalCapacity);

    /// <summary>
    /// Gets a value as a specific target type.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="index">The value index.</param>
    /// <returns>The value as target type.</returns>
    public readonly T Get<T>(int index) where T : Value =>
        _values[index].AsNotNullCast<T>();

    /// <summary>
    /// Reverses this list.
    /// </summary>
    public readonly void Reverse() => _values.Reverse();

    /// <summary>
    /// Moves the current items to the given target list.
    /// </summary>
    /// <param name="list">The target list to move to.</param>
    public void MoveTo(ref InlineList<Value> list) => _values.MoveTo(ref list);

    /// <summary>
    /// Moves the current items to the given target list.
    /// </summary>
    /// <param name="list">The target list to move to.</param>
    public void MoveTo(ref ValueBuilderList list) => MoveTo(ref list._values);

    /// <summary>
    /// Copies the current items to the given target list.
    /// </summary>
    /// <param name="list">The target list to copy to.</param>
    /// <param name="offset">The source offset to start copying at.</param>
    public readonly void CopyTo(ref ValueBuilderList list, int offset) =>
        _values.CopyTo(ref list._values, offset);

    /// <summary>
    /// Returns an enumerator to enumerate all items in this list.
    /// </summary>
    /// <returns>The enumerator.</returns>
    /// <remarks>
    /// CAUTION: iterating over this list can be dangerous, as the underlying inline
    /// list might change and this instance is a structure value.
    /// </remarks>
    public readonly ReadOnlySpan<Value>.Enumerator GetEnumerator() =>
        AsReadOnlySpan().GetEnumerator();

    /// <summary>
    /// Converts the given list into a read-only span.
    /// </summary>
    /// <param name="list">The list to convert.</param>
    public static implicit operator ReadOnlySpan<Value>(ValueBuilderList list) =>
        list._values;

    /// <summary>
    /// Converts the given list into an inline list.
    /// </summary>
    /// <param name="list">The list to convert.</param>
    public static implicit operator InlineList<Value>(ValueBuilderList list) =>
        list._values;
}
