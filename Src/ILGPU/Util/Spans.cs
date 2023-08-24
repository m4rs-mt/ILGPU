// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Spans.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Util
{
    /// <summary>
    /// Extension methods for spans.
    /// </summary>
    public static class Spans
    {
        /// <summary>
        /// Returns a reference to the specified element of the given span.
        /// </summary>
        /// <typeparam name="T">The span element type.</typeparam>
        /// <param name="span">The input span.</param>
        /// <param name="index">The element index.</param>
        /// <returns>A reference to the specified element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T GetItemRef<T>(this Span<T> span, int index) =>
            ref Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint)index);

        /// <summary>
        /// Returns a reference to the specified element of the given span.
        /// </summary>
        /// <typeparam name="T">The span element type.</typeparam>
        /// <param name="span">The input span.</param>
        /// <param name="index">The element index.</param>
        /// <returns>A readonly reference to the specified element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly T GetItemRef<T>(
            this ReadOnlySpan<T> span,
            int index) =>
            ref Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint)index);

        /// <summary>
        /// Casts the given span into a span of another type (unsafe).
        /// <typeparam name="TIn">The input span element type.</typeparam>
        /// <typeparam name="TOut">The output span element type.</typeparam>
        /// </summary>
        /// <param name="span">The span to cast.</param>
        /// <returns>The resulting span.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<TOut> CastUnsafe<TIn, TOut>(this Span<TIn> span)
            where TIn : struct
            where TOut : struct =>
            MemoryMarshal.Cast<TIn, TOut>(span);

        /// <summary>
        /// Casts the given span into a span of another type (unsafe).
        /// </summary>
        /// <typeparam name="TIn">The input span element type.</typeparam>
        /// <typeparam name="TOut">The output span element type.</typeparam>
        /// <param name="span">The span to cast.</param>
        /// <returns>The resulting span.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<TOut> CastUnsafe<TIn, TOut>(
            this ReadOnlySpan<TIn> span)
            where TIn : struct
            where TOut : struct =>
            MemoryMarshal.Cast<TIn, TOut>(span);
    }
}