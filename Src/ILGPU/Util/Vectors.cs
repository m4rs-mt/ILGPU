// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vectors.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Util
{
    /// <summary>
    /// Represents performance-optimized vector helpers operating on spans.
    /// </summary>
    public static class Vectors
    {
        /// <summary>
        /// Loads a vector (unsafe) from the given span while assuming proper alignment.
        /// </summary>
        /// <typeparam name="T">The vector element type.</typeparam>
        /// <param name="source">The source span to load from.</param>
        /// <returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector<T> LoadAlignedVectorUnsafe<T>(
            this ReadOnlySpan<T> source)
            where T : struct
        {
            void* sourcePtr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(source));
            return Unsafe.Read<Vector<T>>(sourcePtr);
        }

        /// <summary>
        /// Loads a vector (unsafe) from the given span while assuming proper alignment.
        /// </summary>
        /// <typeparam name="T">The vector element type.</typeparam>
        /// <param name="source">The source span to load from.</param>
        /// <returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector<T> LoadAlignedVectorUnsafe<T>(this Span<T> source)
            where T : struct =>
            ((ReadOnlySpan<T>)source).LoadAlignedVectorUnsafe();

        /// <summary>
        /// Stores the current vector into the given span while assuming proper alignment.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="value">The vector to store.</param>
        /// <param name="target">The target span to store to.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void StoreAlignedVectorUnsafe<T>(
            this Vector<T> value,
            Span<T> target)
            where T : struct
        {
            void* targetPtr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(target));
            Unsafe.Write(targetPtr, value);
        }
    }
}