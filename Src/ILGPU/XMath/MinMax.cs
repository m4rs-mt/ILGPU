// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MinMax.cs
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
    /// Computes min(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The minimum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Min(double first, double second) =>
        Math.Min(first, second);

    /// <summary>
    /// Computes min(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The minimum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Min(float first, float second) =>
        Math.Min(first, second);

    /// <summary>
    /// Computes min(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The minimum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half Min(Half first, Half second) =>
        HalfExtensions.MinFP32(first, second);

    /// <summary>
    /// Computes min(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The minimum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte Min(sbyte first, sbyte second) =>
        Math.Min(first, second);

    /// <summary>
    /// Computes min(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The minimum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short Min(short first, short second) =>
        Math.Min(first, second);

    /// <summary>
    /// Computes min(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The minimum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Min(int first, int second) =>
        Math.Min(first, second);

    /// <summary>
    /// Computes min(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The minimum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Min(long first, long second) =>
        Math.Min(first, second);

    /// <summary>
    /// Computes min(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The minimum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Min(byte first, byte second) =>
        Math.Min(first, second);

    /// <summary>
    /// Computes min(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The minimum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Min(ushort first, ushort second) =>
        Math.Min(first, second);

    /// <summary>
    /// Computes min(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The minimum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Min(uint first, uint second) =>
        Math.Min(first, second);

    /// <summary>
    /// Computes min(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The minimum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Min(ulong first, ulong second) =>
        Math.Min(first, second);

    /// <summary>
    /// Computes max(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The maximum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Max(double first, double second) =>
        Math.Max(first, second);

    /// <summary>
    /// Computes max(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The maximum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Max(float first, float second) =>
        Math.Max(first, second);

    /// <summary>
    /// Computes max(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The maximum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half Max(Half first, Half second) =>
        HalfExtensions.MaxFP32(first, second);

    /// <summary>
    /// Computes max(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The maximum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte Max(sbyte first, sbyte second) =>
        Math.Max(first, second);

    /// <summary>
    /// Computes max(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The maximum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short Max(short first, short second) =>
        Math.Max(first, second);

    /// <summary>
    /// Computes max(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The maximum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Max(int first, int second) =>
        Math.Max(first, second);

    /// <summary>
    /// Computes max(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The maximum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Max(long first, long second) =>
        Math.Max(first, second);

    /// <summary>
    /// Computes max(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The maximum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Max(byte first, byte second) =>
        Math.Max(first, second);

    /// <summary>
    /// Computes max(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The maximum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Max(ushort first, ushort second) =>
        Math.Max(first, second);

    /// <summary>
    /// Computes max(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The maximum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Max(uint first, uint second) =>
        Math.Max(first, second);

    /// <summary>
    /// Computes max(first, second).
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <returns>The maximum of first and second value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Max(ulong first, ulong second) =>
        Math.Max(first, second);

    /// <summary>
    /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The first argument.</param>
    /// <param name="max">The second argument.</param>
    /// <returns>The clamped value in the interval [min, max].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Clamp(double value, double min, double max) =>
        Max(Min(value, max), min);

    /// <summary>
    /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The first argument.</param>
    /// <param name="max">The second argument.</param>
    /// <returns>The clamped value in the interval [min, max].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float value, float min, float max) =>
        Max(Min(value, max), min);

    /// <summary>
    /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The first argument.</param>
    /// <param name="max">The second argument.</param>
    /// <returns>The clamped value in the interval [min, max].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half Clamp(Half value, Half min, Half max) =>
        Max(Min(value, max), min);

    /// <summary>
    /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The first argument.</param>
    /// <param name="max">The second argument.</param>
    /// <returns>The clamped value in the interval [min, max].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte Clamp(sbyte value, sbyte min, sbyte max) =>
        Max(Min(value, max), min);

    /// <summary>
    /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The first argument.</param>
    /// <param name="max">The second argument.</param>
    /// <returns>The clamped value in the interval [min, max].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short Clamp(short value, short min, short max) =>
        Max(Min(value, max), min);

    /// <summary>
    /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The first argument.</param>
    /// <param name="max">The second argument.</param>
    /// <returns>The clamped value in the interval [min, max].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int value, int min, int max) =>
        Max(Min(value, max), min);

    /// <summary>
    /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The first argument.</param>
    /// <param name="max">The second argument.</param>
    /// <returns>The clamped value in the interval [min, max].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Clamp(long value, long min, long max) =>
        Max(Min(value, max), min);

    /// <summary>
    /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The first argument.</param>
    /// <param name="max">The second argument.</param>
    /// <returns>The clamped value in the interval [min, max].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Clamp(byte value, byte min, byte max) =>
        Max(Min(value, max), min);

    /// <summary>
    /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The first argument.</param>
    /// <param name="max">The second argument.</param>
    /// <returns>The clamped value in the interval [min, max].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Clamp(ushort value, ushort min, ushort max) =>
        Max(Min(value, max), min);

    /// <summary>
    /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The first argument.</param>
    /// <param name="max">The second argument.</param>
    /// <returns>The clamped value in the interval [min, max].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Clamp(uint value, uint min, uint max) =>
        Max(Min(value, max), min);

    /// <summary>
    /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The first argument.</param>
    /// <param name="max">The second argument.</param>
    /// <returns>The clamped value in the interval [min, max].</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Clamp(ulong value, ulong min, ulong max) =>
        Max(Min(value, max), min);
}
