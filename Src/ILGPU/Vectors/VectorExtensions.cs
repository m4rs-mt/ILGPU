// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: VectorExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU.Vectors;

/// <summary>
/// Represents extension methods for vectors.
/// </summary>
public static class VectorExtensions
{
    #region Offsets

    /// <summary>
    /// Represents the offset of the x-component of a <see cref="Vector2"/>.
    /// </summary>
    public static readonly int Vector2XOffset =
        Interop.OffsetOf<Vector2>(nameof(Vector2.X));

    /// <summary>
    /// Represents the offset of the y-component of a <see cref="Vector2"/>.
    /// </summary>
    public static readonly int Vector2YOffset =
        Interop.OffsetOf<Vector2>(nameof(Vector2.Y));

    /// <summary>
    /// Represents the offset of the x-component of a <see cref="Vector3"/>.
    /// </summary>
    public static readonly int Vector3XOffset =
        Interop.OffsetOf<Vector3>(nameof(Vector3.X));

    /// <summary>
    /// Represents the offset of the y-component of a <see cref="Vector3"/>.
    /// </summary>
    public static readonly int Vector3YOffset =
        Interop.OffsetOf<Vector3>(nameof(Vector3.Y));

    /// <summary>
    /// Represents the offset of the z-component of a <see cref="Vector3"/>.
    /// </summary>
    public static readonly int Vector3ZOffset =
        Interop.OffsetOf<Vector3>(nameof(Vector3.Z));

    /// <summary>
    /// Represents the offset of the x-component of a <see cref="Vector4"/>.
    /// </summary>
    public static readonly int Vector4XOffset =
        Interop.OffsetOf<Vector4>(nameof(Vector4.X));

    /// <summary>
    /// Represents the offset of the y-component of a <see cref="Vector4"/>.
    /// </summary>
    public static readonly int Vector4YOffset =
        Interop.OffsetOf<Vector4>(nameof(Vector4.Y));

    /// <summary>
    /// Represents the offset of the z-component of a <see cref="Vector4"/>.
    /// </summary>
    public static readonly int Vector4ZOffset =
        Interop.OffsetOf<Vector4>(nameof(Vector4.Z));

    /// <summary>
    /// Represents the offset of the w-component of a <see cref="Vector4"/>.
    /// </summary>
    public static readonly int Vector4WOffset =
        Interop.OffsetOf<Vector4>(nameof(Vector4.W));

    #endregion

    #region Conversions

    /// <summary>
    /// Converts the index to a corresponding <see cref="Vector2"/>.
    /// </summary>
    /// <param name="index">The source index.</param>
    /// <returns>The converted <see cref="Vector2"/>.</returns>
    public static Vector2 ToVector(this Index2D index) => new(index.X, index.Y);

    /// <summary>
    /// Converts the index to a corresponding <see cref="Vector3"/>.
    /// </summary>
    /// <param name="index">The source index.</param>
    /// <returns>The converted <see cref="Vector3"/>.</returns>
    public static Vector3 ToVector(this Index3D index) =>
        new(index.X, index.Y, index.Z);

    /// <summary>
    /// Converts the vector to a corresponding <see cref="Index2D"/>.
    /// </summary>
    /// <param name="vector">The source vector.</param>
    /// <returns>The converted <see cref="Index2D"/>.</returns>
    public static Index2D ToIndex(this Vector2 vector) =>
        new((int)vector.X, (int)vector.Y);

    /// <summary>
    /// Converts the vector to a corresponding <see cref="Index3D"/>.
    /// </summary>
    /// <param name="vector">The source vector.</param>
    /// <returns>The converted <see cref="Index3D"/>.</returns>
    public static Index3D ToIndex(this Vector3 vector) =>
        new((int)vector.X, (int)vector.Y, (int)vector.Z);

    #endregion

    #region Load/Store

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

    #endregion
}
