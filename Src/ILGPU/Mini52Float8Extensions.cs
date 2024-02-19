// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Mini52Float8Extensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU;


/// <summary>
/// Extension class for Mini52Float8
/// </summary>
public static partial class Mini52Float8Extensions
{
    /// <summary>
    /// The bit mask of the sign bit.
    /// </summary>
    internal const byte SignBitMask = 0x80;

    /// <summary>
    /// The bit mask of the exponent and the mantissa.
    /// </summary>
    internal const byte ExponentMantissaMask = 0x7F;
    // 0111 1111 (ignores the sign bit)

    internal const byte ExponentMask = 0x7E;
    // 0111 1110 (covers only the exponent)

    internal const byte MantissaMask = 0x01;
    // 0000 0001 (covers only the mantissa)

    /// <summary>
    /// Negate value
    /// </summary>
    /// <param name="Mini52Float8Value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 Neg(Mini52Float8 Mini52Float8Value) =>
        new Mini52Float8((byte)(Mini52Float8Value.RawValue ^ SignBitMask));



    /// <summary>
    /// Absolute value
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 Abs(Mini52Float8 value) =>
        new Mini52Float8((byte)(value.RawValue & ExponentMantissaMask));

    /// <summary>
    /// Implements a FP16 addition using FP32.
    /// </summary>
    /// <param name="first">The first Mini52Float8.</param>
    /// <param name="second">The second Mini52Float8.</param>
    /// <returns>The resulting Mini52Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 AddFP32(Mini52Float8 first, Mini52Float8 second) =>
        (Mini52Float8)((float)first + (float)second);

    /// <summary>
    /// Implements a FP16 subtraction using FP32.
    /// </summary>
    /// <param name="first">The first Mini52Float8.</param>
    /// <param name="second">The second Mini52Float8.</param>
    /// <returns>The resulting Mini52Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 SubFP32(Mini52Float8 first, Mini52Float8 second) =>
        (Mini52Float8)((float)first - (float)second);

    /// <summary>
    /// Implements a FP16 multiplication using FP32.
    /// </summary>
    /// <param name="first">The first Mini52Float8.</param>
    /// <param name="second">The second Mini52Float8.</param>
    /// <returns>The resulting Mini52Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 MulFP32(Mini52Float8 first, Mini52Float8 second) =>
        (Mini52Float8)((float)first * (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first Mini52Float8.</param>
    /// <param name="second">The second Mini52Float8.</param>
    /// <returns>The resulting Mini52Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 DivFP32(Mini52Float8 first, Mini52Float8 second) =>
        (Mini52Float8)((float)first / (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first Mini52Float8.</param>
    /// <param name="second">The second Mini52Float8.</param>
    /// <param name="third">The third Mini52Float8.</param>
    /// <returns>The resulting Mini52Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 FmaFP32(Mini52Float8 first, Mini52Float8 second,
        Mini52Float8 third) =>
        (Mini52Float8)((float)first * (float)second + (float)third);


}
