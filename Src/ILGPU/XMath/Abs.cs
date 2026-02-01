// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Abs.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU;

partial class XMath
{
    /// <summary>
    /// Computes |value|.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>|value|.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Abs(double value) => Math.Abs(value);

    /// <summary>
    /// Computes |value|.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>|value|.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Abs(float value) => Math.Abs(value);

    /// <summary>
    /// Computes |value|.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>|value|.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half Abs(Half value) => Half.Abs(value);

    /// <summary>
    /// Computes |value|.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>|value|.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte Abs(sbyte value) => (sbyte)Abs((int)value);

    /// <summary>
    /// Computes |value|.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>|value|.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short Abs(short value) => (short)Abs((int)value);

    /// <summary>
    /// Computes |value|.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>|value|.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Abs(int value) => Math.Abs(value);

    /// <summary>
    /// Computes |value|.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>|value|.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Abs(long value) => Math.Abs(value);
}
