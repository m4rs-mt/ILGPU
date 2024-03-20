// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Mini43AIFloat8Extensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU;


/// <summary>
/// Extension class for Mini43AIFloat8
/// </summary>
public static partial class Mini43AIFloat8Extensions
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

    internal const byte ExponentMask = 0x78;
    // 0111 1000 (covers only the exponent)

    internal const byte MantissaMask = 0x07;
    // 0000 0111 (covers only the mantissa)


    private static uint[] exponentToSingleLookupTable
        = GenerateToSingleExponentLookupTable();

// Generates the lookup table for exponent conversion
// from Mini52Float8 to single-precision float.
    private static uint[] GenerateToSingleExponentLookupTable()
    {
        uint[] table = new uint[16]; // 4-bit exponent can have 16 different values
        for (int i = 0; i < 16; i++)
        {
            // Adjust the exponent from Mini52Float8 bias (15)
            // to single-precision float bias (127)
            int adjustedExponent = i - 7 + 127;
            // Ensure adjusted exponent is not negative. If it is, set it to 0
            // (which represents a denormalized number in IEEE 754)
            table[i] = (uint)adjustedExponent << 23;
            // Shift adjusted exponent into the correct position for single-precision
        }
        return table;
    }

    /// <summary>
    /// Convert Mini43AIFloat8 to float
    /// </summary>
    /// <param name="rawMini43AIFloat8">Mini43AIFloat8 as byte value to convert</param>
    /// <returns>Value converted to float</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float ByteToSingleForMiniFloat43AI(byte rawMini43AIFloat8)
    {
        uint sign = (uint)(rawMini43AIFloat8 & 0x80) << 24;
        // Move sign bit to correct position

        uint exponentIndex = (uint)(rawMini43AIFloat8 >> 3) & 0x0F;


        // Mini43Float8 does not support infinities and extends its range to
        // values to 448
        if ((exponentIndex == 0x0F) && ((rawMini43AIFloat8 & 0x7) == 0x7))
        {
            return float.NaN;
        }

        uint exponent = exponentToSingleLookupTable[exponentIndex];

        uint mantissa = (uint)(rawMini43AIFloat8 & 0x07) << (23 - 3);
        // Correctly scale mantissa, considering 3 mantissa bits


        // Combine sign, exponent, and mantissa into a 32-bit float representation
        uint floatBits = sign | exponent | mantissa;

        // Convert the 32-bit representation into a float
        return Unsafe.As<uint, float>(ref floatBits);
    }



    /// <summary>
    /// Negate value
    /// </summary>
    /// <param name="mini43AiFloat8Value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 Neg(Mini43AIFloat8 mini43AiFloat8Value) =>
        new Mini43AIFloat8((byte)(mini43AiFloat8Value.RawValue ^ SignBitMask));



    /// <summary>
    /// Absolute value
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 Abs(Mini43AIFloat8 value) =>
        new Mini43AIFloat8((byte)(value.RawValue & ExponentMantissaMask));

    /// <summary>
    /// Implements a FP16 addition using FP32.
    /// </summary>
    /// <param name="first">The first Mini43AIFloat8.</param>
    /// <param name="second">The second Mini43AIFloat8.</param>
    /// <returns>The resulting Mini43AIFloat8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 AddFP32(Mini43AIFloat8 first, Mini43AIFloat8 second) =>
        (Mini43AIFloat8)((float)first + (float)second);

    /// <summary>
    /// Implements a FP16 subtraction using FP32.
    /// </summary>
    /// <param name="first">The first Mini43AIFloat8.</param>
    /// <param name="second">The second Mini43AIFloat8.</param>
    /// <returns>The resulting Mini43AIFloat8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 SubFP32(Mini43AIFloat8 first, Mini43AIFloat8 second) =>
        (Mini43AIFloat8)((float)first - (float)second);

    /// <summary>
    /// Implements a FP16 multiplication using FP32.
    /// </summary>
    /// <param name="first">The first Mini43AIFloat8.</param>
    /// <param name="second">The second Mini43AIFloat8.</param>
    /// <returns>The resulting Mini43AIFloat8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 MulFP32(Mini43AIFloat8 first, Mini43AIFloat8 second) =>
        (Mini43AIFloat8)((float)first * (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first Mini43AIFloat8.</param>
    /// <param name="second">The second Mini43AIFloat8.</param>
    /// <returns>The resulting Mini43AIFloat8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 DivFP32(Mini43AIFloat8 first, Mini43AIFloat8 second) =>
        (Mini43AIFloat8)((float)first / (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first Mini43AIFloat8.</param>
    /// <param name="second">The second Mini43AIFloat8.</param>
    /// <param name="third">The third Mini43AIFloat8.</param>
    /// <returns>The resulting Mini43AIFloat8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 FmaFP32(Mini43AIFloat8 first, Mini43AIFloat8 second,
        Mini43AIFloat8 third) =>
        (Mini43AIFloat8)((float)first * (float)second + (float)third);


}
