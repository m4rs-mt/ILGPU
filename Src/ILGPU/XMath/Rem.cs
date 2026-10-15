// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Rem.cs
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
    /// Computes x%y.
    /// </summary>
    /// <param name="x">The nominator.</param>
    /// <param name="y">The denominator.</param>
    /// <returns>x%y.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Rem(double x, double y) => x % y;

    /// <summary>
    /// Computes x%y.
    /// </summary>
    /// <param name="x">The nominator.</param>
    /// <param name="y">The denominator.</param>
    /// <returns>x%y.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Rem(float x, float y) => x % y;

    /// <summary>
    /// Computes x%y.
    /// </summary>
    /// <param name="x">The nominator.</param>
    /// <param name="y">The denominator.</param>
    /// <returns>x%y.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half Rem(Half x, Half y) => (Half)Rem((float)x, (float)y);

    /// <summary>
    /// Computes remainder operation that complies with the IEEE 754 specification.
    /// </summary>
    /// <param name="x">The nominator.</param>
    /// <param name="y">The denominator.</param>
    /// <returns>x%y.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double IEEERemainder(double x, double y) =>
        Math.IEEERemainder(x, y);

    /// <summary>
    /// Computes remainder operation that complies with the IEEE 754 specification.
    /// </summary>
    /// <param name="x">The nominator.</param>
    /// <param name="y">The denominator.</param>
    /// <returns>x%y.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float IEEERemainder(float x, float y) =>
        MathF.IEEERemainder(x, y);

    /// <summary>
    /// Computes remainder operation that complies with the IEEE 754 specification.
    /// </summary>
    /// <param name="x">The nominator.</param>
    /// <param name="y">The denominator.</param>
    /// <returns>x%y.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half IEEERemainder(Half x, Half y) =>
        (Half)IEEERemainder((float)x, (float)y);
}
