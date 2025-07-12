// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IIndex.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU;

/// <summary>
/// Represents the type of index.
/// </summary>
public enum IndexType
{
    /// <summary>
    /// Represents no compatible index type.
    /// </summary>
    None = 0,

    /// <summary>
    /// Represents a 1D index.
    /// </summary>
    Index1D = 1,

    /// <summary>
    /// Represents a 2D index.
    /// </summary>
    Index2D = 2,

    /// <summary>
    /// Represents a 3D index.
    /// </summary>
    Index3D = 3,

    /// <summary>
    /// Represents a 1D index.
    /// </summary>
    LongIndex1D = 4,

    /// <summary>
    /// Represents a 2D index.
    /// </summary>
    LongIndex2D = 5,

    /// <summary>
    /// Represents a 3D index.
    /// </summary>
    LongIndex3D = 6,
}

/// <summary>
/// An internal attribute to specify the index type of a custom structure.
/// </summary>
/// <remarks>
/// Constructs a new attribute instance.
/// </remarks>
/// <param name="indexType">The index type.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
sealed class IndexTypeAttribute(IndexType indexType) : Attribute
{
    /// <summary>
    /// Returns the associated index type.
    /// </summary>
    public IndexType IndexType { get; } = indexType;
}

/// <summary>
/// Contains utility functions for handling index types.
/// </summary>
public static class IndexTypeExtensions
{
    /// <summary>
    /// Asserts that the given long range can be accessed using a 32-bit integer.
    /// </summary>
    /// <param name="index">The long value range.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AssertIntIndex(long index) =>
        Trace.Assert(
            Bitwise.And(index >= int.MinValue, index <= int.MaxValue),
            "32-bit index expected");

    /// <summary>
    /// Asserts that the given long range can be expressed by using a 32-bit integer.
    /// </summary>
    /// <param name="range">The long value range.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AssertIntIndexRange(long range) =>
        Trace.Assert(
            Bitwise.And(range >= int.MinValue, range <= int.MaxValue),
            "32-bit index out of range");

    /// <summary>
    /// Returns a 32-bit integer size of the given index.
    /// </summary>
    /// <typeparam name="TIndex">The index type.</typeparam>
    /// <param name="index">The index type instance.</param>
    /// <returns>The 32-bit integer size of the given index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIntSize<TIndex>(this TIndex index)
        where TIndex : struct, IIndex
    {
        long size = index.Size;
        AssertIntIndexRange(size);
        return (int)size;
    }

    /// <summary>
    /// Returns the extent of an one-dimensional array.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The source array.</param>
    /// <returns>The extent of an one-dimensional array.</returns>
    public static Index1D GetExtent<T>(this T[] array)
    {
        Debug.Assert(array != null, "Invalid array");
        return array.Length;
    }

    /// <summary>
    /// Returns the extent of a two-dimensional array.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The source array.</param>
    /// <returns>The extent of a two-dimensional array.</returns>
    [SuppressMessage(
        "Performance",
        "CA1814:Prefer jagged arrays over multidimensional",
        Target = "array")]
    public static Index2D GetExtent<T>(this T[,] array)
    {
        Debug.Assert(array != null, "Invalid array");
        return new Index2D(
            array.GetLength(0),
            array.GetLength(1));
    }

    /// <summary>
    /// Returns the extent of a three-dimensional array.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The source array.</param>
    /// <returns>The extent of a three-dimensional array.</returns>
    [SuppressMessage(
        "Performance",
        "CA1814:Prefer jagged arrays over multidimensional",
        Target = "array")]
    public static Index3D GetExtent<T>(this T[,,] array)
    {
        Debug.Assert(array != null, "Invalid array");
        return new Index3D(
            array.GetLength(0),
            array.GetLength(1),
            array.GetLength(2));
    }

    /// <summary>
    /// Returns the value at the given index.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The source array.</param>
    /// <param name="index">The element index.</param>
    /// <returns>The value at the given index.</returns>
    public static T GetValue<T>(this T[] array, Index1D index) =>
        array[index];

    /// <summary>
    /// Returns the value at the given index.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The source array.</param>
    /// <param name="index">The element index.</param>
    /// <returns>The value at the given index.</returns>
    [SuppressMessage(
        "Performance",
        "CA1814:Prefer jagged arrays over multidimensional",
        Target = "array")]
    public static T GetValue<T>(this T[,] array, Index2D index) =>
        array[index.X, index.Y];

