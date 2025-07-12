// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Trig.cs
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
    /// Computes sin(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>sin(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sin(double value) => Math.Sin(value);

    /// <summary>
    /// Computes sin(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>sin(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sin(float value) => MathF.Sin(value);

    /// <summary>
    /// Computes asin(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>asin(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Asin(double value) => Math.Asin(value);

    /// <summary>
    /// Computes asin(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>asin(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Asin(float value) => MathF.Asin(value);

    /// <summary>
    /// Computes asinh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>asinh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Asinh(float value) => MathF.Asinh(value);

    /// <summary>
    /// Computes asinh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>asinh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Asinh(double value) => Math.Asinh(value);

    /// <summary>
    /// Computes sinh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>sinh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sinh(double value) => Math.Sinh(value);

    /// <summary>
    /// Computes sinh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>sinh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sinh(float value) => MathF.Sinh(value);

    /// <summary>
    /// Computes cos(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>cos(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Cos(double value) => Math.Cos(value);

    /// <summary>
    /// Computes cos(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>cos(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cos(float value) => MathF.Cos(value);

    /// <summary>
    /// Computes cosh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>cosh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Cosh(double value) => Math.Cosh(value);

    /// <summary>
    /// Computes cosh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>cosh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cosh(float value) => MathF.Cosh(value);

    /// <summary>
    /// Computes acos(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>acos(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Acos(double value) => Math.Acos(value);

    /// <summary>
    /// Computes acos(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>acos(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Acos(float value) => MathF.Acos(value);

    /// <summary>
    /// Computes acosh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>acosh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Acosh(double value) => Math.Acosh(value);

    /// <summary>
    /// Computes acosh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>acosh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Acosh(float value) => MathF.Acosh(value);

    /// <summary>
    /// Computes tan(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>tan(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Tan(double value) => Math.Tan(value);

    /// <summary>
    /// Computes tan(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>tan(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Tan(float value) => MathF.Tan(value);

    /// <summary>
    /// Computes tanh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>tanh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Tanh(double value) => Math.Tanh(value);

    /// <summary>
    /// Computes tanh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>tanh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Tanh(float value) => MathF.Tanh(value);

    /// <summary>
    /// Computes tanh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>tanh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half Tanh(Half value) => HalfExtensions.TanhFP32(value);

    /// <summary>
    /// Computes atan(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>atan(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Atan(double value) => Math.Atan(value);

    /// <summary>
    /// Computes atan(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>atan(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Atan(float value) => MathF.Atan(value);

    /// <summary>
    /// Computes atanh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>atanh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Atanh(double value) => Math.Atanh(value);

    /// <summary>
    /// Computes atanh(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>atanh(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Atanh(float value) => MathF.Atanh(value);

    /// <summary>
    /// Computes atan2(y, x).
    /// </summary>
    /// <param name="y">The y value in radians.</param>
    /// <param name="x">The x value in radians.</param>
    /// <returns>atan2(y, x).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Atan2(double y, double x) => Math.Atan2(y, x);

    /// <summary>
    /// Computes atan2(y, x).
    /// </summary>
    /// <param name="y">The y value in radians.</param>
    /// <param name="x">The x value in radians.</param>
    /// <returns>atan2(y, x).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Atan2(float y, float x) => MathF.Atan2(y, x);

    /// <summary>
    /// Computes sin(value) and cos(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <param name="sin">The result of sin(value).</param>
    /// <param name="cos">The result of cos(value).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SinCos(double value, out double sin, out double cos)
    {
        sin = Sin(value);
        cos = Cos(value);
    }

    /// <summary>
    /// Computes sin(value) and cos(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>The sin/cos result of the given value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (double, double) SinCos(double value) => (Sin(value), Cos(value));

    /// <summary>
    /// Computes sin(value) and cos(value).
    /// </summary>
    /// <param name="value">The value in radians.</param>
    /// <returns>The sin/cos result of the given value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (float, float) SinCos(float value) => (Sin(value), Cos(value));
}
