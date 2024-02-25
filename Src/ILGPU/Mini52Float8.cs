// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Mini52Float8.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU;

/// <summary>
/// Mini52Float8 Implementation
/// </summary>
public readonly struct Mini52Float8
#if NET7_0_OR_GREATER
    : INumber<Mini52Float8>
#else
    : IComparable, IEquatable<Mini52Float8>, IComparable<Mini52Float8>
#endif
{

    #region constants

    /// <summary>
    /// Radix
    /// </summary>
    public static int Radix => 2;

    /// <summary>
    /// Zero
    /// </summary>
    public static Mini52Float8 Zero  {get; } = new Mini52Float8(0x00);

    /// <summary>
    /// One
    /// </summary>

    public static Mini52Float8 One { get; } = new Mini52Float8(0x3C);


    /// <summary>
    /// Represents positive infinity.
    /// </summary>
    public static Mini52Float8 PositiveInfinity { get; } = new Mini52Float8(0xFC);

    /// <summary>
    /// Represents negative infinity.
    /// </summary>
    public static Mini52Float8 NegativeInfinity{ get; } = new Mini52Float8(0xFC);

    /// <summary>
    /// Epsilon - smallest positive value
    /// </summary>
    public static Mini52Float8 Epsilon { get; } = new Mini52Float8(0x04);

    /// <summary>
    /// MaxValue - most positive value
    /// </summary>
    public static Mini52Float8 MaxValue { get; } = new Mini52Float8(0x7B);

    /// <summary>
    /// MinValue ~ most negative value
    /// </summary>
    public static Mini52Float8 MinValue { get; } = new Mini52Float8(0xFB);

    /// <summary>
    /// NaN ~ value with all exponent bits set to 1 and a non-zero mantissa
    /// </summary>
    public static Mini52Float8 NaN { get; } = new Mini52Float8(0x7F);



    #endregion

    #region Comparable



    /// <summary>
    /// CompareTo
    /// </summary>
    /// <param name="obj">Object to compare</param>
    /// <returns>Zero when equal</returns>
    /// <exception cref="ArgumentException">Thrown when not Mini52Float8</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(object? obj)
    {
        if (obj is Mini52Float8 other)
            return CompareTo((Mini52Float8)other);
        if (obj != null)
            throw new ArgumentException("Must be " + nameof(Mini52Float8));
        return 1;
    }



    /// <summary>
    /// CompareTo
    /// </summary>
    /// <param name="other">Mini52Float8 to compare</param>
    /// <returns>Zero when successful</returns>
    public int CompareTo(Mini52Float8 other) => ((float)this).CompareTo(other);

    #endregion

    #region Equality




    /// <summary>
    /// Equals
    /// </summary>
    /// <param name="obj">Object to compare</param>
    /// <returns>bool</returns>
    public readonly override bool Equals(object? obj) =>
        obj is Mini52Float8 mini52Float8 && Equals(mini52Float8);

    /// <summary>
    /// Equals
    /// </summary>
    /// <param name="other">Other to compare</param>
    /// <returns>True when successful</returns>
    public bool Equals(Mini52Float8 other) => this == other;

    /// <summary>
    /// GetHashCode
    /// </summary>
    /// <returns>RawValue as hashcode</returns>
    public readonly override int GetHashCode() => RawValue;

    #endregion

    #region ComparisonOperators

    /// <summary>
    /// Operator Equals
    /// </summary>
    /// <param name="first">First Mini52Float8 value</param>
    /// <param name="second">Second Mini52Float8 value</param>
    /// <returns>True when equal</returns>
    [CompareIntrinisc(CompareKind.Equal)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Mini52Float8 first, Mini52Float8 second) =>
        (byte)Unsafe.As<Mini52Float8, byte>(ref first) ==
        (byte)Unsafe.As<Mini52Float8, byte>(ref second);


    /// <summary>
    /// Operator Not Equals
    /// </summary>
    /// <param name="first">First Mini52Float8 value</param>
    /// <param name="second">Second Mini52Float8 value</param>
    /// <returns>True when not equal</returns>
    [CompareIntrinisc(CompareKind.NotEqual)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator != (Mini52Float8 first, Mini52Float8 second) =>
        (byte)Unsafe.As<Mini52Float8, byte>(ref first) !=
        (byte)Unsafe.As<Mini52Float8, byte>(ref second);


    /// <summary>
    /// Operator less than
    /// </summary>
    /// <param name="first">First Mini52Float8 value</param>
    /// <param name="second">Second Mini52Float8 value</param>
    /// <returns>True when less than</returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Mini52Float8 first, Mini52Float8 second) =>
        (float)first < (float)second;


    /// <summary>
    /// Operator less than or equals
    /// </summary>
    /// <param name="first">First Mini52Float8 value</param>
    /// <param name="second">Second Mini52Float8 value</param>
    /// <returns>True when less than or equal</returns>
    [CompareIntrinisc(CompareKind.LessEqual)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Mini52Float8 first, Mini52Float8 second) =>
        (float)first <= (float)second;


    /// <summary>
    /// Operator greater than
    /// </summary>
    /// <param name="first">First Mini52Float8 value</param>
    /// <param name="second">Second Mini52Float8 value</param>
    /// <returns>True when greater than</returns>
    [CompareIntrinisc(CompareKind.GreaterThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Mini52Float8 first, Mini52Float8 second) =>
        (float)first > (float)second;


    /// <summary>
    /// Operator greater than or equals
    /// </summary>
    /// <param name="first">First Mini52Float8 value</param>
    /// <param name="second">Second Mini52Float8 value</param>
    /// <returns>True when greater than or equal</returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Mini52Float8 first, Mini52Float8 second) =>
        (float)first >= (float)second;



    #endregion


    #region AdditionAndIncrement

    /// <summary>
    /// Increment operator
    /// </summary>
    /// <param name="value">Mini52Float8 value to increment</param>
    /// <returns>Incremented value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 operator ++(Mini52Float8 value)
        => (Mini52Float8) ((float) value + 1f);



    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="value">Mini52Float8 self to add</param>
    /// <returns>Mini52Float8 value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 operator +(Mini52Float8 value) => value;

    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="left">First Mini52Float8 value</param>
    /// <param name="right">Second Mini52Float8 value</param>
    /// <returns>Returns addition</returns>
    public static Mini52Float8 operator +(Mini52Float8 left, Mini52Float8 right)
        => Mini52Float8Extensions.AddFP32(left, right);


#if NET7_0_OR_GREATER

    /// <summary>
    /// IAdditiveIdentity
    /// </summary>
    static Mini52Float8 IAdditiveIdentity<Mini52Float8, Mini52Float8>.AdditiveIdentity
        => new Mini52Float8((byte) 0);
#endif

    #endregion


    #region DecrementAndSubtraction

    /// <summary>
    /// Decrement operator
    /// </summary>
    /// <param name="value">Value to be decremented by 1</param>
    /// <returns>Decremented value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 operator --(Mini52Float8 value)
        => (Mini52Float8) ((float) value - 1f);


    /// <summary>
    /// Subtraction
    /// </summary>
    /// <param name="left">First Mini52Float8 value</param>
    /// <param name="right">Second Mini52Float8 value</param>
    /// <returns>left - right</returns>
    public static Mini52Float8 operator -(Mini52Float8 left, Mini52Float8 right)
        => Mini52Float8Extensions.SubFP32(left, right);




    /// <summary>
    /// Negation
    /// </summary>
    /// <param name="value">Mini52Float8 to Negate</param>
    /// <returns>Negated value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 operator -(Mini52Float8 value)
        => Mini52Float8Extensions.Neg(value);

    #endregion


    #region MultiplicationDivisionAndModulus

    /// <summary>
    /// Multiplication
    /// </summary>
    /// <param name="left">First Mini52Float8 value</param>
    /// <param name="right">Second Mini52Float8 value</param>
    /// <returns>Multiplication of left * right</returns>
    public static Mini52Float8 operator *(Mini52Float8 left, Mini52Float8 right)
        => Mini52Float8Extensions.MulFP32(left,right);

    /// <summary>
    /// Division
    /// </summary>
    /// <param name="left">First Mini52Float8 value</param>
    /// <param name="right">Second Mini52Float8 value</param>
    /// <returns>Left / right</returns>
    public static Mini52Float8 operator /(Mini52Float8 left, Mini52Float8 right)
        => Mini52Float8Extensions.DivFP32(left, right);


    /// <summary>
    /// Multiplicative Identity
    /// </summary>
    public static Mini52Float8 MultiplicativeIdentity  => new Mini52Float8(0x39);

    /// <summary>
    /// Modulus operator
    /// </summary>
    /// <param name="left">First Mini52Float8 value</param>
    /// <param name="right">Second Mini52Float8 value</param>
    /// <returns>Left modulus right</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 operator %(Mini52Float8 left, Mini52Float8 right)
        =>  (Mini52Float8) ((float) left % (float) right);


    #endregion


    #region Formatting


    /// <summary>
    /// ToString
    /// </summary>
    /// <param name="format">Numeric format</param>
    /// <param name="formatProvider">Culture specific parsing provider</param>
    /// <returns>String conversion</returns>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        ((float)this).ToString(format, formatProvider);

    /// <summary>
    /// TryFormat
    /// </summary>
    /// <param name="destination">Span to update</param>
    /// <param name="charsWritten">length written</param>
    /// <param name="format">Numeric format</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>True when successful</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider)
        => ((float)this).TryFormat(destination, out charsWritten, format, provider );

#if NET8_0_OR_GREATER

    /// <summary>
    /// TryFormat
    /// </summary>
    /// <param name="destination">Span to update</param>
    /// <param name="charsWritten">length written</param>
    /// <param name="format">Numeric format</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>True when successful</returns>
    public bool TryFormat(Span<byte> destination, out int charsWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider)
        => ((float)this).TryFormat(destination, out charsWritten, format, provider );

#endif
    #endregion

    #region parsing


    /// <summary>
    /// Parse string to Mini52Float8
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Mini52Float8 value when successful</returns>
    public static Mini52Float8 Parse(string s, IFormatProvider? provider)
        => (Mini52Float8)float.Parse(s,
            NumberStyles.Float | NumberStyles.AllowThousands,
            provider);

    /// <summary>
    /// TryParse string to Mini52Float8
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Mini52Float8 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(string? s, IFormatProvider? provider,
        out Mini52Float8 result)
    {
        float value;
        bool itWorked = float.TryParse(s,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (Mini52Float8)value;
        return itWorked;

    }

#if NET7_0_OR_GREATER

    /// <summary>
    /// Parse Span of char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Value if parsed successfully</returns>
    public static Mini52Float8 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => (Mini52Float8) float.Parse(s, provider);


    /// <summary>
    /// TryParse Span of char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Mini52Float8 out param</param>
    /// <returns>True if parsed successfully</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider,
        out Mini52Float8 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, provider, out floatResult);
        result = (Mini52Float8)floatResult;
        return isGood;
    }



    /// <summary>
    /// Parse Span char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formating attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Value if parsed successfully</returns>
    public static Mini52Float8 Parse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider)
        => (Mini52Float8)float.Parse(s, style, provider);

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formating attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Mini52Float8 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider, out Mini52Float8 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (Mini52Float8)floatResult;
        return isGood;
    }