    /// <summary>
    /// Returns the value at the given index.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The source array.</param>
    /// <param name="index">The element index.</param>
    /// <returns>The value at the given index.</returns>
    [SuppressMessage(
        "Performance",
        "CA1814:Prefer jagged arrays over multidimensional",
        Target = "array")]
    public static T GetValue<T>(this T[,,] array, Index3D index) =>
        array[index.X, index.Y, index.Z];

    /// <summary>
    /// Sets the value at the given index to the given one.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The target array.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="index">The element index.</param>
    public static void SetValue<T>(this T[] array, T value, Index1D index) =>
        array[index] = value;

    /// <summary>
    /// Sets the value at the given index to the given one.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The target array.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="index">The element index.</param>
    [SuppressMessage(
        "Performance",
        "CA1814:Prefer jagged arrays over multidimensional",
        Target = "array")]
    public static void SetValue<T>(this T[,] array, T value, Index2D index) =>
        array[index.X, index.Y] = value;

    /// <summary>
    /// Sets the value at the given index to the given one.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The target array.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="index">The element index.</param>
    [SuppressMessage(
        "Performance",
        "CA1814:Prefer jagged arrays over multidimensional",
        Target = "array")]
    public static void SetValue<T>(this T[,,] array, T value, Index3D index) =>
        array[index.X, index.Y, index.Z] = value;
}

/// <summary>
/// Represents a basic index type.
/// </summary>
public interface IIndex
{
    /// <summary>
    /// Returns the size represented by this index (e.g. x, x * y or x * y * z).
    /// </summary>
    long Size { get; }
}

/// <summary>
/// Represents a 32-bit index.
/// </summary>
public interface IIntIndex : IIndex
{
    /// <summary>
    /// Returns the size represented by this index (e.g. x, x * y or x * y * z).
    /// </summary>
    new int Size { get; }
}

/// <summary>
/// Represents an intrinsic index type.
/// </summary>
public interface IIntrinsicIndex : IIndex
{
    /// <summary>
    /// Returns the current index type.
    /// </summary>
    IndexType IndexType { get; }
}

/// <summary>
/// Represents a generic index type.
/// </summary>
/// <typeparam name="TIndex">The type of the generic index.</typeparam>
public interface IGenericIndex<TIndex> :
    IIndex,
    IEquatable<TIndex>
    where TIndex : struct, IIndex
{
    /// <summary>
    /// Returns true if the current index is greater than or equal to 0 and
    /// is less than the given dimension.
    /// </summary>
    /// <param name="dimension">The dimension bounds.</param>
    /// <returns>True if the current index is inside the given bounds.</returns>
    bool InBounds(TIndex dimension);

    /// <summary>
    /// Returns true if the current index is greater than or equal to 0 and
    /// is less than or equal to the given dimension.
    /// </summary>
    /// <param name="dimension">The dimension bounds.</param>
    /// <returns>True if the current index is inside the given bounds.</returns>
    bool InBoundsInclusive(TIndex dimension);

    /// <summary>
    /// Computes this + right-hand side.
    /// </summary>
    /// <param name="rhs">The right-hand side of the addition.</param>
    /// <returns>The added index.</returns>
    TIndex Add(TIndex rhs);

    /// <summary>
    /// Computes this - right-hand side.
    /// </summary>
    /// <param name="rhs">The right-hand side of the subtraction.</param>
    /// <returns>The subtracted index.</returns>
    TIndex Subtract(TIndex rhs);
}

/// <summary>
/// An integer register.
/// </summary>
/// <typeparam name="TIndex">The integer type.</typeparam>
/// <typeparam name="TLongIndex">The long integer type.</typeparam>
public interface IIntIndex<TIndex, TLongIndex> :
    IIntIndex,
    IIntrinsicIndex,
    IGenericIndex<TIndex>
    where TIndex : struct, IIntIndex<TIndex, TLongIndex>
    where TLongIndex : struct, ILongIndex<TLongIndex, TIndex>
{
    /// <summary>
    /// Converts this index to a long integer index.
    /// </summary>
    /// <returns>The resulting long integer representation.</returns>
    TLongIndex ToLongIndex();
}

/// <summary>
/// A long integer register.
/// </summary>
/// <typeparam name="TLongIndex">The long integer type.</typeparam>
/// <typeparam name="TIndex">The integer type.</typeparam>
public interface ILongIndex<TLongIndex, TIndex> :
    IIntrinsicIndex,
    IGenericIndex<TLongIndex>
    where TLongIndex : struct, ILongIndex<TLongIndex, TIndex>
    where TIndex : struct, IIntIndex<TIndex, TLongIndex>
{
    /// <summary>
    /// Converts this index to an integer index.
    /// </summary>
    /// <returns>The resulting integer representation.</returns>
    TIndex ToIntIndex();
}
