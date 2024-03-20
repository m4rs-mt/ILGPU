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
/// temp half operator
/// </summary>
public readonly partial struct Half
{
    /// <summary>
    /// Cast BFloat16 to Half
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static explicit operator Half(BFloat16 value) =>
        BFloat16ToHalf(value);

    internal static Half BFloat16ToHalf(BFloat16 bFloat16)
    {
        ushort bFloat16Bits = bFloat16.RawValue;

        // Extracting sign (1 bit) - directly copied
        ushort sign = (ushort)(bFloat16Bits & 0x8000);

        // Adjusting the exponent from BFloat16 to Half, considering bias differences
        int exponent = ((bFloat16Bits >> 7) & 0xFF) - 127 + 15; // Adjust for bias
        if (exponent < 0) exponent = 0; // Clamp to zero if underflow
        if (exponent > 0x1F) exponent = 0x1F; // Clamp to max if overflow
        exponent <<= 10; // Position the exponent for Half

        // Extracting and positioning the mantissa bits (no need to expand, just align)
        ushort mantissa = (ushort)((bFloat16Bits & 0x007F) << (13 - 7));
        // Align with Half's mantissa

        // Combining sign, adjusted exponent, and mantissa into Half format
        ushort halfBits = (ushort)(sign | exponent | mantissa);

        return Unsafe.As<ushort, Half>(ref halfBits);
    }
}


/// <summary>
/// Extension class for BFloat16
/// </summary>
public static partial class BFloat16Extensions
{
    /// <summary>
    /// The bit mask of the sign bit.
    /// </summary>
    internal const ushort SignBitMask = 0x8000;

    /// <summary>
    /// The bit mask of the exponent and the mantissa.
    /// </summary>
    internal const ushort ExponentMantissaMask = 0x7FFF;
    // 0111 1111 1111 1111 (ignores the sign bit)
    internal const ushort ExponentMask = 0x7F80;
    // 0111 1111 1000 0000 (covers only the exponent)
    internal const ushort MantissaMask = 0x007F;

    /// <summary>
    /// Negate value
    /// </summary>
    /// <param name="bFloat16Value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 Neg(BFloat16 bFloat16Value) =>
        new BFloat16((ushort)(bFloat16Value.RawValue ^ SignBitMask));



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
