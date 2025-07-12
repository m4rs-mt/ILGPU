// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: InstanceId.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;

namespace ILGPU;

/// <summary>
/// Represents a unique instance id.
/// </summary>
/// <param name="Value">The raw id value.</param>
internal readonly record struct InstanceId(long Value) : IEquatable<InstanceId>
{
    #region Nested Types

    /// <summary>
    /// Compares instance id.
    /// </summary>
    public readonly struct Comparer : IEqualityComparer<InstanceId>
    {
        /// <summary>
        /// Returns true if both instance ids represent the same value.
        /// </summary>
        public readonly bool Equals(InstanceId x, InstanceId y) => x == y;

        /// <summary>
        /// Returns the hash code of the given instance id.
        /// </summary>
        public readonly int GetHashCode(InstanceId obj) => obj.GetHashCode();
    }

    #endregion

    #region Static

    /// <summary>
    /// Represents the empty instance id.
    /// </summary>
    public static readonly InstanceId Empty = new(-1);

    /// <summary>
    /// A shared static instance id counter.
    /// </summary>
    private static long _staticCounter;

    /// <summary>
    /// Creates a new unique instance id.
    /// </summary>
    /// <returns>The unique instance id.</returns>
    public static InstanceId CreateNew() => new(Interlocked.Add(ref _staticCounter, 1L));

    #endregion

    #region Methods

    /// <summary>
    /// Returns the string representation of the <see cref="Value"/> property.
    /// </summary>
    /// <returns>
    /// The string representation of the <see cref="Value"/> property.
    /// </returns>
    public readonly override string ToString() => Value.ToString();

    /// <summary>
    /// Converts the given instance id into its underlying long value.
    /// </summary>
    /// <param name="id">The instance id.</param>
    public static implicit operator long(InstanceId id) => id.Value;

    #endregion
}
