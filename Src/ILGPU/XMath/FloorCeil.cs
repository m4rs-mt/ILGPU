// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: FloorCeil.cs
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
    /// Computes floor(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>floor(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Floor(double value) => Math.Floor(value);

    /// <summary>
    /// Computes floor(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>floor(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Floor(float value) => MathF.Floor(value);

    /// <summary>
    /// Computes ceiling(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>ceiling(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Ceiling(double value) => Math.Ceiling(value);

    /// <summary>
    /// Computes ceiling(value).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>ceiling(value).</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Ceiling(float value) => MathF.Ceiling(value);
}
