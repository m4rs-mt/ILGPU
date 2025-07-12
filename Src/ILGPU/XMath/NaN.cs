// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: NaN.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using System.Runtime.CompilerServices;

namespace ILGPU;

partial class XMath
{
    /// <summary>
    /// Returns true iff the given value is NaN.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, iff the given value is NaN.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNaN(double value) => double.IsNaN(value);

    /// <summary>
    /// Returns true iff the given value is NaN.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, iff the given value is NaN.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNaN(float value) => float.IsNaN(value);

    /// <summary>
    /// Returns true iff the given value is NaN.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, iff the given value is NaN.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNaN(Half value) => HalfExtensions.IsNaN(value);

    /// <summary>
    /// Returns true iff the given value is infinity.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, iff the given value is infinity.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInfinity(double value) => double.IsInfinity(value);

    /// <summary>
    /// Returns true iff the given value is infinity.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, iff the given value is infinity.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInfinity(float value) => float.IsInfinity(value);

    /// <summary>
    /// Returns true iff the given value is infinity.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, iff the given value is infinity.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInfinity(Half value) => HalfExtensions.IsInfinity(value);

    /// <summary>
    /// Returns true iff the given value is finite.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, iff the given value is finite.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinite(double value) => double.IsFinite(value);

    /// <summary>
    /// Returns true iff the given value is finite.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, iff the given value is finite.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinite(float value) => float.IsFinite(value);

    /// <summary>
    /// Returns true iff the given value is finite.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True, iff the given value is finite.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinite(Half value) => HalfExtensions.IsFinite(value);
}
