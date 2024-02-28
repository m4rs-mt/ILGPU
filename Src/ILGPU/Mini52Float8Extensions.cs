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

using System;
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



   private static uint[] exponentToSingleLookupTable
       = GenerateToSingleExponentLookupTable();

// Generates the lookup table for exponent conversion from
// Mini52Float8 to single-precision float.
   private static uint[] GenerateToSingleExponentLookupTable()
   {
       uint[] table = new uint[32]; // 5-bit exponent can have 32 different values
       for (int i = 0; i < 32; i++)
       {
           // Adjust the exponent from Mini52Float8 bias (15) to
           // single-precision float bias (127)
           int adjustedExponent = (i - 15) + 127;
           // Ensure adjusted exponent is not negative. If it is, set it to 0
           // (which represents a denormalized number in IEEE 754)

           adjustedExponent = Math.Max(0, adjustedExponent);

           table[i] = (uint)adjustedExponent << 23;
           // Shift adjusted exponent into the correct position for single-precision
       }
       return table;
   }

   /// <summary>
   /// Convert Mini52Float8 to float
   /// </summary>
   /// <param name="mini52Float8">Mini52Float8 value to convert</param>
   /// <returns>Value converted to float</returns>

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   internal static float ByteToSingleForMiniFloat52(byte rawMini52Float8)
   {
       uint sign = (uint)(rawMini52Float8 & 0x80) << 24;
       // Move sign bit to correct position

       uint exponentIndex = (uint)(rawMini52Float8 >> 2) & 0x1F;

       uint exponent = exponentToSingleLookupTable[exponentIndex];

       uint mantissa = (uint)(rawMini52Float8 & 0x03) << (23 - 2);
       // Correctly scale mantissa, considering 2 mantissa bits

       // Check for special cases: NaN and Infinity
       if (exponentIndex == 0x1F) { // All exponent bits are 1
           if (mantissa >> 21 != 0) { // Non-zero mantissa means NaN
               return float.NaN;
           } else { // Zero mantissa means Infinity
               return sign == 0 ? float.PositiveInfinity : float.NegativeInfinity;
           }
       }

       // Combine sign, exponent, and mantissa into a 32-bit float representation
       uint floatBits = sign | exponent | mantissa;

       // Convert the 32-bit representation into a float
       return Unsafe.As<uint, float>(ref floatBits);
   }




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
