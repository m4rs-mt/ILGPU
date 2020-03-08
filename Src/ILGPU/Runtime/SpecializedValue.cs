// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: SpecializedValue.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Provides static helper functions for the structure <see cref="SpecializedValue{T}"/>.
    /// </summary>
    public static class SpecializedValue
    {
        /// <summary>
        /// Creates a new specialized value instance.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>The new specialized value.</returns>
        public static SpecializedValue<T> New<T>(in T value)
            where T : struct, IEquatable<T> =>
            new SpecializedValue<T>(value);
    }

    /// <summary>
    /// Represents a dynamically specialized value that can be passed to a kernel.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public struct SpecializedValue<T> : IEquatable<SpecializedValue<T>>
        where T : struct, IEquatable<T>
    {
        #region Instance

        /// <summary>
        /// Constructs a new specialized value.
        /// </summary>
        /// <param name="value">The underlying value to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpecializedValue(T value)
        {
            Value = value;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the underlying value.
        /// </summary>
        public T Value { get; set; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the other specialized value is equal to this value.
        /// </summary>
        /// <param name="other">The other specialized value.</param>
        /// <returns>True, if the other specialized value is equal to this value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SpecializedValue<T> other) => Value.Equals(other.Value);

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to this value.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to this value.</returns>
        public override bool Equals(object obj) =>
            obj is SpecializedValue<T> other && Equals(other);

        /// <summary>
        /// Returns the hash code of this value.
        /// </summary>
        /// <returns>The hash code of this value.</returns>
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// Returns the string representation of this value.
        /// </summary>
        /// <returns>The string representation of this value.</returns>
        public override string ToString() => Value.ToString();

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the first and second value are the same.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>True, if the first and second value are the same.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SpecializedValue<T> first, SpecializedValue<T> second) =>
            first.Equals(second);

        /// <summary>
        /// Returns true if the first and second value are not the same.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>True, if the first and second value are not the same.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SpecializedValue<T> first, SpecializedValue<T> second) =>
            !first.Equals(second);

        /// <summary>
        /// Converts the given <see cref="SpecializedValue{T}"/> instance into its underlying value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(SpecializedValue<T> value) => value.Value;

        #endregion
    }
}
