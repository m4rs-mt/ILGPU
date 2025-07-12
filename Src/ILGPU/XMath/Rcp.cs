// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Rcp.cs
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
    /// Computes 1.0 / value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>1.0 / value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Rcp(double value) => 1.0 / value;

    /// <summary>
    /// Computes 1.0f / value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>1.0f / value.</returns>
    [MathIntrinsic]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Rcp(float value) => 1.0f / value;
}
