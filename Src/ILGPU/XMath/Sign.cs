// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Sign.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU;

partial class XMath
{
    /// <summary>
    /// Computes the sign of the provided value.
    /// Sign will return 0 for NaN, Infinity or 0 values.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>-1 for negative value, 1 for positive values, and 0 for
    /// 0, NaN or Infinity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sign(sbyte value) =>
        Utilities.Select(value < 0, -1, Utilities.Select(value > 0, 1, 0));

    /// <summary>
    /// Computes the sign of the provided value.
    /// Sign will return 0 for NaN, Infinity or 0 values.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>-1 for negative value, 1 for positive values, and 0 for
    /// 0, NaN or Infinity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sign(short value) =>
        Utilities.Select(value < 0, -1, Utilities.Select(value > 0, 1, 0));

    /// <summary>
    /// Computes the sign of the provided value.
    /// Sign will return 0 for NaN, Infinity or 0 values.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>-1 for negative value, 1 for positive values, and 0 for
    /// 0, NaN or Infinity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sign(int value) =>
        Utilities.Select(value < 0, -1, Utilities.Select(value > 0, 1, 0));

    /// <summary>
    /// Computes the sign of the provided value.
    /// Sign will return 0 for NaN, Infinity or 0 values.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>-1 for negative value, 1 for positive values, and 0 for
    /// 0, NaN or Infinity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sign(long value) =>
        Utilities.Select(value < 0, -1, Utilities.Select(value > 0, 1, 0));

    /// <summary>
    /// Computes the sign of the provided value.
    /// Sign will return 0 for NaN, Infinity or 0 values.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>-1 for negative value, 1 for positive values, and 0 for
    /// 0, NaN or Infinity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sign(double value) =>
        Utilities.Select(value < 0.0, -1, Utilities.Select(value > 0.0, 1, 0));

    /// <summary>
    /// Computes the sign of the provided value.
    /// Sign will return 0 for NaN, Infinity or 0 values.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>-1 for negative value, 1 for positive values, and 0 for
    /// 0, NaN or Infinity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sign(float value) =>
        Utilities.Select(value < 0.0f, -1, Utilities.Select(value > 0.0f, 1, 0));

    /// <summary>
    /// Returns a value with the magnitude of x and the sign of y.
    /// </summary>
    /// <param name="x">A number whose magnitude is used in the result.</param>
    /// <param name="y">A number whose sign is the used in the result.</param>
    /// <returns>A value with the magnitude of x and the sign of y.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CopySign(double x, double y) =>
        Math.CopySign(x, y);

    /// <summary>
    /// Returns a value with the magnitude of x and the sign of y.
    /// </summary>
    /// <param name="x">A number whose magnitude is used in the result.</param>
    /// <param name="y">A number whose sign is the used in the result.</param>
    /// <returns>A value with the magnitude of x and the sign of y.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CopySign(float x, float y) =>
        MathF.CopySign(x, y);
}
