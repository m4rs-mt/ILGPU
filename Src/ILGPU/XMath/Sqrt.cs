// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Sqrt.cs
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
    /// Computes sqrt(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>sqrt(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sqrt(double value) => Math.Sqrt(value);

    /// <summary>
    /// Computes sqrt(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>sqrt(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sqrt(float value) => MathF.Sqrt(value);

    /// <summary>
    /// Computes 1/sqrt(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>1/sqrt(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Rsqrt(double value) => Rcp(Sqrt(value));

    /// <summary>
    /// Computes 1/sqrt(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>1/sqrt(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Rsqrt(float value) => Rcp(Sqrt(value));

    /// <summary>
    /// Computes cbrt(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>cbrt(value).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Cbrt(double value) => Pow(value, 1.0 / 3.0);

    /// <summary>
    /// Computes cbrt(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>cbrt(value).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cbrt(float value) => Pow(value, 1.0f / 3.0f);
}
