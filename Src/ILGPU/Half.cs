// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Half.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
#if !DEBUG
using System.Diagnostics;
#endif
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// A half precision floating point value with 16 bit precision.
    /// </summary>
    [Serializable]
    public readonly partial struct Half : IEquatable<Half>, IComparable<Half>
    {
        #region Static

        /// <summary>
        /// Returns the absolute value of the given half value.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>The absolute value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Abs)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half Abs(Half half) => HalfExtensions.Abs(half);

        /// <summary>
        /// Returns true if the given half value represents a NaN value.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>True, if the given half represents a NaN value.</returns>
        [MathIntrinsic(MathIntrinsicKind.IsNaNF)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNaN(Half half) => HalfExtensions.IsNaN(half);

        /// <summary>
        /// Returns true if the given half value represents 0.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>True, if the given half represents 0.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsZero(Half half) => HalfExtensions.IsZero(half);

        /// <summary>
        /// Returns true if the given half value represents +infinity.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>True, if the given half value represents +infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPositiveInfinity(Half half) =>
            HalfExtensions.IsPositiveInfinity(half);

        /// <summary>
        /// Returns true if the given half value represents -infinity.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>True, if the given half value represents -infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNegativeInfinity(Half half) =>
            HalfExtensions.IsNegativeInfinity(half);

        /// <summary>
        /// Returns true if the given half value represents infinity.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>True, if the given half value represents infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInfinity(Half half) => HalfExtensions.IsInfinity(half);

        /// <summary>
        /// Returns true if the given half value represents a finite number.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>True, if the given half value represents a finite number.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(Half half) => HalfExtensions.IsFinite(half);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new half value.
        /// </summary>
        /// <param name="rawValue">The underlying raw value.</param>
        internal Half(ushort rawValue)
        {
            RawValue = rawValue;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Represents the raw value.
        /// </summary>
#if !DEBUG
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
#endif
        internal ushort RawValue { get; }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given half is equal to the current half.
        /// </summary>
        /// <param name="other">The other half.</param>
        /// <returns>True, if the given half is equal to the current half.</returns>
        public readonly bool Equals(Half other) => this == other;

        #endregion

        #region IComparable

        /// <summary>
        /// Compares this half value to the given half.
        /// </summary>
        /// <param name="other">The other half.</param>
        /// <returns>The result of the half comparison.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int CompareTo(Half other) => ((float)this).CompareTo(other);

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current half.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to the current half.</returns>
        public readonly override bool Equals(object? obj) =>
            obj is Half half && Equals(half);

        /// <summary>
        /// Returns the hash code of this half.
        /// </summary>
        /// <returns>The hash code of this half.</returns>
        public readonly override int GetHashCode() => RawValue;

        /// <summary>
        /// Returns the string representation of this half.
        /// </summary>
        /// <returns>The string representation of this half.</returns>
        public readonly override string ToString() => ((float)this).ToString();

        #endregion

        #region Operators

        /// <summary>
        /// Negates the given half value.
        /// </summary>
        /// <param name="halfValue">The half value to negate.</param>
        /// <returns>The negated half value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Neg)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half operator -(Half halfValue) => HalfExtensions.Neg(halfValue);

        /// <summary>
        /// Adds two half values.
        /// </summary>
        /// <param name="first">The first half.</param>
        /// <param name="second">The second half.</param>
        /// <returns>The resulting half value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Add)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half operator +(Half first, Half second) =>
            HalfExtensions.AddFP32(first, second);

        /// <summary>
        /// Subtracts two half values.
        /// </summary>
        /// <param name="first">The first half.</param>
        /// <param name="second">The second half.</param>
        /// <returns>The resulting half value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Sub)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half operator -(Half first, Half second) =>
            HalfExtensions.SubFP32(first, second);

        /// <summary>
        /// Multiplies two half values.
        /// </summary>
        /// <param name="first">The first half.</param>
        /// <param name="second">The second half.</param>
        /// <returns>The resulting half value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Mul)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half operator *(Half first, Half second) =>
            HalfExtensions.MulFP32(first, second);

        /// <summary>
        /// Divides two half values.
        /// </summary>
        /// <param name="first">The first half.</param>
        /// <param name="second">The second half.</param>
        /// <returns>The resulting half value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Div)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half operator /(Half first, Half second) =>
            HalfExtensions.DivFP32(first, second);

        /// <summary>
        /// Returns true if the first and second half represent the same value.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>True, if the first and second half are the same.</returns>
        [CompareIntrinisc(CompareKind.Equal)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Half first, Half second) =>
            (float)first == second;

        /// <summary>
        /// Returns true if the first and second half represent not the same value.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>True, if the first and second half are not the same.</returns>
        [CompareIntrinisc(CompareKind.NotEqual, CompareFlags.UnsignedOrUnordered)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Half first, Half second) =>
            (float)first != second;

        /// <summary>
        /// Returns true if the first half is smaller than the second half.
        /// </summary>
        /// <param name="first">The first half.</param>
        /// <param name="second">The second half.</param>
        /// <returns>True, if the first half is smaller than the second half.</returns>
        [CompareIntrinisc(CompareKind.LessThan)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(Half first, Half second) =>
            (float)first < second;

        /// <summary>
        /// Returns true if the first half is smaller than or equal to the half index.
        /// </summary>
        /// <param name="first">The first half.</param>
        /// <param name="second">The second half.</param>
        /// <returns>
        /// True, if the first half is smaller than or equal to the second half.
        /// </returns>
        [CompareIntrinisc(CompareKind.LessEqual)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(Half first, Half second) =>
            (float)first <= second;

        /// <summary>
        /// Returns true if the first half is greater than the second half.
        /// </summary>
        /// <param name="first">The first half.</param>
        /// <param name="second">The second half.</param>
        /// <returns>True, if the first half is greater than the second half.</returns>
        [CompareIntrinisc(CompareKind.GreaterThan)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(Half first, Half second) =>
            (float)first > second;

        /// <summary>
        /// Returns true if the first half is greater than or equal to the second half.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>
        /// True, if the first index is greater than or equal to the second index.
        /// </returns>
        [CompareIntrinisc(CompareKind.GreaterEqual)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(Half first, Half second) =>
            (float)first >= second;

        /// <summary>
        /// Implicitly converts a half to an float.
        /// </summary>
        /// <param name="halfValue">The half to convert.</param>
        [ConvertIntrinisc]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator float(Half halfValue) =>
            HalfExtensions.ConvertHalfToFloat(halfValue);

        /// <summary>
        /// Implicitly converts a half to an double.
        /// </summary>
        /// <param name="halfValue">The half to convert.</param>
        [ConvertIntrinisc]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator double(Half halfValue) =>
            (float)halfValue;

        /// <summary>
        /// Explicitly converts a float to a half.
        /// </summary>
        /// <param name="floatValue">The float to convert.</param>
        [ConvertIntrinisc]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Half(float floatValue) =>
            HalfExtensions.ConvertFloatToHalf(floatValue);

        /// <summary>
        /// Explicitly converts a double to a half.
        /// </summary>
        /// <param name="doubleValue">The double to convert.</param>
        [ConvertIntrinisc]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Half(double doubleValue) =>
            (Half)(float)doubleValue;

        #endregion
    }

    internal static partial class HalfExtensions
    {
        /// <summary>
        /// Negates the given half value.
        /// </summary>
        /// <param name="halfValue">The half value to negate.</param>
        /// <returns>The negated half value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half Neg(Half halfValue) =>
            new Half((ushort)(halfValue.RawValue ^ SignBitMask));

        /// <summary>
        /// Returns the absolute value of the given half value.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>The absolute value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half Abs(Half half) =>
            new Half((ushort)(half.RawValue & ExponentMantissaMask));

        /// <summary>
        /// Returns true if the given half value represents a NaN value.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>True, if the given half represents a NaN value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNaN(Half half) =>
            (half.RawValue & ExponentMantissaMask) > ExponentMask;

        /// <summary>
        /// Returns true if the given half value represents 0.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>True, if the given half represents 0.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsZero(Half half) =>
            (half.RawValue & ExponentMantissaMask) == 0;

        /// <summary>
        /// Returns true if the given half value represents +infinity.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>True, if the given half value represents +infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPositiveInfinity(Half half) =>
            half == Half.PositiveInfinity;

        /// <summary>
        /// Returns true if the given half value represents -infinity.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>True, if the given half value represents -infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNegativeInfinity(Half half) =>
            half == Half.NegativeInfinity;

        /// <summary>
        /// Returns true if the given half value represents infinity.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>True, if the given half value represents infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInfinity(Half half) =>
            (half.RawValue & ExponentMantissaMask) == ExponentMask;

        /// <summary>
        /// Returns true if the given half value represents a finite number.
        /// </summary>
        /// <param name="half">The half value.</param>
        /// <returns>True, if the given half value represents a finite number.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(Half half) =>
            Bitwise.And(!IsNaN(half), !IsInfinity(half));

        /// <summary>
        /// Implements a FP16 addition using FP32.
        /// </summary>
        /// <param name="first">The first half.</param>
        /// <param name="second">The second half.</param>
        /// <returns>The resulting half value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half AddFP32(Half first, Half second) =>
            (Half)((float)first + second);

        /// <summary>
        /// Implements a FP16 subtraction using FP32.
        /// </summary>
        /// <param name="first">The first half.</param>
        /// <param name="second">The second half.</param>
        /// <returns>The resulting half value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half SubFP32(Half first, Half second) =>
            (Half)((float)first - second);

        /// <summary>
        /// Implements a FP16 multiplication using FP32.
        /// </summary>
        /// <param name="first">The first half.</param>
        /// <param name="second">The second half.</param>
        /// <returns>The resulting half value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half MulFP32(Half first, Half second) =>
            (Half)((float)first * second);

        /// <summary>
        /// Implements a FP16 division using FP32.
        /// </summary>
        /// <param name="first">The first half.</param>
        /// <param name="second">The second half.</param>
        /// <returns>The resulting half value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half DivFP32(Half first, Half second) =>
            (Half)((float)first / second);

        /// <summary>
        /// Implements a FP16 division using FP32.
        /// </summary>
        /// <param name="first">The first half.</param>
        /// <param name="second">The second half.</param>
        /// <param name="third">The third half.</param>
        /// <returns>The resulting half value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half FmaFP32(Half first, Half second, Half third) =>
            (Half)((float)first * second + third);
    }
}
