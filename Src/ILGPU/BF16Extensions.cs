// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: BF16Extensions.cs
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
    /// Cast BF16 to Half
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static explicit operator Half(BF16 value) =>
        BF16ToHalf(value);

    internal static Half BF16ToHalf(BF16 bf16)
    {
        ushort BF16Bits = bf16.RawValue;

        // Extracting sign (1 bit) - directly copied
        ushort sign = (ushort)(BF16Bits & 0x8000);

        // Adjusting the exponent from BF16 to Half, considering bias differences
        int exponent = ((BF16Bits >> 7) & 0xFF) - 127 + 15; // Adjust for bias
        if (exponent < 0) exponent = 0; // Clamp to zero if underflow
        if (exponent > 0x1F) exponent = 0x1F; // Clamp to max if overflow
        exponent <<= 10; // Position the exponent for Half

        // Extracting and positioning the mantissa bits (no need to expand, just align)
        ushort mantissa = (ushort)((BF16Bits & 0x007F) << (13 - 7));
        // Align with Half's mantissa

        // Combining sign, adjusted exponent, and mantissa into Half format
        ushort halfBits = (ushort)(sign | exponent | mantissa);

        return Unsafe.As<ushort, Half>(ref halfBits);
    }
}


/// <summary>
/// Extension class for BF16
/// </summary>
public static partial class BF16Extensions
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
    /// <param name="bf16Value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 Neg(BF16 bf16Value) =>
        new BF16((ushort)(bf16Value.RawValue ^ SignBitMask));



    /// <summary>
    /// Absolute value
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 Abs(BF16 value) =>
        new BF16((ushort)(value.RawValue & ExponentMantissaMask));

    /// <summary>
    /// Implements a FP16 addition using FP32.
    /// </summary>
    /// <param name="first">The first BF16.</param>
    /// <param name="second">The second BF16.</param>
    /// <returns>The resulting BF16 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 AddFP32(BF16 first, BF16 second) =>
        (BF16)((float)first + (float)second);

    /// <summary>
    /// Implements a FP16 subtraction using FP32.
    /// </summary>
    /// <param name="first">The first BF16.</param>
    /// <param name="second">The second BF16.</param>
    /// <returns>The resulting BF16 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 SubFP32(BF16 first, BF16 second) =>
        (BF16)((float)first - (float)second);

    /// <summary>
    /// Implements a FP16 multiplication using FP32.
    /// </summary>
    /// <param name="first">The first BF16.</param>
    /// <param name="second">The second BF16.</param>
    /// <returns>The resulting BF16 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 MulFP32(BF16 first, BF16 second) =>
        (BF16)((float)first * (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first BF16.</param>
    /// <param name="second">The second BF16.</param>
    /// <returns>The resulting BF16 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 DivFP32(BF16 first, BF16 second) =>
        (BF16)((float)first / (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first BF16.</param>
    /// <param name="second">The second BF16.</param>
    /// <param name="third">The third BF16.</param>
    /// <returns>The resulting BF16 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 FmaFP32(BF16 first, BF16 second, BF16 third) =>
        (BF16)((float)first * (float)second + (float)third);


}
