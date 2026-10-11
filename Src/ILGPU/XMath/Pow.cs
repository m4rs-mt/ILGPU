// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Pow.cs
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
    /// Computes basis^exp.
    /// </summary>
    /// <param name="base">The basis.</param>
    /// <param name="exp">The exponent.</param>
    /// <returns>pow(basis, exp).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Pow(double @base, double exp) => Math.Pow(@base, exp);

    /// <summary>
    /// Computes basis^exp.
    /// </summary>
    /// <param name="base">The basis.</param>
    /// <param name="exp">The exponent.</param>
    /// <returns>pow(basis, exp).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Pow(float @base, float exp) => MathF.Pow(@base, exp);

    /// <summary>
    /// Computes basis^exp.
    /// </summary>
    /// <param name="base">The basis.</param>
    /// <param name="exp">The exponent.</param>
    /// <returns>pow(basis, exp).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half Pow(Half @base, Half exp) => (Half)Pow((float)@base, (float)exp);

    /// <summary>
    /// Computes exp(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>exp(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Exp(double value) => Math.Exp(value);

    /// <summary>
    /// Computes exp(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>exp(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Exp(float value) => MathF.Exp(value);

    /// <summary>
    /// Computes exp(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>exp(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half Exp(Half value) => (Half)Exp((float)value);

    /// <summary>
    /// Computes 2^value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>2^value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Exp2(double value) => Math.Pow(2.0, value);

    /// <summary>
    /// Computes 2^value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>2^value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Exp2(float value) => MathF.Pow(2.0f, value);

    /// <summary>
    /// Computes 2^value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>2^value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half Exp2(Half value) => (Half)Exp2((float)value);
}
