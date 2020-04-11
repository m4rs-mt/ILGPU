// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Index2.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ILGPU
{
    /// <summary>
    /// Represents a 2D index.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Index2 : IIntrinsicIndex, IGenericIndex<Index2>
    {
        #region Static

        /// <summary>
        /// Represents an invalid index (-1);
        /// </summary>
        public static readonly Index2 Invalid = new Index2(-1);

        /// <summary>
        /// Represents an index with zero.
        /// </summary>
        public static readonly Index2 Zero = new Index2(0, 0);

        /// <summary>
        /// Represents an index with 1.
        /// </summary>
        public static readonly Index2 One = new Index2(1, 1);

        /// <summary>
        /// Returns the grid dimension for this index type.
        /// </summary>
        [Obsolete("Use Grid.Dimension.XY instead")]
        public static Index2 Dimension => new Index2(Grid.DimX, Grid.DimY);

        /// <summary>
        /// Reconstructs a 2D index from a linear index.
        /// </summary>
        /// <param name="linearIndex">The linear index.</param>
        /// <param name="dimension">The 2D dimension for reconstruction.</param>
        /// <returns>The reconstructed 2D index.</returns>
        public static Index2 ReconstructIndex(int linearIndex, Index2 dimension)
        {
            var x = linearIndex % dimension.X;
            var y = linearIndex / dimension.X;
            return new Index2(x, y);
        }

        /// <summary>
        /// Returns the main constructor to create a new index instance.
        /// </summary>
        internal static ConstructorInfo MainConstructor =
            typeof(Index2).GetConstructor(new Type[] { typeof(int), typeof(int) });

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        public static Index2 Min(Index2 first, Index2 second) =>
            new Index2(
                IntrinsicMath.Min(first.X, second.X),
                IntrinsicMath.Min(first.Y, second.Y));

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        public static Index2 Max(Index2 first, Index2 second) =>
            new Index2(
                IntrinsicMath.Max(first.X, second.X),
                IntrinsicMath.Max(first.Y, second.Y));

        /// <summary>
        /// Clamps the given index value according to Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static Index2 Clamp(Index2 value, Index2 min, Index2 max) =>
            new Index2(
                IntrinsicMath.Clamp(value.X, min.X, max.X),
                IntrinsicMath.Clamp(value.Y, min.Y, max.Y));

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new 2D index.
        /// </summary>
        /// <param name="value">The value of every component (x, y).</param>
        public Index2(int value)
            : this(value, value)
        { }

        /// <summary>
        /// Constructs a new 2D index.
        /// </summary>
        /// <param name="x">The x index.</param>
        /// <param name="y">The y index.</param>
        public Index2(int x, int y)
        {
            X = x;
            Y = y;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the x index.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Returns the y index.
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Returns true if this is the first index.
        /// </summary>
        public bool IsFirst => X == 0 && Y == 0;

        /// <summary>
        /// Returns the current index type.
        /// </summary>
        public IndexType IndexType => IndexType.Index2D;

        /// <summary>
        /// Returns the size represented by this index (x * y).
        /// </summary>
        public int Size => X * Y;

        #endregion

        #region IGenericIndex

        /// <summary cref="IGenericIndex{TIndex}.InBounds(TIndex)"/>
        public bool InBounds(Index2 dimension) =>
            X >= 0 && X < dimension.X &&
            Y >= 0 && Y < dimension.Y;

        /// <summary cref="IGenericIndex{TIndex}.InBoundsInclusive(TIndex)"/>
        public bool InBoundsInclusive(Index2 dimension) =>
            X >= 0 && X <= dimension.X &&
            Y >= 0 && Y <= dimension.Y;

        /// <summary>
        /// Computes the linear index of this 2D index by using the provided 2D dimension.
        /// </summary>
        /// <param name="dimension">The dimension for index computation.</param>
        /// <returns>The computed linear index of this 2D index.</returns>
        public int ComputeLinearIndex(Index2 dimension) => Y * dimension.X + X;

        /// <summary>
        /// Reconstructs a 2D index from a linear index.
        /// </summary>
        /// <param name="linearIndex">The linear index.</param>
        /// <returns>The reconstructed 2D index.</returns>
        public Index2 ReconstructIndex(int linearIndex) =>
            ReconstructIndex(linearIndex, this);

        /// <summary cref="IGenericIndex{TIndex}.Add(TIndex)"/>
        public Index2 Add(Index2 rhs) => this + rhs;

        /// <summary cref="IGenericIndex{TIndex}.Subtract(TIndex)"/>
        public Index2 Subtract(Index2 rhs) => this - rhs;

        /// <summary cref="IGenericIndex{TIndex}.ComputedCastedExtent(TIndex, int, int)"/>
        public Index2 ComputedCastedExtent(
            Index2 extent,
            int elementSize,
            int newElementSize)
        {
            var xExtent = (extent.X * elementSize) / newElementSize;
            Debug.Assert(xExtent > 0, "OutOfBounds cast");
            return new Index2(xExtent, extent.Y);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Deconstructs the current instance into a value tuple.
        /// </summary>
        /// <param name="x">The x value.</param>
        /// <param name="y">The y value.</param>
        public void Deconstruct(out int x, out int y)
        {
            x = X;
            y = Y;
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given index is equal to the current index.
        /// </summary>
        /// <param name="other">The other index.</param>
        /// <returns>True, if the given index is equal to the current index.</returns>
        public bool Equals(Index2 other) => this == other;

        #endregion

        #region IComparable

        /// <summary cref="IComparable{T}.CompareTo(T)"/>
        public int CompareTo(Index2 other)
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
        public override bool Equals(object obj) => obj is Index2 other && Equals(other);

        /// <summary>
        /// Returns the hash code of this index.
        /// </summary>
        /// <returns>The hash code of this index.</returns>
        public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode();

        /// <summary>
        /// Returns the string representation of this index.
        /// </summary>
        /// <returns>The string representation of this index.</returns>
        public override string ToString() => $"({X}, {Y})";

        #endregion

        #region Operators

        /// <summary>
        /// Converts the given value tuple into an equivalent <see cref="Index2"/>.
        /// </summary>
        /// <param name="values">The values.</param>
        public static implicit operator Index2((int, int) values) =>
            new Index2(values.Item1, values.Item2);

        /// <summary>
        /// Adds two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The added result index.</returns>
        public static Index2 Add(Index2 first, Index2 second) => first + second;

        /// <summary>
        /// Adds two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The added result index.</returns>
        public static Index2 operator +(Index2 first, Index2 second) =>
            new Index2(first.X + second.X, first.Y + second.Y);

        /// <summary>
        /// Subtracts two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The subtracted result index.</returns>
        public static Index2 Subtract(Index2 first, Index2 second) => first - second;

        /// <summary>
        /// Subtracts two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The subtracted result index.</returns>
        public static Index2 operator -(Index2 first, Index2 second) =>
            new Index2(first.X - second.X, first.Y - second.Y);

        /// <summary>
        /// Multiplies an index with a scalar (component wise).
        /// </summary>
        /// <param name="first">The scalar value.</param>
        /// <param name="second">The index.</param>
        /// <returns>The multiplied index.</returns>
        public static Index2 operator *(int first, Index2 second) =>
            new Index2(first) * second;

        /// <summary>
        /// Multiplies an index with a scalar (component wise).
        /// </summary>
        /// <param name="first">The index.</param>
        /// <param name="second">The scalar value.</param>
        /// <returns>The multiplied index.</returns>
        public static Index2 operator *(Index2 first, int second) =>
            first * new Index2(second);

        /// <summary>
        /// Multiplies two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The multiplied index.</returns>
        public static Index2 Multiply(Index2 first, Index2 second) => first * second;

        /// <summary>
        /// Multiplies two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The multiplied index.</returns>
        public static Index2 operator *(Index2 first, Index2 second) =>
            new Index2(first.X * second.X, first.Y * second.Y);

        /// <summary>
        /// Divides an index with a scalar (component wise).
        /// </summary>
        /// <param name="first">The scalar value.</param>
        /// <param name="second">The index.</param>
        /// <returns>The divided index.</returns>
        public static Index2 operator /(int first, Index2 second) =>
            new Index2(first) / second;

        /// <summary>
        /// Divides an index with a scalar (component wise).
        /// </summary>
        /// <param name="first">The index.</param>
        /// <param name="second">The scalar value.</param>
        /// <returns>The divided index.</returns>
        public static Index2 operator /(Index2 first, int second) =>
            first / new Index2(second);

        /// <summary>
        /// Divides two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The divided index.</returns>
        public static Index2 Divide(Index2 first, Index2 second) => first / second;

        /// <summary>
        /// Divides two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The divided index.</returns>
        public static Index2 operator /(Index2 first, Index2 second) =>
            new Index2(first.X / second.X, first.Y / second.Y);

        /// <summary>
        /// Returns true if the first and second index are the same.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, if the first and second index are the same.</returns>
        public static bool operator ==(Index2 first, Index2 second) =>
            first.X == second.X && first.Y == second.Y;

        /// <summary>
        /// Returns true if the first and second index are not the same.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, if the first and second index are not the same.</returns>
        public static bool operator !=(Index2 first, Index2 second) =>
            first.X != second.X || first.Y != second.Y;

        /// <summary>
        /// Returns true if the first index is smaller than the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, if the first index is smaller than the second index.</returns>
        public static bool operator <(Index2 first, Index2 second) =>
            first.X < second.X && first.Y < second.Y;

        /// <summary>
        /// Returns true if the first index is smaller than or equal to the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>
        /// True, if the first index is smaller than or equal to the second index.
        /// </returns>
        public static bool operator <=(Index2 first, Index2 second) =>
            first.X <= second.X && first.Y <= second.Y;

        /// <summary>
        /// Returns true if the first index is greater than the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, if the first index is greater than the second index.</returns>
        public static bool operator >(Index2 first, Index2 second) =>
            first.X > second.X && first.Y > second.Y;

        /// <summary>
        /// Returns true if the first index is greater than or equal to the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>
        /// True, if the first index is greater than or equal to the second index.
        /// </returns>
        public static bool operator >=(Index2 first, Index2 second) =>
            first.X >= second.X && first.Y >= second.Y;

        #endregion
    }
}
