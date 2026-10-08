// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPURuntimeView.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Runtime.CPU;

/// <summary>
/// A lightweight, unmanaged view type for CPU kernel execution.
/// Wraps a native <c>T*</c> pointer and length — no managed references,
/// no GC tracking overhead.
/// </summary>
/// <remarks>
/// Used by ILGPUC-generated CPU kernels in place of <see cref="ArrayView{T}"/>
/// to avoid managed reference overhead in hot kernel code. Constructed from
/// <see cref="ArrayView{T}"/> at the kernel launch boundary.
/// </remarks>
/// <typeparam name="T">The unmanaged element type.</typeparam>
/// <remarks>
/// Constructs a runtime view from a raw pointer and length.
/// </remarks>
/// <param name="ptr">The base pointer.</param>
/// <param name="length">The number of elements.</param>
[StructLayout(LayoutKind.Sequential)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public unsafe readonly struct CPURuntimeView<T>(T* ptr, long length)
    where T : unmanaged
{
    /// <summary>
    /// The base pointer to the element data.
    /// </summary>
    public readonly T* Ptr = ptr;

    /// <summary>
    /// The number of elements in this view.
    /// </summary>
    public readonly long Length = length;

    /// <summary>
    /// Pointer arithmetic: returns a pointer offset by
    /// <paramref name="offset"/> elements from the base.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* operator +(CPURuntimeView<T> view, int offset) =>
        view.Ptr + offset;

    /// <summary>
    /// Pointer arithmetic: returns a pointer offset by
    /// <paramref name="offset"/> elements from the base.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* operator +(CPURuntimeView<T> view, long offset) =>
        view.Ptr + offset;

    /// <summary>
    /// Constructs a runtime view from an <see cref="ArrayView{T}"/>,
    /// extracting the native pointer and length.
    /// </summary>
    /// <param name="source">The source array view.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CPURuntimeView(ArrayView<T> source)
        : this(
              source.IsValid
              ? (T*)Unsafe.AsPointer(ref source.LoadEffectiveAddress())
              : null,
              source.Length)
    { }

    /// <summary>
    /// Constructs a runtime view from any <see cref="ArrayView{TSource}"/>
    /// whose element type may differ from <typeparamref name="T"/> only in
    /// signedness (e.g., <c>ArrayView&lt;uint&gt;</c> for
    /// <c>CPURuntimeView&lt;int&gt;</c>). The memory layout is identical.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CPURuntimeView<T> FromView<TSource>(
        ArrayView<TSource> source) where TSource : unmanaged =>
        new(
            source.IsValid
            ? (T*)Unsafe.AsPointer(ref source.LoadEffectiveAddress())
            : null,
            source.Length);

    /// <summary>
    /// Creates a sub-view starting at <paramref name="offset"/> with
    /// <paramref name="length"/> elements.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly CPURuntimeView<T> SubView(long offset, long length) =>
        new(Ptr + offset, length);

    /// <summary>
    /// Accesses the element at the given index.
    /// </summary>
    /// <param name="index">The element index.</param>
    /// <returns>A reference to the element.</returns>
    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Ptr[index];
    }

    /// <summary>
    /// Accesses the element at the given index.
    /// </summary>
    /// <param name="index">The element index.</param>
    /// <returns>A reference to the element.</returns>
    public ref T this[long index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Ptr[index];
    }

    #region IO Operations

    /// <summary>
    /// Gathers values from this view using per-lane int indices.
    /// </summary>
    [MethodImpl(
        MethodImplOptions.AggressiveOptimization |
        MethodImplOptions.AggressiveInlining)]
    public readonly void GatherLoad(
        Span<T> target,
        ReadOnlySpan<int> indices,
        ReadOnlySpan<bool> mask)
    {
        for (int i = 0; i < indices.Length; i++)
            target[i] = mask[i] ? Ptr[indices[i]] : default;
    }

    /// <summary>
    /// Gathers values from this view using per-lane long indices.
    /// Preserves 64-bit global memory addressability.
    /// </summary>
    [MethodImpl(
        MethodImplOptions.AggressiveOptimization |
        MethodImplOptions.AggressiveInlining)]
    public readonly void GatherLoad(
        Span<T> target,
        ReadOnlySpan<long> indices,
        ReadOnlySpan<bool> mask)
    {
        for (int i = 0; i < indices.Length; i++)
            target[i] = mask[i] ? Ptr[indices[i]] : default;
    }

    /// <summary>
    /// Scatters values to this view using per-lane int indices.
    /// </summary>
    [MethodImpl(
        MethodImplOptions.AggressiveOptimization |
        MethodImplOptions.AggressiveInlining)]
    public readonly void ScatterStore(
        ReadOnlySpan<int> indices,
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
    {
        for (int i = 0; i < indices.Length; i++)
        {
            if (mask[i])
                Ptr[indices[i]] = values[i];
        }
    }

    /// <summary>
    /// Scatters values to this view using per-lane long indices.
    /// Preserves 64-bit global memory addressability.
    /// </summary>
    [MethodImpl(
        MethodImplOptions.AggressiveOptimization |
        MethodImplOptions.AggressiveInlining)]
    public readonly void ScatterStore(
        ReadOnlySpan<long> indices,
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
    {
        for (int i = 0; i < indices.Length; i++)
        {
            if (mask[i])
                Ptr[indices[i]] = values[i];
        }
    }

    /// <summary>
    /// Fills indexed positions with a single scalar value (scatter-fill).
    /// Used when the store value is uniform across all SIMD lanes but the
    /// indices are per-lane.
    /// </summary>
    [MethodImpl(
        MethodImplOptions.AggressiveOptimization |
        MethodImplOptions.AggressiveInlining)]
    public readonly void ScatterFill(
        ReadOnlySpan<int> indices, T value, ReadOnlySpan<bool> mask)
    {
        for (int i = 0; i < indices.Length; i++)
        {
            if (mask[i])
                Ptr[indices[i]] = value;
        }
    }

    /// <summary>
    /// Fills a single value at scatter positions with long indices and masking.
    /// </summary>
    [MethodImpl(
        MethodImplOptions.AggressiveOptimization |
        MethodImplOptions.AggressiveInlining)]
    public readonly void ScatterFill(
        ReadOnlySpan<long> indices, T value, ReadOnlySpan<bool> mask)
    {
        for (int i = 0; i < indices.Length; i++)
        {
            if (mask[i])
                Ptr[indices[i]] = value;
        }
    }

    /// <summary>
    /// Loads values contiguously from this view with masking.
    /// Inactive lanes receive <c>default(T)</c>.
    /// </summary>
    [MethodImpl(
        MethodImplOptions.AggressiveOptimization |
        MethodImplOptions.AggressiveInlining)]
    public readonly void MaskedLoad(
        Span<T> target,
        ReadOnlySpan<bool> mask)
    {
        for (int i = 0; i < target.Length; i++)
            target[i] = mask[i] ? Ptr[i] : default;
    }

    /// <summary>
    /// Stores values contiguously to this view with masking.
    /// Only active lanes are written.
    /// </summary>
    [MethodImpl(
        MethodImplOptions.AggressiveOptimization |
        MethodImplOptions.AggressiveInlining)]
    public readonly void MaskedStore(
        ReadOnlySpan<T> values,
        ReadOnlySpan<bool> mask)
    {
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i])
                Ptr[i] = values[i];
        }
    }

    #endregion

    #region Conversions

    /// <summary>
    /// Implicitly converts to a writable <see cref="Span{T}"/>.
    /// For local compute operations only — not for global IO addressing.
    /// </summary>
    /// <param name="view">The view to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Span<T>(CPURuntimeView<T> view) =>
        new(view.Ptr, (int)view.Length);

    /// <summary>
    /// Implicitly converts to a read-only <see cref="ReadOnlySpan{T}"/>.
    /// For local compute operations only — not for global IO addressing.
    /// </summary>
    /// <param name="view">The view to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(CPURuntimeView<T> view) =>
        new(view.Ptr, (int)view.Length);

    #endregion
}
