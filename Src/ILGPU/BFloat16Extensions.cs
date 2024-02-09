// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: BFloat16Extensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------



using System.Runtime.CompilerServices;

namespace ILGPU;

/// <summary>
/// Extension class for BFloat16
/// </summary>
public static partial class BFloat16Extensions
{
    /// <summary>
    /// The bit mask of the sign bit.
    /// </summary>
    private const ushort SignBitMask = 0x8000;

    /// <summary>
    /// Negate value
    /// </summary>
    /// <param name="bFloat16Value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 Neg(BFloat16 bFloat16Value) =>
        new BFloat16((ushort)(bFloat16Value.RawValue ^ SignBitMask));

    /// <summary>
    /// The bit mask of the exponent and the mantissa.
    /// </summary>
    private const ushort ExponentMantissaMask = 0x7FFF;


    /// <summary>
    /// Absolute value
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 Abs(BFloat16 value) =>
        new BFloat16((ushort)(value.RawValue & ExponentMantissaMask));

    /// <summary>
    /// Implements a FP16 addition using FP32.
    /// </summary>
    /// <param name="first">The first BFloat16.</param>
    /// <param name="second">The second BFloat16.</param>
    /// <returns>The resulting BFloat16 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 AddFP32(BFloat16 first, BFloat16 second) =>
        (BFloat16)((float)first + (float)second);

    /// <summary>
    /// Implements a FP16 subtraction using FP32.
    /// </summary>
    /// <param name="first">The first BFloat16.</param>
    /// <param name="second">The second BFloat16.</param>
    /// <returns>The resulting BFloat16 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 SubFP32(BFloat16 first, BFloat16 second) =>
        (BFloat16)((float)first - (float)second);

    /// <summary>
    /// Implements a FP16 multiplication using FP32.
    /// </summary>
    /// <param name="first">The first BFloat16.</param>
    /// <param name="second">The second BFloat16.</param>
    /// <returns>The resulting BFloat16 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 MulFP32(BFloat16 first, BFloat16 second) =>
        (BFloat16)((float)first * (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first BFloat16.</param>
    /// <param name="second">The second BFloat16.</param>
    /// <returns>The resulting BFloat16 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 DivFP32(BFloat16 first, BFloat16 second) =>
        (BFloat16)((float)first / (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first BFloat16.</param>
    /// <param name="second">The second BFloat16.</param>
    /// <param name="third">The third BFloat16.</param>
    /// <returns>The resulting BFloat16 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 FmaFP32(BFloat16 first, BFloat16 second, BFloat16 third) =>
        (BFloat16)((float)first * (float)second + (float)third);


}