#endif

#if NET8_0_OR_GREATER
    /// <summary>
    /// Parse
    /// </summary>
    /// <param name="utf8Text">Uft8 encoded by span to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Parsed Half Value</returns>
    public static Mini52Float8 Parse(ReadOnlySpan<byte> utf8Text,
        IFormatProvider? provider)
        => (Mini52Float8) float.Parse(utf8Text, provider);

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="utf8Text">Utf8 encoded byte span to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Mini52Float8 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider,
        out Mini52Float8 result)
    {
        float value;
        bool itWorked = float.TryParse(utf8Text,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (Mini52Float8)value;
        return itWorked;
    }

#endif

    #endregion

    #region object

    internal byte RawValue { get; }



    public Mini52Float8(byte rawValue)
    {
        RawValue = rawValue;
    }

    /// <summary>
    /// AsByte - returns internal value
    /// </summary>
    public byte AsByte => RawValue;


   #endregion


   #region Conversions

   private static uint[] exponentToSingleLookupTable = GenerateToSingleExponentLookupTable();

// Generates the lookup table for exponent conversion from Mini52Float8 to single-precision float.
   private static uint[] GenerateToSingleExponentLookupTable()
   {
       uint[] table = new uint[32]; // 5-bit exponent can have 32 different values
       for (int i = 0; i < 32; i++)
       {
           // Adjust the exponent from Mini52Float8 bias (15) to single-precision float bias (127)
           int adjustedExponent = (i - 15) + 127;
           // Ensure adjusted exponent is not negative. If it is, set it to 0 (which represents a denormalized number in IEEE 754)
           adjustedExponent = Math.Max(0, adjustedExponent);
           table[i] = (uint)adjustedExponent << 23; // Shift adjusted exponent into the correct position for single-precision
       }
       return table;
   }

   /// <summary>
   /// Convert Mini52Float8 to float
   /// </summary>
   /// <param name="mini52Float8">Mini52Float8 value to convert</param>
   /// <returns>Value converted to float</returns>

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private static float Mini52Float8ToSingle(Mini52Float8 mini52Float8)
   {
       byte rawMini52Float8 = mini52Float8.RawValue;
       uint sign = (uint)(rawMini52Float8 & 0x80) << 24; // Move sign bit to correct position

       uint exponentIndex = (uint)(rawMini52Float8 >> 2) & 0x1F;

       uint exponent = exponentToSingleLookupTable[exponentIndex];

       uint mantissa = (uint)(rawMini52Float8 & 0x07) << (23 - 2); // Correctly scale mantissa, considering 2 mantissa bits

       // Combine sign, exponent, and mantissa into a 32-bit float representation
       uint floatBits = sign | exponent | mantissa;

       // Convert the 32-bit representation into a float
       return Unsafe.As<uint, float>(ref floatBits);
   }

// uint exponent =  (((uint)(rawMini52Float8 >> 3) & 0x1F)+ 127 - 7 )<<23;

    private static readonly byte[] exponentToMiniLookupTable = GenerateToMiniExponentLookupTable();

// Generates the lookup table for exponent conversion from single-precision float to Mini52Float8 format.
    private static byte[] GenerateToMiniExponentLookupTable()
    {
        byte[] table = new byte[256]; // 8-bit exponent field can have 256 different values
        for (int i = 0; i < 256; i++)
        {
            // Adjusting from single-precision bias (127) to Mini52Float8 bias (15) and clamping
            int adjustedExponent = i - 127 + 15; // Adjust for Mini52Float8 format
            adjustedExponent = Math.Max(0, Math.Min(31, adjustedExponent)); // Clamp to [0, 31] for 5-bit exponent
            table[i] = (byte)adjustedExponent;
        }
        return table;
    }

    private static Mini52Float8 SingleToMini52Float8(float value)
    {
        // Extracting the binary representation of the float value
        uint floatBits = Unsafe.As<float, uint>(ref value);

        // Extracting sign (1 bit)
        byte sign = (byte)((floatBits >> 24) & 0x80); // Extract sign bit

        // Using the lookup table to convert the exponent
        byte exponentIndex = (byte)((floatBits >> 23) & 0xFF); // Extract 8-bit exponent
        byte exponent = exponentToMiniLookupTable[exponentIndex]; // Convert using the lookup table


        // Extract mantissa bits for rounding
        uint mantissaBits = (floatBits & 0x007FFFFF);
        byte mantissa = (byte)((mantissaBits >> 21) & 0x3); // Direct extraction
        byte roundBit = (byte)((mantissaBits >> 20) & 0x1);
        byte stickyBit = (byte)((mantissaBits & 0x000FFFFF) > 0 ? 1 : 0);

        // Rounding
        if (roundBit == 1 && (stickyBit == 1 || (mantissa & 0x1) == 1)) {
            mantissa++;
            if (mantissa == 0x4) {
                mantissa = 0;
                if (++exponent == 0x20) { // Simplified handling for overflow
                    exponent = 0x1F; // Max value for 5-bit exponent
                }
            }
        }

        // Combining into Mini52Float8 format (1 bit sign, 5 bits exponent, 2 bits mantissa)
        byte mini52Float8 = (byte)(sign | (exponent << 2) | mantissa);

        return new Mini52Float8(mini52Float8);
    }


    /// <summary>
    /// Convert Mini52Float8 to double
    /// </summary>
    /// <param name="mini52Float8"></param>
    /// <returns>Double</returns>
    private static double Mini52Float8ToDouble(Mini52Float8 mini52Float8)
    {
        byte mini52Float8Raw = mini52Float8.RawValue;

        // Extracting sign bit
        ulong sign = (ulong)(mini52Float8Raw & 0x80) << 55; // Shift left for double



        uint exponent = 0;// (((rawMini52Float8 >> 3) & 0x1F) - 15 + 127) << 23;
        // Positioning exponent for double

        // Extracting and positioning the mantissa bits
        ulong mantissa = ((ulong)(mini52Float8Raw & 0x07)) << 49;
        // Align mantissa for double

        // Assembling the double
        ulong doubleBits = sign | exponent | mantissa;

        return BitConverter.Int64BitsToDouble((long)doubleBits);
    }


    /// <summary>
    /// StripSign
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>sign bit as Mini52Float8</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte StripSign(Mini52Float8 value)
    => (byte)(value.RawValue & 0x7F);


    /// <summary>
    /// Convert BFloat16 to Mini52Float8
    /// </summary>
    /// <param name="value">Half to convert</param>
    /// <returns>Mini52Float8</returns>
    private static Mini52Float8 BFloat16ToMini52Float8(BFloat16 value)
    {
        // Extracting the binary representation of the BFloat16 value
        ushort bFloat16Bits = value.RawValue;

        // Extracting sign bit
        byte sign = (byte)((bFloat16Bits >> 15) & 0x01); // Extracting the sign bit (MSB)

        // Adjusting the exponent from BFloat16 (8 bits) to Mini52Float8 (8 bits)
        // This involves adjusting for bias differences
        byte exponent = (byte)(((bFloat16Bits >> 7) & 0xFF) - 127 + 127);
        // Adjusting exponent and bias

        // Adjusting the mantissa from BFloat16 (7 bits) to Mini52Float8 (7 bits)
        // This involves no changes as the mantissa size is the same

        // Combining sign, exponent, and mantissa into Mini52Float8 format
        byte mini52Float8Bits = (byte)((sign << 7) | exponent | (bFloat16Bits & 0x7F));
        // Shift and combine bits

        return new Mini52Float8(mini52Float8Bits);
    }



    /// <summary>
    /// Convert Half to Mini52Float8
    /// </summary>
    /// <param name="value">Half to convert</param>
    /// <returns>Mini52Float8</returns>
    private static Mini52Float8 HalfToMini52Float8(Half value)
    {
        // Extracting the binary representation of the half value
        ushort halfBits = value.RawValue;

        // Extracting sign bit
        byte sign = (byte)((halfBits >> 15) & 0x01); // Extracting the sign bit (MSB)

        // Adjusting the exponent from Half (5 bits) to Mini52Float8 (8 bits)
        byte exponent = (byte)(((halfBits >> 10) & 0x1F) - 15 + 127);
        // Adjusting exponent and bias for Mini52Float8
        // Shift left to align with Mini52Float8's exponent position
        exponent = (byte)(exponent << 3);

        // Adjusting the mantissa from Half (10 bits) to Mini52Float8 (7 bits)
        byte mantissa = (byte)((halfBits & 0x03FF) >> (10 - 7));
        // Truncate mantissa to fit Mini52Float8

        // Combining sign, exponent, and mantissa into Mini52Float8 format
        byte mini52Float8Bits = (byte)((sign << 7) | (exponent) | mantissa);
        // Shift and combine bits

        return new Mini52Float8(mini52Float8Bits);
    }



    /// <summary>
    /// Convert double to Mini52Float8
    /// </summary>
    /// <param name="value">double to convert</param>
    /// <returns>Mini52Float8</returns>
    private static Mini52Float8 DoubleToMini52Float8(double value)
    {
        // Extracting the binary representation of the double value
        ulong doubleBits = BitConverter.ToUInt64(BitConverter.GetBytes(value), 0);

        // Extracting sign bit
        byte sign = (byte)((doubleBits >> 48) & 0x80); // Extracting the sign bit (MSB)

        // Extracting exponent (11 bits)
        long exponentBits = (long)((doubleBits >> 52) & 0x7FF) - 1023 + 127;
        // Adjusting exponent and bias for Mini52Float8
        // Ensure the exponent does not overflow or underflow the valid range
        exponentBits = exponentBits < 0 ? 0 : exponentBits > 0xFF ? 0xFF : exponentBits;
        byte exponent = (byte)(exponentBits >> 4);
        // Shift to align with Mini52Float8's exponent position

        // Extracting mantissa (top 7 bits of the double's 52-bit mantissa)
        byte mantissa = (byte)((doubleBits >> (52 - 7)) & 0x7F);
        // Extracting top 7 bits of mantissa

        // Combining into Mini52Float8 format (1 bit sign, 8 bits exponent,
        // 7 bits mantissa)
        byte mini52Float8 = (byte)(sign | exponent | mantissa);

        return new Mini52Float8(mini52Float8);
    }

    #endregion

    #region operators

    /// <summary>
    /// Cast float to Mini52Float8
    /// </summary>
    /// <param name="value">float to cast</param>
    /// <returns>Mini52Float8</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Mini52Float8(float value)
        => SingleToMini52Float8(value);


    /// <summary>
    /// Cast double to Mini52Float8
    /// </summary>
    /// <param name="value">double to cast</param>
    /// <returns>Mini52Float8</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Mini52Float8(double value)
        => DoubleToMini52Float8(value);

    /// <summary>
    /// Cast Mini52Float8 to Mini43Float8
    /// </summary>
    /// <param name="value">Mini52Float8 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Mini43Float8(Mini52Float8 value)
        => (Mini43Float8)Mini52Float8ToSingle(value);

    /// <summary>
    /// Cast Mini52Float8 to BFloat16
    /// </summary>
    /// <param name="value">Mini52Float8 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BFloat16(Mini52Float8 value)
        => (BFloat16)Mini52Float8ToSingle(value);

    /// <summary>
    /// Cast Mini52Float8 to Half
    /// </summary>
    /// <param name="value">Mini52Float8 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Half(Mini52Float8 value)
        => (Half)Mini52Float8ToSingle(value);


    /// <summary>
    /// Cast Mini52Float8 to float
    /// </summary>
    /// <param name="value">Mini52Float8 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(Mini52Float8 value)
        => Mini52Float8ToSingle(value);

    /// <summary>
    /// Cast Mini52Float8 to double
    /// </summary>
    /// <param name="value">Mini52Float8 value to cast</param>
    /// <returns>double</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(Mini52Float8 value) =>
        (double)Mini52Float8ToSingle(value);


    #endregion

    #region INumberbase




    /// <summary>
    /// Absolute Value
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>Absolute of Mini52Float8</returns>
    [MathIntrinsic(MathIntrinsicKind.Abs)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 Abs(Mini52Float8 value)
        => Mini52Float8Extensions.Abs(value);

    /// <summary>
    /// Is Mini52Float8 Canonical
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>True based on type</returns>
    public static bool IsCanonical(Mini52Float8 value) => true;


    /// <summary>
    /// Is Mini52Float8 a complex number
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>False based on type</returns>
    public static bool IsComplexNumber(Mini52Float8 value) => false;


    /// <summary>
    /// Is value finite
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>True when finite</returns>
    public static bool IsFinite(Mini52Float8 value)
        => Bitwise.And(!IsNaN(value), !IsInfinity(value));

    /// <summary>
    /// Is imaginary number
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>False based on type</returns>
    public static bool IsImaginaryNumber(Mini52Float8 value) => false;

    /// <summary>
    /// Is an infinite value?
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>True when finite</returns>
    public static bool IsInfinity(Mini52Float8 value) =>
        (value.RawValue & 0x80) != 0 && (value.RawValue & 0x7F) == 0;



    /// <summary>
    /// Is NaN
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>True when NaN</returns>
    public static bool IsNaN(Mini52Float8 value) =>
        ((value.RawValue & 0x80) == 0x80) && ((value.RawValue & 0x7F) != 0);



    /// <summary>
    /// Is negative?
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>True when negative</returns>
    public static bool IsNegative(Mini52Float8 value) => (value.RawValue & 0x80) != 0;


    /// <summary>
    /// Is negative infinity
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>True when negative infinity</returns>
    public static bool IsNegativeInfinity(Mini52Float8 value)
        => value == NegativeInfinity;

    /// <summary>
    /// Is normal
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>True when normal</returns>
    public static bool IsNormal(Mini52Float8 value)
    {
        byte num = StripSign(value);
        return num < 0x80 && num != 0 && (num & 0x80) != 0;
    }



    /// <summary>
    /// Is positive?
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>True when positive</returns>
    public static bool IsPositive(Mini52Float8 value) => (value.RawValue & 0x8000) == 0;

    /// <summary>
    /// Is positive infinity?
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>True when positive</returns>
    public static bool IsPositiveInfinity(Mini52Float8 value)
        => value == PositiveInfinity;

    /// <summary>
    /// Is real number
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>True when real number</returns>
    public static bool IsRealNumber(Mini52Float8 value)
    {
        bool isExponentAllOnes = (value.RawValue & Mini52Float8Extensions.ExponentMask)
                                 == Mini52Float8Extensions.ExponentMask;
        bool isMantissaNonZero =
            (value.RawValue & Mini52Float8Extensions.MantissaMask) != 0;

        // If exponent is all ones and mantissa is non-zero, it's NaN, so return false
        return !(isExponentAllOnes && isMantissaNonZero);
    }

    /// <summary>
    /// Is subnormal
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>True when subnormal</returns>
    public static bool IsSubnormal(Mini52Float8 value)
        => (value.RawValue & 0x7F80) == 0 && (value.RawValue & 0x007F) != 0;

    /// <summary>
    /// Is Zero?
    /// </summary>
    /// <param name="value">Mini52Float8</param>
    /// <returns>True when Zero</returns>
    public static bool IsZero(Mini52Float8 value)
        => (value.RawValue & Mini52Float8Extensions.ExponentMantissaMask) == 0;

    /// <summary>
    /// MaxMagnitude
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns larger of x or y, NaN when equal</returns>
    public static Mini52Float8 MaxMagnitude(Mini52Float8 x, Mini52Float8 y)
        =>(Mini52Float8) MathF.MaxMagnitude((float) x, (float) y);

    /// <summary>
    /// MaxMagnitudeNumber
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns x or y based on larger of Abs(x) vs Abs(Y) </returns>
    public static Mini52Float8 MaxMagnitudeNumber(Mini52Float8 x, Mini52Float8 y)
    {
        Mini52Float8 bf1 = Mini52Float8.Abs(x);
        Mini52Float8 bf2 = Mini52Float8.Abs(y);
        return bf1 > bf2 || Mini52Float8.IsNaN(bf2) || bf1
            == bf2 && !Mini52Float8.IsNegative(x) ? x : y;
    }

    /// <summary>
    /// MinMagnitude
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>returns smaller of x or y or NaN when equal</returns>
    public static Mini52Float8 MinMagnitude(Mini52Float8 x, Mini52Float8 y)
        =>(Mini52Float8) MathF.MinMagnitude((float) x, (float) y);

    /// <summary>
    /// MinMagnitudeNumber
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns x or y based on smaller of Abs(x) vs Abs(Y)</returns>
    public static Mini52Float8 MinMagnitudeNumber(Mini52Float8 x, Mini52Float8 y)
    {
        Mini52Float8 bf1 = Mini52Float8.Abs(x);
        Mini52Float8 bf2 = Mini52Float8.Abs(y);
        return bf1 < bf2 || Mini52Float8.IsNaN(bf2) ||
               bf1 == bf2 && Mini52Float8.IsNegative(x) ? x : y;
    }



    /// <summary>
    /// Cast double to Mini52Float8
    /// </summary>
    /// <param name="value">Half value to convert</param>
    /// <returns>Mini52Float8</returns>
    public static explicit operator Mini52Float8(Half value)
        => HalfToMini52Float8(value);




    /// <summary>
    /// Parse string
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formating attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Parsed Mini52Float8 value when successful</returns>
    public static Mini52Float8 Parse(string s, NumberStyles style,
        IFormatProvider? provider)
        => (Mini52Float8)float.Parse(s, style, provider);




    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formating attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Mini52Float8 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider,
        out Mini52Float8 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (Mini52Float8)floatResult;
        return isGood;
    }


#if NET7_0_OR_GREATER


    /// <summary>
    /// Is value an integer?
    /// </summary>
    /// <param name="value">Mini52Float8 to test</param>
    /// <returns>True when integer</returns>
    public static bool IsInteger(Mini52Float8 value) => float.IsInteger((float)value);


    /// <summary>
    /// Is an even integer
    /// </summary>
    /// <param name="value">Mini52Float8 to test</param>
    /// <returns>True when an even integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEvenInteger(Mini52Float8 value) =>
         float.IsEvenInteger((float) value);

    /// <summary>
    /// Is odd integer?
    /// </summary>
    /// <param name="value">Mini52Float8 to test</param>
    /// <returns>True when off integer</returns>
    public static bool IsOddInteger(Mini52Float8 value)
        => float.IsOddInteger((float) value);


    /// <summary>
    /// TryConvertFrom
    /// </summary>
    /// <param name="value">Typed Value to convert</param>
    /// <param name="result">Mini52Float8 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFrom<TOther>(TOther value, out Mini52Float8 result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof (double))
        {
            double num = (double) (object) value;
            result = (Mini52Float8) num;
            return true;
        }
        if (ofOther == typeof (short))
        {
            short num = (short) (object) value;
            result = (Mini52Float8) num;
            return true;
        }
        if (ofOther == typeof (int))
        {
            int num = (int) (object) value;
            result = (Mini52Float8) num;
            return true;
        }
        if (ofOther == typeof (long))
        {
            long num = (long) (object) value;
            result = (Mini52Float8) num;
            return true;
        }
        if (ofOther == typeof (Int128))
        {
            Int128 int128 = (Int128) (object) value;
            result = (Mini52Float8)(float) int128;
            return true;
        }
        if (ofOther == typeof (IntPtr))
        {
            IntPtr num = (IntPtr) (object) value;
            result = (Mini52Float8) num;
            return true;
        }
        if (ofOther == typeof (sbyte))
        {
            sbyte num = (sbyte) (object) value;
            result = (Mini52Float8) num;
            return true;
        }
        if (ofOther == typeof (float))
        {
            float num = (float) (object) value;
            result = (Mini52Float8) num;
            return true;
        }
        if (ofOther == typeof (Half))
        {
            Half num = (Half) (object) value;
            result = (Mini52Float8) num;
            return true;
        }

        result = new Mini52Float8();
        return false;
    }


    /// <summary>
    /// TryConvertFromChecked
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">Mini52Float8 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromChecked<TOther>(TOther value,
        out Mini52Float8 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);

    /// <summary>
    /// TryConvertFromSaturating
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">Mini52Float8 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromSaturating<TOther>(TOther value,
        out Mini52Float8 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);


    /// <summary>
    /// TryConvertFromTruncating
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">BFlaot16 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromTruncating<TOther>(TOther value,
        out Mini52Float8 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);



    /// <summary>
    /// TryConvertTo
    /// </summary>
    /// <param name="value">Mini52Float8 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
     private static bool TryConvertTo<TOther>(Mini52Float8 value,
        [MaybeNullWhen(false)] out TOther result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof(byte))
        {
            byte num = value >= (Mini52Float8)byte.MaxValue
                ? byte.MaxValue
                : (value <= (Mini52Float8)(byte)0 ? (byte)0 : (byte)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(char))
        {
            char ch = value == Mini52Float8.PositiveInfinity
                ? char.MaxValue
                : (value <= Mini52Float8.Zero ? char.MinValue : (char)value);
            result = (TOther)Convert.ChangeType((ValueType)ch, ofOther);
            return true;
        }

        if (ofOther == typeof(decimal))
        {
            decimal num = value == Mini52Float8.PositiveInfinity
                ? decimal.MaxValue
                : (value == Mini52Float8.NegativeInfinity
                    ? decimal.MinValue
                    : (Mini52Float8.IsNaN(value) ? 0.0M : (decimal)(float)value));
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(ushort))
        {
            ushort num = value == Mini52Float8.PositiveInfinity
                ? ushort.MaxValue
                : (value <= Mini52Float8.Zero ? (ushort)0 : (ushort)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(uint))
        {
            uint num = value == Mini52Float8.PositiveInfinity
                ? uint.MaxValue
                : (value <= Mini52Float8.Zero ? 0U : (uint)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(ulong))
        {
            ulong num = value == Mini52Float8.PositiveInfinity
                ? ulong.MaxValue
                : (value <= Mini52Float8.Zero ? 0UL : (Mini52Float8.IsNaN(value) ?
                    0UL : (ulong)value));
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(UInt128))
        {
            UInt128 uint128 = value == Mini52Float8.PositiveInfinity
                ? UInt128.MaxValue
                : (value <= Mini52Float8.Zero ? UInt128.MinValue : (UInt128)(float)value);
            result = (TOther)Convert.ChangeType((ValueType)uint128, ofOther);
            return true;
        }

        if (ofOther == typeof(UIntPtr))
        {
            UIntPtr num = value == Mini52Float8.PositiveInfinity
                ? UIntPtr.MaxValue
                : (value <= Mini52Float8.Zero ? UIntPtr.MinValue : (UIntPtr)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(Half))
        {
            float num = (float)value;  // Direct conversion to float
            result = (TOther)Convert.ChangeType((Half) num, ofOther);
            return true;
        }


        if (ofOther == typeof(float))
        {
            float num = (float)value;  // Direct conversion to float
            result = (TOther)Convert.ChangeType(num, ofOther);
            return true;
        }

        if (ofOther == typeof(double))
        {
            double num = (double)value;  // Direct conversion to double
            result = (TOther)Convert.ChangeType(num, ofOther);
            return true;
        }

        result = default(TOther);
        return false;
    }

     #nullable disable

    /// <summary>
    /// TryConvertToChecked
    /// </summary>
    /// <param name="value">Mini52Float8 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToChecked<TOther>(Mini52Float8 value, out TOther result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof (byte))
        {
            byte num = checked ((byte) value);
            result = (TOther) Convert.ChangeType((ValueType) num, ofOther);
            return true;
        }
        if (ofOther == typeof (char))
        {
            char ch = checked ((char) value);
            result = (TOther)  Convert.ChangeType((ValueType) ch, ofOther);
            return true;
        }
        if (ofOther == typeof (decimal))
        {
            decimal num = (decimal) (float) value;
            result = (TOther)  Convert.ChangeType((ValueType) num, ofOther);;
            return true;
        }
        if (ofOther == typeof(Mini52Float8))
        {
            float num = checked((float)value);  // Direct conversion to float
            result = (TOther)Convert.ChangeType(num, ofOther);
            return true;
        }
        if (ofOther == typeof(float))
        {
            float num = checked((float)value);  // Direct conversion to float
            result = (TOther)Convert.ChangeType(num, ofOther);
            return true;
        }

        if (ofOther == typeof(double))
        {
            double num = checked((double)value);  // Direct conversion to double
            result = (TOther)Convert.ChangeType(num, ofOther);
            return true;
        }

        if (ofOther == typeof (ushort))
        {
            ushort num = checked ((ushort) value);
            result = (TOther)  Convert.ChangeType((ValueType) num, ofOther);;
            return true;
        }
        if (ofOther == typeof (uint))
        {
            uint num = checked ((uint) value);
            result = (TOther)  Convert.ChangeType((ValueType) num, ofOther);;
            return true;
        }
        if (ofOther == typeof (ulong))
        {
            ulong num = checked ((ulong) value);
            result = (TOther)  Convert.ChangeType((ValueType) num, ofOther);;
            return true;
        }
        if (ofOther == typeof (UInt128))
        {
            UInt128 uint128 = checked ((UInt128) (float) value);
            result = (TOther)  Convert.ChangeType((ValueType) uint128, ofOther);;
            return true;
        }
        if (ofOther == typeof (UIntPtr))
        {
            UIntPtr num = checked ((UIntPtr) value);
            result = (TOther) Convert.ChangeType((ValueType) num, ofOther);
            return true;
        }
        result = default (TOther);
        return false;
    }

    /// <summary>
    /// TryConvertToSaturating
    /// </summary>
    /// <param name="value">Mini52Float8 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToSaturating<TOther>(Mini52Float8 value,
        out TOther result) where TOther : INumberBase<TOther>
        =>  Mini52Float8.TryConvertTo<TOther>(value, out result);

    /// <summary>
    /// TryConvertToTruncating
    /// </summary>
    /// <param name="value">Mini52Float8 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToTruncating<TOther>(Mini52Float8 value,
        out TOther result) where TOther : INumberBase<TOther>
        =>  Mini52Float8.TryConvertTo<TOther>(value, out result);

    #nullable enable

#endif
    #endregion

}

