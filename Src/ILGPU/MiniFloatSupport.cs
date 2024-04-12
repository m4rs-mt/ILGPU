// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: MiniFloatSupport.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU;

internal static class MiniFloatSupport
{
    internal static byte[] GenerateShiftTable(int mantissaBitsInFloat8)
    {
        byte[] shiftTable = new byte[512];
        // For each possible 8-bit exponent value in the IEEE 754 format

        // Calculate the shift required to adjust a 23-bit
        // IEEE 754 mantissa to the target mantissa size
        int standardShift = 23 - mantissaBitsInFloat8;

        for (int i = 0; i < 256; i++)
        {
            int exponent = i - 127; // Adjusting for the bias of 32-bit floats

            if (exponent <= -127) // Handling special cases (subnormals, zeros)
            {
                // For IEEE 754, subnormals have an exponent of -126
                // However, when the exponent bits are all zeros
                // it represents either zero or subnormal numbers.
                // In these cases, it might make sense to shift the mantissa fully,
                // as these are too small to represent directly.
                shiftTable[i] = (byte)standardShift;
            }
            else if (exponent >= -126 && exponent <= 128) // Normal range
            {
                // The shift for normal numbers adjusts the mantissa to fit the
                // mini float's smaller size.
                shiftTable[i] = (byte)standardShift;
            }
            else // Handling very large exponents (overflow cases)
            {
                // For very large exponent values that the mini float cannot represent,
                // it might be necessary to handle these as special cases,
                // potentially clamping them.
                // However, without specific handling for infinity or NaN
                // (not representable in all mini float formats),
                // a full shift could be used to effectively zero the mantissa.
                shiftTable[i] = (byte)standardShift;
            }
        }

        return shiftTable;
    }

    internal static byte[] GenerateBaseTable(int exponentBitsInFloat8)
    {
        byte[] baseTable = new byte[512];
        // There are 256 possible values for an 8-bit exponent
        // However we want to include the s

        int miniFloatBias = (1 << (exponentBitsInFloat8 - 1)) - 1;
        // Correctly calculate the mini-float bias
        int maxExponentValue = (1 << exponentBitsInFloat8) - 1;
        // Maximum value based on exponent bits

        for (int i = 0; i < 256; i++)
        {
            // Convert i to a signed integer to handle negative exponents properly
            int exponent = i - 127; // Adjust for 32-bit float bias

            // Adjust for mini float bias and clamp
            exponent += miniFloatBias;

            // Clamp the value to fit in the range determined by exponentBitsInFloat8
            exponent = Math.Max(0, Math.Min(exponent, maxExponentValue - 1));

            baseTable[i] = (byte)exponent;
            baseTable[512-i] = (byte)exponent;
        }

        return baseTable;
    }



}
