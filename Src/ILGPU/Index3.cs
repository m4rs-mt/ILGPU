// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Index3.cs
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
    /// Represents a 3D index.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Index3 : IIntrinsicIndex, IGenericIndex<Index3>
    {
        #region Static

        /// <summary>
        /// Represents an invalid index (-1);
        /// </summary>
        public static readonly Index3 Invalid = new Index3(-1);

        /// <summary>
        /// Represents an index with zero.
        /// </summary>
        public static readonly Index3 Zero = new Index3(0, 0, 0);

        /// <summary>
        /// Represents an index with 1.
        /// </summary>
        public static readonly Index3 One = new Index3(1, 1, 1);

        /// <summary>
        /// Returns the grid dimension for this index type.
        /// </summary>
        public static Index3 Dimension => Grid.Dimension;

        /// <summary>
        /// Reconstructs a 3D index from a linear index.
        /// </summary>
        /// <param name="linearIndex">The lienar index.</param>
        /// <param name="dimension">The 3D dimension for reconstruction.</param>
        /// <returns>The reconstructed 3D index.</returns>
        public static Index3 ReconstructIndex(int linearIndex, Index3 dimension)
        {
            var x = linearIndex % dimension.X;
            var yz = linearIndex / dimension.X;
            var y = yz % dimension.Y;
            var z = yz / dimension.Y;
            return new Index3(x, y, z);
        }

        /// <summary>
        /// Returns the main constructor to create a new index instance.
        /// </summary>
        internal static ConstructorInfo MainConstructor =
            typeof(Index3).GetConstructor(new Type[] { typeof(int), typeof(int), typeof(int) });

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        public static Index3 Min(Index3 first, Index3 second) =>
            new Index3(
                IntrinsicMath.Min(first.X, second.X),
                IntrinsicMath.Min(first.Y, second.Y),
                IntrinsicMath.Min(first.Z, second.Z));

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        public static Index3 Max(Index3 first, Index3 second) =>
            new Index3(
                IntrinsicMath.Max(first.X, second.X),
                IntrinsicMath.Max(first.Y, second.Y),
                IntrinsicMath.Max(first.Z, second.Z));

        /// <summary>
        /// Clamps the given index value according to Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static Index3 Clamp(Index3 value, Index3 min, Index3 max) =>
            new Index3(
                IntrinsicMath.Clamp(value.X, min.X, max.X),
                IntrinsicMath.Clamp(value.Y, min.Y, max.Y),
                IntrinsicMath.Clamp(value.Z, min.Z, max.Z));

        #endregion

        #region Instance

        internal readonly int x;
        internal readonly int y;
        internal readonly int z;

        /// <summary>
        /// Constructs a new 3D index.
        /// </summary>
        /// <param name="value">The value of every component (x, y, z).</param>
        public Index3(int value)
            : this(value, value, value)
        { }

        /// <summary>
        /// Constructs a new 3D index.
        /// </summary>
        /// <param name="xy">The x and y indices.</param>
        /// <param name="z">The z index.</param>
        public Index3(Index2 xy, int z)
            : this(xy.X, xy.Y, z)
        { }

        /// <summary>
        /// Constructs a new 3D index.
        /// </summary>
        /// <param name="x">The x index.</param>
        /// <param name="yz">The y and z indices.</param>
        public Index3(int x, Index2 yz)
            : this(x, yz.X, yz.Y)
        { }

        /// <summary>
        /// Constructs a new 3D index.
        /// </summary>
        /// <param name="x">The x index.</param>
        /// <param name="y">The y index.</param>
        /// <param name="z">The z index.</param>
        public Index3(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the x index.
        /// </summary>
        public int X => x;

        /// <summary>
        /// Returns the y index.
        /// </summary>
        public int Y => y;

        /// <summary>
        /// Returns the z index.
        /// </summary>
        public int Z => z;

        /// <summary>
        /// Returns true iff this is the first index.
        /// </summary>
        public bool IsFirst => X == 0 && Y == 0 && Z == 0;

        /// <summary>
        /// Returns the current index type.
        /// </summary>
        public IndexType IndexType => IndexType.Index3D;

        /// <summary>
        /// Returns the size represented by this index (x * y * z).
        /// </summary>
        public int Size => X * Y * Z;

        /// <summary>
        /// /Returns the x and y components as <see cref="Index2"/>.
        /// </summary>
        public Index2 XY => new Index2(X, Y);

        /// <summary>
        /// /Returns the y and z components as <see cref="Index2"/>.
        /// </summary>
        public Index2 YZ => new Index2(Y, Z);

        #endregion

        #region IGenericIndex

        /// <summary cref="IGenericIndex{TIndex}.InBounds(TIndex)"/>
        public bool InBounds(Index3 dimension)
        {
            return x >= 0 && x < dimension.x &&
                y >= 0 && y < dimension.y &&
                z >= 0 && z < dimension.z;
        }

        /// <summary cref="IGenericIndex{TIndex}.InBoundsInclusive(TIndex)"/>
        public bool InBoundsInclusive(Index3 dimension)
        {
            return x >= 0 && x <= dimension.x &&
                y >= 0 && y <= dimension.y &&
                z >= 0 && z <= dimension.z;
        }

        /// <summary>
        /// Computes the linear index of this 3D index by using the provided 3D dimension.
        /// </summary>
        /// <param name="dimension">The dimension for index computation.</param>
        /// <returns>The computed linear index of this 3D index.</returns>
        public int ComputeLinearIndex(Index3 dimension)
        {
            return ((Z * dimension.Y) + Y) * dimension.X + X;
        }

        /// <summary>
        /// Reconstructs a 3D index from a linear index.
        /// </summary>
        /// <param name="linearIndex">The lienar index.</param>
        /// <returns>The reconstructed 3D index.</returns>
        public Index3 ReconstructIndex(int linearIndex)
        {
            return ReconstructIndex(linearIndex, this);
        }

        /// <summary cref="IGenericIndex{TIndex}.Add(TIndex)"/>
        public Index3 Add(Index3 rhs)
        {
            return this + rhs;
        }

        /// <summary cref="IGenericIndex{TIndex}.Subtract(TIndex)"/>
        public Index3 Subtract(Index3 rhs)
        {
            return this - rhs;
        }

        /// <summary cref="IGenericIndex{TIndex}.ComputedCastedExtent(TIndex, int, int)"/>
        public Index3 ComputedCastedExtent(Index3 extent, int elementSize, int newElementSize)
        {
            var zExtent = (extent.Z * elementSize) / newElementSize;
            Debug.Assert(zExtent > 0, "OutOfBounds cast");
            return new Index3(extent.X, extent.Y, zExtent);
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given index is equal to the current index.
        /// </summary>
        /// <param name="other">The other index.</param>
        /// <returns>True, iff the given index is equal to the current index.</returns>
        public bool Equals(Index3 other)
        {
            return this == other;
        }

        #endregion

        #region IComparable

        /// <summary cref="IComparable{T}.CompareTo(T)"/>
        public int CompareTo(Index3 other)
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
            if (obj is Index3)
                return Equals((Index3)obj);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this index.
        /// </summary>
        /// <returns>The hash code of this index.</returns>
        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of this index.
        /// </summary>
        /// <returns>The string representation of this index.</returns>
        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }

        #endregion

        #region Operators

        /// <summary>
        /// Adds two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The added index.</returns>
        public static Index3 Add(Index3 first, Index3 second)
        {
            return first + second;
        }

        /// <summary>
        /// Adds two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The added index.</returns>
        public static Index3 operator +(Index3 first, Index3 second)
        {
            return new Index3(first.X + second.X, first.Y + second.Y, first.Z + second.Z);
        }

        /// <summary>
        /// Subtracts two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The subtracted index.</returns>
        public static Index3 Subtract(Index3 first, Index3 second)
        {
            return first - second;
        }

        /// <summary>
        /// Subtracts two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The subtracted index.</returns>
        public static Index3 operator -(Index3 first, Index3 second)
        {
            return new Index3(first.X - second.X, first.Y - second.Y, first.Z - second.Z);
        }

        /// <summary>
        /// Multiplies an index with a scalar (component wise).
        /// </summary>
        /// <param name="first">The scalar value.</param>
        /// <param name="second">The index.</param>
        /// <returns>The multiplied index.</returns>
        public static Index3 operator *(int first, Index3 second)
        {
            return new Index3(first) * second;
        }

        /// <summary>
        /// Multiplies an index with a scalar (component wise).
        /// </summary>
        /// <param name="first">The index.</param>
        /// <param name="second">The scalar value.</param>
        /// <returns>The multiplied index.</returns>
        public static Index3 operator *(Index3 first, int second)
        {
            return first * new Index3(second);
        }

        /// <summary>
        /// Multiplies two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The multiplied index.</returns>
        public static Index3 Multiply(Index3 first, Index3 second)
        {
            return first * second;
        }

        /// <summary>
        /// Multiplies two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The multiplied index.</returns>
        public static Index3 operator *(Index3 first, Index3 second)
        {
            return new Index3(first.X * second.X, first.Y * second.Y, first.Z * second.Z);
        }

        /// <summary>
        /// Divides an index with a scalar (component wise).
        /// </summary>
        /// <param name="first">The scalar value.</param>
        /// <param name="second">The index.</param>
        /// <returns>The divided index.</returns>
        public static Index3 operator /(int first, Index3 second)
        {
            return new Index3(first) / second;
        }

        /// <summary>
        /// Divides an index with a scalar (component wise).
        /// </summary>
        /// <param name="first">The index.</param>
        /// <param name="second">The scalar value.</param>
        /// <returns>The divided index.</returns>
        public static Index3 operator /(Index3 first, int second)
        {
            return first / new Index3(second);
        }

        /// <summary>
        /// Divides two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The divided index.</returns>
        public static Index3 Divide(Index3 first, Index3 second)
        {
            return first / second;
        }

        /// <summary>
        /// Divides two indices (component wise).
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>The divided index.</returns>
        public static Index3 operator /(Index3 first, Index3 second)
        {
            return new Index3(first.X / second.X, first.Y / second.Y, first.Z / second.Z);
        }

        /// <summary>
        /// Returns true iff the first and second index are the same.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first and second index are the same.</returns>
        public static bool operator ==(Index3 first, Index3 second)
        {
            return first.X == second.X && first.Y == second.Y && first.Z == second.Z;
        }

        /// <summary>
        /// Returns true iff the first and second index are not the same.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first and second index are not the same.</returns>
        public static bool operator !=(Index3 first, Index3 second)
        {
            return first.X != second.X || first.Y != second.Y || first.Z != second.Z;
        }

        /// <summary>
        /// Returns true iff the first index is smaller than the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first index is smaller than the second index.</returns>
        public static bool operator <(Index3 first, Index3 second)
        {
            return first.X < second.X && first.Y < second.Y && first.Z < second.Z;
        }

        /// <summary>
        /// Returns true iff the first index is smaller than or equal to the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first index is smaller than or equal the second index.</returns>
        public static bool operator <=(Index3 first, Index3 second)
        {
            return first.X <= second.X && first.Y <= second.Y && first.Z <= second.Z;
        }

        /// <summary>
        /// Returns true iff the first index is greater than the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first index is greater than the second index.</returns>
        public static bool operator >(Index3 first, Index3 second)
        {
            return first.X > second.X && first.Y > second.Y && first.Z > second.Z;
        }

        /// <summary>
        /// Returns true iff the first index is greater than or equal to the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first index is greater or equal to the second index.</returns>
        public static bool operator >=(Index3 first, Index3 second)
        {
            return first.X >= second.X && first.Y >= second.Y && first.Z >= second.Z;
        }

        #endregion
    }
}
