// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Index.cs
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
    public struct Index : IIntrinsicIndex, IGenericIndex<Index>
    {
        #region Static

        /// <summary>
        /// Represents an invalid index (-1);
        /// </summary>
        public static readonly Index Invalid = new Index(-1);

        /// <summary>
        /// Represents an index with zero.
        /// </summary>
        public static readonly Index Zero = new Index(0);

        /// <summary>
        /// Represents an index with 1.
        /// </summary>
        public static readonly Index One = new Index(1);

        /// <summary>
        /// Returns the grid dimension for this index type.
        /// </summary>
        public static Index Dimension => Grid.Dimension.X;

        /// <summary>
        /// Returns the main constructor to create a new index instance.
        /// </summary>
        internal static ConstructorInfo MainConstructor =
            typeof(Index).GetConstructor(new Type[] { typeof(int) });

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        public static Index Min(Index first, Index second)
        {
            return new Index(GPUMath.Min(first.X, second.X));
        }

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        public static Index Max(Index first, Index second)
        {
            return new Index(GPUMath.Max(first.X, second.X));
        }

        /// <summary>
        /// Clamps the given index value according to Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static Index Clamp(Index value, Index min, Index max)
        {
            return new Index(GPUMath.Clamp(value.X, min.X, max.X));
        }

        #endregion

        #region Instance

        private int x;

        /// <summary>
        /// Constructs a new 1D index.
        /// </summary>
        /// <param name="x">The x index.</param>
        public Index(int x)
        {
            this.x = x;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the x index.
        /// </summary>
        public int X => x;

        /// <summary>
        /// Returns true iff this is the first index.
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
        public bool InBounds(Index dimension)
        {
            return x >= 0 && x < dimension.x;
        }

        /// <summary cref="IGenericIndex{TIndex}.InBoundsInclusive(TIndex)"/>
        public bool InBoundsInclusive(Index dimension)
        {
            return x >= 0 && x <= dimension.x;
        }

        /// <summary cref="IGenericIndex{TIndex}.ComputeLinearIndex(TIndex)"/>
        public int ComputeLinearIndex(Index dimension)
        {
            return x;
        }

        /// <summary cref="IGenericIndex{TIndex}.ReconstructIndex(int)"/>
        public Index ReconstructIndex(int linearIndex)
        {
            return linearIndex;
        }

        /// <summary cref="IGenericIndex{TIndex}.Add(TIndex)"/>
        public Index Add(Index rhs)
        {
            return this + rhs;
        }

        /// <summary cref="IGenericIndex{TIndex}.Subtract(TIndex)"/>
        public Index Subtract(Index rhs)
        {
            return this - rhs;
        }

        /// <summary cref="IGenericIndex{TIndex}.ComputedCastedExtent(TIndex, int, int)"/>
        public Index ComputedCastedExtent(Index extent, int elementSize, int newElementSize)
        {
            return (extent.Size * elementSize) / newElementSize;
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given index is equal to the current index.
        /// </summary>
        /// <param name="other">The other index.</param>
        /// <returns>True, iff the given index is equal to the current index.</returns>
        public bool Equals(Index other)
        {
            return this == other;
        }

        #endregion

        #region IComparable

        /// <summary cref="IComparable{T}.CompareTo(T)"/>
        public int CompareTo(Index other)
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
        /// Returns true iff the given object is equal to the current index.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current index.</returns>
        public override bool Equals(object obj)
        {
            if (obj is Index)
                return Equals((Index)obj);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this index.
        /// </summary>
        /// <returns>The hash code of this index.</returns>
        public override int GetHashCode()
        {
            return X.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of this index.
        /// </summary>
        /// <returns>The string representation of this index.</returns>
        public override string ToString()
        {
            return X.ToString();
        }

        #endregion

        #region Operators

        /// <summary>
        /// Adds two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The added index.</returns>
        public static Index Add(Index first, Index second)
        {
            return first + second;
        }

        /// <summary>
        /// Adds two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The added index.</returns>
        public static Index operator +(Index first, Index second)
        {
            return first.X + second.X;
        }

        /// <summary>
        /// Subtracts two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The subtracted index.</returns>
        public static Index Subtract(Index first, Index second)
        {
            return first - second;
        }

        /// <summary>
        /// Subtracts two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The subtracted index.</returns>
        public static Index operator -(Index first, Index second)
        {
            return first.X - second.X;
        }

        /// <summary>
        /// Multiplies two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The multiplied index.</returns>
        public static Index Multiply(Index first, Index second)
        {
            return first * second;
        }

        /// <summary>
        /// Multiplies two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The multiplied index.</returns>
        public static Index operator *(Index first, Index second)
        {
            return first.X * second.X;
        }

        /// <summary>
        /// Divides two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The divided index.</returns>
        public static Index Divide(Index first, Index second)
        {
            return first / second;
        }

        /// <summary>
        /// Divides two indices.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The divided index.</returns>
        public static Index operator /(Index first, Index second)
        {
            return first.X / second.X;
        }

        /// <summary>
        /// Returns true iff the first and second index are the same.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first and second index are the same.</returns>
        public static bool operator ==(Index first, Index second)
        {
            return first.X == second.X;
        }

        /// <summary>
        /// Returns true iff the first and second index are not the same.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first and second index are not the same.</returns>
        public static bool operator !=(Index first, Index second)
        {
            return first.X != second.X;
        }

        /// <summary>
        /// Returns true iff the first index is smaller than the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first index is smaller than the second index.</returns>
        public static bool operator <(Index first, Index second)
        {
            return first.X < second.X;
        }

        /// <summary>
        /// Returns true iff the first index is smaller than or equal to the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first index is smaller than or equal to the second index.</returns>
        public static bool operator <=(Index first, Index second)
        {
            return first.X <= second.X;
        }

        /// <summary>
        /// Returns true iff the first index is greater than the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first index is greater than the second index.</returns>
        public static bool operator >(Index first, Index second)
        {
            return first.X > second.X;
        }

        /// <summary>
        /// Returns true iff the first index is greater than or equal to the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first index is greater than or equal to the second index.</returns>
        public static bool operator >=(Index first, Index second)
        {
            return first.X >= second.X;
        }

        /// <summary>
        /// Implictly converts an index to an int.
        /// </summary>
        /// <param name="idx">The index to convert.</param>
        public static implicit operator int(Index idx)
        {
            return idx.X;
        }

        /// <summary>
        /// Implictly converts an int to an index.
        /// </summary>
        /// <param name="idx">The int to convert.</param>
        public static implicit operator Index(int idx)
        {
            return new Index(idx);
        }

        #endregion
    }
}
