// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Log.cs
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
    /// Computes log_newBase(value) to base newBase.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="newBase">The desired base.</param>
    /// <returns>log_newBase(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Log(double value, double newBase) => Math.Log(value, newBase);

    /// <summary>
    /// Computes log_newBase(value) to base newBase.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="newBase">The desired base.</param>
    /// <returns>log_newBase(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Log(float value, float newBase) => MathF.Log(value, newBase);

    /// <summary>
    /// Computes log(value) to base e.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>log(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Log(double value) => Math.Log(value);

    /// <summary>
    /// Computes log(value) to base e.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>log(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Log(float value) => MathF.Log(value);

    /// <summary>
    /// Computes log10(value) to base 10.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>log10(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Log10(double value) => Math.Log10(value);

    /// <summary>
    /// Computes log10(value) to base 10.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>log10(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Log10(float value) => MathF.Log10(value);

    /// <summary>
    /// Computes log2(value) to base 2.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>log2(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Log2(double value) => Math.Log2(value);

    /// <summary>
    /// Computes log2(value) to base 2.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>log2(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Log2(float value) => MathF.Log2(value);
}
