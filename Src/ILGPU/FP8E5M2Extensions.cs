// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: FP8E5M2Extensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace ILGPU;


/// <summary>
/// Extension class for FP8E5M2
/// </summary>
public static partial class FP8E5M2Extensions
{
    #region Static



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
// FP8E5M2 to single-precision float.
   private static uint[] GenerateToSingleExponentLookupTable()
   {
       uint[] table = new uint[32]; // 5-bit exponent can have 32 different values
       for (int i = 0; i < 32; i++)
       {
           // Adjust the exponent from FP8E5M2 bias (15) to
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
   /// Convert FP8E5M2 to float
   /// </summary>
   /// <param name="rawFP8E5M2">raw FP8E5M2 byte value to convert</param>
   /// <returns>Value converted to float</returns>

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   internal static float ByteToSingleForFP8E5M2(byte rawFP8E5M2)
   {
       uint sign = (uint)(rawFP8E5M2 & 0x80) << 24;
       // Move sign bit to correct position

       uint exponentIndex = (uint)(rawFP8E5M2 >> 2) & 0x1F;

       uint exponent = exponentToSingleLookupTable[exponentIndex];

       uint mantissa = (uint)(rawFP8E5M2 & 0x03) << (23 - 2);
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



   #endregion



    /// <summary>
    /// Negate value
    /// </summary>
    /// <param name="FP8E5M2Value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 Neg(FP8E5M2 FP8E5M2Value) =>
        new FP8E5M2((byte)(FP8E5M2Value.RawValue ^ SignBitMask));



    /// <summary>
    /// Absolute value
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 Abs(FP8E5M2 value) =>
        new FP8E5M2((byte)(value.RawValue & ExponentMantissaMask));

    /// <summary>
    /// Implements a FP16 addition using FP32.
    /// </summary>
    /// <param name="first">The first FP8E5M2.</param>
    /// <param name="second">The second FP8E5M2.</param>
    /// <returns>The resulting FP8E5M2 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 AddFP32(FP8E5M2 first, FP8E5M2 second) =>
        (FP8E5M2)((float)first + (float)second);

    /// <summary>
    /// Implements a FP16 subtraction using FP32.
    /// </summary>
    /// <param name="first">The first FP8E5M2.</param>
    /// <param name="second">The second FP8E5M2.</param>
    /// <returns>The resulting FP8E5M2 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 SubFP32(FP8E5M2 first, FP8E5M2 second) =>
        (FP8E5M2)((float)first - (float)second);

    /// <summary>
    /// Implements a FP16 multiplication using FP32.
    /// </summary>
    /// <param name="first">The first FP8E5M2.</param>
    /// <param name="second">The second FP8E5M2.</param>
    /// <returns>The resulting FP8E5M2 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 MulFP32(FP8E5M2 first, FP8E5M2 second) =>
        (FP8E5M2)((float)first * (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first FP8E5M2.</param>
    /// <param name="second">The second FP8E5M2.</param>
    /// <returns>The resulting FP8E5M2 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 DivFP32(FP8E5M2 first, FP8E5M2 second) =>
        (FP8E5M2)((float)first / (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first FP8E5M2.</param>
    /// <param name="second">The second FP8E5M2.</param>
    /// <param name="third">The third FP8E5M2.</param>
    /// <returns>The resulting FP8E5M2 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 FmaFP32(FP8E5M2 first, FP8E5M2 second,
        FP8E5M2 third) =>
        (FP8E5M2)((float)first * (float)second + (float)third);


}
