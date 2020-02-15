// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Index1.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ILGPU
{
    /// <summary>
    /// Represents a 1D index.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Index1 : IIntrinsicIndex, IGenericIndex<Index1>
    {
        #region Static

        /// <summary>
        /// Represents an invalid index (-1);
        /// </summary>
        public static readonly Index1 Invalid = new Index1(-1);

        /// <summary>
        /// Represents an index with zero.
        /// </summary>
        public static readonly Index1 Zero = new Index1(0);

        /// <summary>
        /// Represents an index with 1.
        /// </summary>
        public static readonly Index1 One = new Index1(1);

        /// <summary>
        /// Returns the grid dimension for this index type.
        /// </summary>
        public static Index1 Dimension => Grid.Dimension.X;

        /// <summary>
        /// Returns the main constructor to create a new index instance.
        /// </summary>
        internal static ConstructorInfo MainConstructor =
            typeof(Index1).GetConstructor(new Type[] { typeof(int) });

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        public static Index1 Min(Index1 first, Index1 second) =>
            new Index1(IntrinsicMath.Min(first.X, second.X));

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        public static Index1 Max(Index1 first, Index1 second) =>
            new Index1(IntrinsicMath.Max(first.X, second.X));

        /// <summary>
        /// Clamps the given index value according to Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static Index1 Clamp(Index1 value, Index1 min, Index1 max) =>
            new Index1(IntrinsicMath.Clamp(value.X, min.X, max.X));

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new 1D index.
        /// </summary>
        /// <param name="x">The x index.</param>
        public Index1(int x)
        {
            X = x;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the x index.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Returns true if this is the first index.
        /// </summary>
        public bool IsFirst => X == 0;

        /// <summary>
        /// Returns the current index type.
        /// </summary>
        public IndexType IndexType => IndexType.Index1D;

        /// <summary>
        /// Returns the size represented by this index (x);
        /// </summary>
        public int Size => X;

        #endregion

        #region IGenericIndex

        /// <summary cref="IGenericIndex{TIndex}.InBounds(TIndex)"/>
        public bool InBounds(Index1 dimension) =>
            X >= 0 && X < dimension.X;

        /// <summary cref="IGenericIndex{TIndex}.InBoundsInclusive(TIndex)"/>
        public bool InBoundsInclusive(Index1 dimension) =>
            X >= 0 && X <= dimension.X;

        /// <summary cref="IGenericIndex{TIndex}.ComputeLinearIndex(TIndex)"/>
        public int ComputeLinearIndex(Index1 dimension) => X;

        /// <summary cref="IGenericIndex{TIndex}.ReconstructIndex(int)"/>
        public Index1 ReconstructIndex(int linearIndex) => linearIndex;

        /// <summary cref="IGenericIndex{TIndex}.Add(TIndex)"/>
        public Index1 Add(Index1 rhs) => this + rhs;

        /// <summary cref="IGenericIndex{TIndex}.Subtract(TIndex)"/>
        public Index1 Subtract(Index1 rhs) => this - rhs;

        /// <summary cref="IGenericIndex{TIndex}.ComputedCastedExtent(TIndex, int, int)"/>
        public Index1 ComputedCastedExtent(Index1 extent, int elementSize, int newElementSize) =>
            (extent.Size * elementSize) / newElementSize;

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given index is equal to the current index.
        /// </summary>
        /// <param name="other">The other index.</param>
        /// <returns>True, if the given index is equal to the current index.</returns>
        public bool Equals(Index1 other) => this == other;

        #endregion

        #region IComparable

        /// <summary cref="IComparable{T}.CompareTo(T)"/>
        public int CompareTo(Index1 other)
        {
            if (this < other)
                return -1;
            else if (this > other)
                return 1;
            Debug.Assert(this == other);
            return 0;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current index.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to the current index.</returns>
        public override bool Equals(object obj) =>
            obj is Index1 other && Equals(other);

        /// <summary>
        /// Returns the hash code of this index.
        /// </summary>
        /// <returns>The hash code of this index.</returns>
        public override int GetHashCode() => X.GetHashCode();

        /// <summary>
        /// Returns the string representation of this index.
        /// </summary>
        /// <returns>The string representation of this index.</returns>
        public override string ToString() => X.ToString();

        #endregion

        #region Operators

        /// <summary>
        /// Adds two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The added index.</returns>
        public static Index1 Add(Index1 first, Index1 second) =>
            first + second;

        /// <summary>
        /// Adds two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The added index.</returns>
        public static Index1 operator +(Index1 first, Index1 second) =>
            first.X + second.X;

        /// <summary>
        /// Subtracts two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The subtracted index.</returns>
        public static Index1 Subtract(Index1 first, Index1 second) =>
            first - second;

        /// <summary>
        /// Subtracts two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The subtracted index.</returns>
        public static Index1 operator -(Index1 first, Index1 second) =>
            first.X - second.X;

        /// <summary>
        /// Multiplies two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The multiplied index.</returns>
        public static Index1 Multiply(Index1 first, Index1 second) =>
            first * second;

        /// <summary>
        /// Multiplies two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The multiplied index.</returns>
        public static Index1 operator *(Index1 first, Index1 second) =>
            first.X * second.X;

        /// <summary>
        /// Divides two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The divided index.</returns>
        public static Index1 Divide(Index1 first, Index1 second) =>
            first / second;

        /// <summary>
        /// Divides two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The divided index.</returns>
        public static Index1 operator /(Index1 first, Index1 second) =>
            first.X / second.X;

        /// <summary>
        /// Returns true if the first and second index are the same.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, if the first and second index are the same.</returns>
        public static bool operator ==(Index1 first, Index1 second) =>
            first.X == second.X;

        /// <summary>
        /// Returns true if the first and second index are not the same.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, if the first and second index are not the same.</returns>
        public static bool operator !=(Index1 first, Index1 second) =>
            first.X != second.X;

        /// <summary>
        /// Returns true if the first index is smaller than the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, if the first index is smaller than the second index.</returns>
        public static bool operator <(Index1 first, Index1 second) =>
            first.X < second.X;

        /// <summary>
        /// Returns true if the first index is smaller than or equal to the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, if the first index is smaller than or equal to the second index.</returns>
        public static bool operator <=(Index1 first, Index1 second) =>
            first.X <= second.X;

        /// <summary>
        /// Returns true if the first index is greater than the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, if the first index is greater than the second index.</returns>
        public static bool operator >(Index1 first, Index1 second) =>
            first.X > second.X;

        /// <summary>
        /// Returns true if the first index is greater than or equal to the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, if the first index is greater than or equal to the second index.</returns>
        public static bool operator >=(Index1 first, Index1 second) =>
            first.X >= second.X;

        /// <summary>
        /// Implictly converts an index to an int.
        /// </summary>
        /// <param name="idx">The index to convert.</param>
        public static implicit operator int(Index1 idx) => idx.X;

        /// <summary>
        /// Implictly converts an int to an index.
        /// </summary>
        /// <param name="idx">The int to convert.</param>
        public static implicit operator Index1(int idx) => new Index1(idx);

        /// <summary>
        /// Implictly converts an index to an uint.
        /// </summary>
        /// <param name="idx">The index to convert.</param>
        [CLSCompliant(false)]
        public static explicit operator uint(Index1 idx) => (uint)idx.X;

        #endregion
    }
}
