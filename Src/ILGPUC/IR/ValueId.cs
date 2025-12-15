// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueId.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Threading;

namespace ILGPUC.IR;

/// <summary>
/// Represents a value id.
/// </summary>
/// <param name="Value">The raw value.</param>
readonly record struct ValueId(long Value) : IComparable<ValueId>
{
    /// <summary>
    /// A shared static instance id counter.
    /// </summary>
    private static long _instanceIdCounter;

    /// <summary>
    /// Returns an invalid value id.
    /// </summary>
    public static readonly ValueId Invalid = new(-1);

    /// <summary>
    /// Creates a new unique node id.
    /// </summary>
    /// <returns>A new unique node id.</returns>
    public static ValueId CreateNew() => new(Interlocked.Add(ref _instanceIdCounter, 1L));

    /// <summary>
    /// Compares this id to the given one.
    /// </summary>
    /// <param name="other">The object to compare to.</param>
    /// <returns>The comparison result.</returns>
    public int CompareTo(ValueId other) => Value.CompareTo(other.Value);

    /// <summary>
    /// Returns the raw string representation of the underlying value id.
    /// </summary>
    /// <returns>The raw value id of the underlying id.</returns>
    public override string ToString() => Value.ToString();
}
