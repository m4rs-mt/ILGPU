// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright(c) 2016-2018 ILGPU Lightning Project
//                                    www.ilgpu.net
//
// File: ArrayExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Represents extension methods for arrays.
    /// </summary>
    public static class ArrayExtensions
    {
        /// <summary>
        /// Returns the extent of an one-dimensional array.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="array">The source array.</param>
        /// <returns>The extent of an one-dimensional array.</returns>
        public static Index1 GetExtent<T>(this T[] array)
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
        [SuppressMessage("Performance", "CA1814:Prefer jagged arrays over multidimensional", Target = "array")]
        public static Index2 GetExtent<T>(this T[,] array)
        {
            Debug.Assert(array != null, "Invalid array");
            return new Index2(
                array.GetLength(0),
                array.GetLength(1));
        }

        /// <summary>
        /// Returns the extent of a three-dimensional array.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="array">The source array.</param>
        /// <returns>The extent of a three-dimensional array.</returns>
        [SuppressMessage("Performance", "CA1814:Prefer jagged arrays over multidimensional", Target = "array")]
        public static Index3 GetExtent<T>(this T[,,] array)
        {
            Debug.Assert(array != null, "Invalid array");
            return new Index3(
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
        public static T GetValue<T>(this T[] array, Index1 index) =>
            array[index];

        /// <summary>
        /// Returns the value at the given index.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="array">The source array.</param>
        /// <param name="index">The element index.</param>
        /// <returns>The value at the given index.</returns>
        [SuppressMessage("Performance", "CA1814:Prefer jagged arrays over multidimensional", Target = "array")]
        public static T GetValue<T>(this T[,] array, Index2 index) =>
            array[index.X, index.Y];

        /// <summary>
        /// Returns the value at the given index.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="array">The source array.</param>
        /// <param name="index">The element index.</param>
        /// <returns>The value at the given index.</returns>
        [SuppressMessage("Performance", "CA1814:Prefer jagged arrays over multidimensional", Target = "array")]
        public static T GetValue<T>(this T[,,] array, Index3 index) =>
            array[index.X, index.Y, index.Z];

        /// <summary>
        /// Sets the value at the given index to the given one.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="array">The target array.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="index">The element index.</param>
        public static void SetValue<T>(this T[] array, T value, Index1 index) =>
            array[index] = value;

        /// <summary>
        /// Sets the value at the given index to the given one.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="array">The target array.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="index">The element index.</param>
        [SuppressMessage("Performance", "CA1814:Prefer jagged arrays over multidimensional", Target = "array")]
        public static void SetValue<T>(this T[,] array, T value, Index2 index) =>
            array[index.X, index.Y] = value;

        /// <summary>
        /// Sets the value at the given index to the given one.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="array">The target array.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="index">The element index.</param>
        [SuppressMessage("Performance", "CA1814:Prefer jagged arrays over multidimensional", Target = "array")]
        public static void SetValue<T>(this T[,,] array, T value, Index3 index) =>
            array[index.X, index.Y, index.Z] = value;
    }
}
