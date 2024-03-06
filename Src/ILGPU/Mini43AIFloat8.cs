// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Mini43AIFloat8.cs
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
/// Mini43AIFloat8 Implementation -
/// No Infinities, adds magnitudes 256, 288,320,384,416,448
/// </summary>
public readonly struct Mini43AIFloat8
#if NET7_0_OR_GREATER
    : INumber<Mini43AIFloat8>
#else
    : IComparable, IEquatable<Mini43AIFloat8>, IComparable<Mini43AIFloat8>
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
    public static Mini43AIFloat8 Zero  {get; } = new Mini43AIFloat8(0x00);

    /// <summary>
    /// One
    /// </summary>

    public static Mini43AIFloat8 One { get; } = new Mini43AIFloat8(0x38);


    /// <summary>
    /// Represents positive infinity.
    /// </summary>

    public static Mini43AIFloat8 PositiveInfinity { get; } = NaN;
    /// <summary>
    /// Represents negative infinity.
    /// </summary>
    public static Mini43AIFloat8 NegativeInfinity{ get; } = NaN;

    /// <summary>
    /// Epsilon - smallest positive value
    /// </summary>
    public static Mini43AIFloat8 Epsilon { get; } = new Mini43AIFloat8(0x08);

    /// <summary>
    /// MaxValue - most positive value
    /// </summary>
    public static Mini43AIFloat8 MaxValue { get; } = new Mini43AIFloat8(0x77);

    /// <summary>
    /// MinValue ~ most negative value
    /// </summary>
    public static Mini43AIFloat8 MinValue { get; } = new Mini43AIFloat8(0xF7);

    /// <summary>
    /// NaN ~ value with all exponent bits set to 1 and a non-zero mantissa
    /// </summary>
    public static Mini43AIFloat8 NaN { get; } = new Mini43AIFloat8(0xFF);

    #endregion

    #region Comparable



    /// <summary>
    /// CompareTo
    /// </summary>
    /// <param name="obj">Object to compare</param>
    /// <returns>Zero when equal</returns>
    /// <exception cref="ArgumentException">Thrown when not Mini43AIFloat8</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(object? obj)
    {
        if (obj is Mini43AIFloat8 other)
            return CompareTo((Mini43AIFloat8)other);
        if (obj != null)
            throw new ArgumentException("Must be " + nameof(Mini43AIFloat8));
        return 1;
    }



    /// <summary>
    /// CompareTo
    /// </summary>
    /// <param name="other">Mini43AIFloat8 to compare</param>
    /// <returns>Zero when successful</returns>
    public int CompareTo(Mini43AIFloat8 other) => ((float)this).CompareTo(other);

    #endregion

    #region Equality




    /// <summary>
    /// Equals
    /// </summary>
    /// <param name="obj">Object to compare</param>
    /// <returns>bool</returns>
    public readonly override bool Equals(object? obj) =>
        obj is Mini43AIFloat8 Mini43AIFloat8 && Equals(Mini43AIFloat8);

    /// <summary>
    /// Equals
    /// </summary>
    /// <param name="other">Other to compare</param>
    /// <returns>True when successful</returns>
    public bool Equals(Mini43AIFloat8 other) => this == other;

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
    /// <param name="first">First Mini43AIFloat8 value</param>
    /// <param name="second">Second Mini43AIFloat8 value</param>
    /// <returns>True when equal</returns>
    [CompareIntrinisc(CompareKind.Equal)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Mini43AIFloat8 first, Mini43AIFloat8 second) =>
        (byte)Unsafe.As<Mini43AIFloat8, byte>(ref first) ==
        (byte)Unsafe.As<Mini43AIFloat8, byte>(ref second);


    /// <summary>
    /// Operator Not Equals
    /// </summary>
    /// <param name="first">First Mini43AIFloat8 value</param>
    /// <param name="second">Second Mini43AIFloat8 value</param>
    /// <returns>True when not equal</returns>
    [CompareIntrinisc(CompareKind.NotEqual)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator != (Mini43AIFloat8 first, Mini43AIFloat8 second) =>
        (byte)Unsafe.As<Mini43AIFloat8, byte>(ref first) !=
        (byte)Unsafe.As<Mini43AIFloat8, byte>(ref second);


    /// <summary>
    /// Operator less than
    /// </summary>
    /// <param name="first">First Mini43AIFloat8 value</param>
    /// <param name="second">Second Mini43AIFloat8 value</param>
    /// <returns>True when less than</returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Mini43AIFloat8 first, Mini43AIFloat8 second) =>
        (float)first < (float)second;


    /// <summary>
    /// Operator less than or equals
    /// </summary>
    /// <param name="first">First Mini43AIFloat8 value</param>
    /// <param name="second">Second Mini43AIFloat8 value</param>
    /// <returns>True when less than or equal</returns>
    [CompareIntrinisc(CompareKind.LessEqual)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Mini43AIFloat8 first, Mini43AIFloat8 second) =>
        (float)first <= (float)second;


    /// <summary>
    /// Operator greater than
    /// </summary>
    /// <param name="first">First Mini43AIFloat8 value</param>
    /// <param name="second">Second Mini43AIFloat8 value</param>
    /// <returns>True when greater than</returns>
    [CompareIntrinisc(CompareKind.GreaterThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Mini43AIFloat8 first, Mini43AIFloat8 second) =>
        (float)first > (float)second;


    /// <summary>
    /// Operator greater than or equals
    /// </summary>
    /// <param name="first">First Mini43AIFloat8 value</param>
    /// <param name="second">Second Mini43AIFloat8 value</param>
    /// <returns>True when greater than or equal</returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Mini43AIFloat8 first, Mini43AIFloat8 second) =>
        (float)first >= (float)second;



    #endregion


    #region AdditionAndIncrement

    /// <summary>
    /// Increment operator
    /// </summary>
    /// <param name="value">Mini43AIFloat8 value to increment</param>
    /// <returns>Incremented value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 operator ++(Mini43AIFloat8 value)
        => (Mini43AIFloat8) ((float) value + 1f);



    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="value">Mini43AIFloat8 self to add</param>
    /// <returns>Mini43AIFloat8 value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 operator +(Mini43AIFloat8 value) => value;

    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="left">First Mini43AIFloat8 value</param>
    /// <param name="right">Second Mini43AIFloat8 value</param>
    /// <returns>Returns addition</returns>
    public static Mini43AIFloat8 operator +(Mini43AIFloat8 left, Mini43AIFloat8 right)
        => Mini43AIFloat8Extensions.AddFP32(left, right);


#if NET7_0_OR_GREATER

    /// <summary>
    /// IAdditiveIdentity
    /// </summary>
    static Mini43AIFloat8
        IAdditiveIdentity<Mini43AIFloat8, Mini43AIFloat8>.AdditiveIdentity
        => new Mini43AIFloat8((byte) 0);
#endif

    #endregion


    #region DecrementAndSubtraction

    /// <summary>
    /// Decrement operator
    /// </summary>
    /// <param name="value">Value to be decremented by 1</param>
    /// <returns>Decremented value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 operator --(Mini43AIFloat8 value)
        => (Mini43AIFloat8) ((float) value - 1f);


    /// <summary>
    /// Subtraction
    /// </summary>
    /// <param name="left">First Mini43AIFloat8 value</param>
    /// <param name="right">Second Mini43AIFloat8 value</param>
    /// <returns>left - right</returns>
    public static Mini43AIFloat8 operator -(Mini43AIFloat8 left, Mini43AIFloat8 right)
        => Mini43AIFloat8Extensions.SubFP32(left, right);




    /// <summary>
    /// Negation
    /// </summary>
    /// <param name="value">Mini43AIFloat8 to Negate</param>
    /// <returns>Negated value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 operator -(Mini43AIFloat8 value)
        => Mini43AIFloat8Extensions.Neg(value);

    #endregion


    #region MultiplicationDivisionAndModulus

    /// <summary>
    /// Multiplication
    /// </summary>
    /// <param name="left">First Mini43AIFloat8 value</param>
    /// <param name="right">Second Mini43AIFloat8 value</param>
    /// <returns>Multiplication of left * right</returns>
    public static Mini43AIFloat8 operator *(Mini43AIFloat8 left, Mini43AIFloat8 right)
        => Mini43AIFloat8Extensions.MulFP32(left,right);

    /// <summary>
    /// Division
    /// </summary>
    /// <param name="left">First Mini43AIFloat8 value</param>
    /// <param name="right">Second Mini43AIFloat8 value</param>
    /// <returns>Left / right</returns>
    public static Mini43AIFloat8 operator /(Mini43AIFloat8 left, Mini43AIFloat8 right)
        => Mini43AIFloat8Extensions.DivFP32(left, right);


    /// <summary>
    /// Multiplicative Identity
    /// </summary>
    public static Mini43AIFloat8 MultiplicativeIdentity  => new Mini43AIFloat8(0x31);

    /// <summary>
    /// Modulus operator
    /// </summary>
    /// <param name="left">First Mini43AIFloat8 value</param>
    /// <param name="right">Second Mini43AIFloat8 value</param>
    /// <returns>Left modulus right</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 operator %(Mini43AIFloat8 left, Mini43AIFloat8 right)
        =>  (Mini43AIFloat8) ((float) left % (float) right);


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
    /// Parse string to Mini43AIFloat8
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Mini43AIFloat8 value when successful</returns>
    public static Mini43AIFloat8 Parse(string s, IFormatProvider? provider)
        => (Mini43AIFloat8)float.Parse(s,
            NumberStyles.Float | NumberStyles.AllowThousands, provider);

    /// <summary>
    /// TryParse string to Mini43AIFloat8
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Mini43AIFloat8 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(string? s, IFormatProvider? provider,
        out Mini43AIFloat8 result)
    {
        float value;
        bool itWorked = float.TryParse(s,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (Mini43AIFloat8)value;
        return itWorked;

    }

#if NET7_0_OR_GREATER

    /// <summary>
    /// Parse Span of char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Value if parsed successfully</returns>
    public static Mini43AIFloat8 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => (Mini43AIFloat8) float.Parse(s, provider);


    /// <summary>
    /// TryParse Span of char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Mini43AIFloat8 out param</param>
    /// <returns>True if parsed successfully</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider,
        out Mini43AIFloat8 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, provider, out floatResult);
        result = (Mini43AIFloat8)floatResult;
        return isGood;
    }



    /// <summary>
    /// Parse Span char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Value if parsed successfully</returns>
    public static Mini43AIFloat8 Parse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider)
        => (Mini43AIFloat8)float.Parse(s, style, provider);

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Mini43AIFloat8 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider, out Mini43AIFloat8 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (Mini43AIFloat8)floatResult;
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
    public static Mini43AIFloat8 Parse(ReadOnlySpan<byte> utf8Text,
        IFormatProvider? provider)
        => (Mini43AIFloat8) float.Parse(utf8Text, provider);

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="utf8Text">Utf8 encoded byte span to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Mini43AIFloat8 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider,
        out Mini43AIFloat8 result)
    {
        float value;
        bool itWorked = float.TryParse(utf8Text,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (Mini43AIFloat8)value;
        return itWorked;
    }

#endif

    #endregion

    #region object

    internal byte RawValue { get; }


    /// <summary>
    /// create Mini43AIFloat8 from byte
    /// </summary>
    /// <param name="rawValue">byte encoded minifloat value</param>
    public Mini43AIFloat8(byte rawValue)
    {
        RawValue = rawValue;
    }


    /// <summary>
    /// Raw value
    /// </summary>
    /// <returns>internal byte value</returns>
    public byte AsByte => RawValue;

   #endregion


   #region Conversions


   private static readonly float[] MiniFloatToFloatLookup
       = GenerateMiniFloatToFloatLookup();

   private static float[] GenerateMiniFloatToFloatLookup()
   {
       float[] result = new float[256];
       for (int i = 0; i < 256; i++)
       {
           result[i] = Mini43AIFloat8Extensions.ByteToSingleForMiniFloat43AI((byte)i);
       }

       return result;
   }

   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private static float Mini43AIFloat8ToSingle(Mini43AIFloat8 mini43AiFloat8)
        => MiniFloatToFloatLookup[mini43AiFloat8.RawValue];

   private static readonly byte[] ExponentToMiniLookupTable
       = GenerateToMiniExponentLookupTable();

    // Generates the lookup table for exponent conversion from
    // single-precision float to Mini52Float8 format.
    // combining the sign with the exponent saves 15%
    private static byte[] GenerateToMiniExponentLookupTable()
    {
        byte[] table = new byte[512]; // 8-bit exponent can have 256 different values
        for (int i = 0; i < 256; i++)
        {
            // Adjusting from IEEE 754 single-precision bias (127)
            // to Mini43AIFloat8 bias (7)
            int adjustedExponent = i - 127 + 7; // Correct adjustment
                                                // for Mini43AIFloat8 format
            // Clamp to [0, 15] for 4-bit exponent
            adjustedExponent = Math.Max(0, Math.Min(15, adjustedExponent));
            table[i] = (byte)(adjustedExponent<<3);
            // negative sign bit
            table[i+256] = (byte)(adjustedExponent<<3 | 0x80);
        }
        return table;
    }

    /// <summary>
    /// Convert float to Mini43AIFloat8
    /// </summary>
    /// <param name="value">float value to convert</param>
    /// <returns>Value converted to Mini43AIFloat8</returns>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Mini43AIFloat8 SingleToMini43AIFloat8(float value)
    {
        if (value > 448)
        {
            return MaxValue;
        }

        if (value < -448)
        {
            return MinValue;
        }

        // Extracting the binary representation of the float value
        uint floatBits = Unsafe.As<float, uint>(ref value);

        ushort exponentIndex = (ushort)(floatBits >> 23);
        // Extract 8-bit exponent + sign

        // Extract mantissa bits for rounding
        uint mantissaBits = (floatBits & 0x007FFFFF);

        if ((exponentIndex & 0x00FF) == 0x00FF)
        {
            if (mantissaBits == 0) // No Infinity
            {
                return NaN;
            }
        }


        // Using the lookup table to convert the exponent
        byte exponent = ExponentToMiniLookupTable[exponentIndex];
        // Convert using the lookup table

        byte mantissa = (byte)((mantissaBits >> 20) & 0x7); // Direct extraction
        //byte roundBit = (byte)((mantissaBits >> 19) & 0x1);

        bool roundBit = (mantissaBits & 0x80000) != 0;
        // 0(000 0000 0)|(000) (X)000 0000 0000 0000 0000

        // Rounding, note the .Net optimizer comes up with the same speed no
        // matter how this is expressed
        if (roundBit)
        {
            bool stickyBit = (mantissaBits & 0x0007FFFF) > 0;

            if (stickyBit || (mantissa & 0x1) == 1)
            {
                mantissa++;
                if (mantissa == 0x8)
                {
                    mantissa = 0;
                    // Simplified rounding
                    if (0x78 > ((exponent + 0x08) & 0x78))
                    {
                        //0111 1000 = 78 - 4 bit mantissa
                        exponent =(byte) (exponent + 0x08);

                    }
                    else
                    {
                        exponent = (byte) (exponent | 0x78);
                    }
                }
            }
        }

        // Combining into Mini43AIFloat8 format
        // (1 bit sign, 5 bits exponent, 2 bits mantissa)
        byte Mini43AIFloat8 = (byte)(exponent | mantissa);

        return new Mini43AIFloat8(Mini43AIFloat8);
    }



    #endregion

    #region operators

    /// <summary>
    /// Cast float to Mini43AIFloat8
    /// </summary>
    /// <param name="value">float to cast</param>
    /// <returns>Mini43AIFloat8</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Mini43AIFloat8(float value)
        => SingleToMini43AIFloat8(value);


    /// <summary>
    /// Cast double to Mini43AIFloat8
    /// </summary>
    /// <param name="value">double to cast</param>
    /// <returns>Mini43AIFloat8</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Mini43AIFloat8(double value)
        => SingleToMini43AIFloat8((float) value);

    /// <summary>
    /// Cast Mini43AIFloat8 to Half
    /// </summary>
    /// <param name="value">Mini43AIFloat8 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Half(Mini43AIFloat8 value)
        => (Half) Mini43AIFloat8ToSingle(value);


    /// <summary>
    /// Cast Mini43AIFloat8 to Mini52Float8
    /// </summary>
    /// <param name="value">Mini43AIFloat8 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Mini52Float8(Mini43AIFloat8 value)
        => (Mini52Float8) Mini43AIFloat8ToSingle(value);

    /// <summary>
    /// Cast Mini43AIFloat8 to BFloat16
    /// </summary>
    /// <param name="value">Mini43AIFloat8 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BFloat16(Mini43AIFloat8 value)
        => (BFloat16) Mini43AIFloat8ToSingle(value);


    /// <summary>
    /// Cast Mini43AIFloat8 to float
    /// </summary>
    /// <param name="value">Mini43AIFloat8 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(Mini43AIFloat8 value)
        => Mini43AIFloat8ToSingle(value);

    /// <summary>
    /// Cast Mini43AIFloat8 to double
    /// </summary>
    /// <param name="value">Mini43AIFloat8 value to cast</param>
    /// <returns>double</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(Mini43AIFloat8 value) =>
        (double)Mini43AIFloat8ToSingle(value);


    #endregion

    #region INumberbase




    /// <summary>
    /// Absolute Value
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>Absolute of Mini43AIFloat8</returns>
    [MathIntrinsic(MathIntrinsicKind.Abs)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43AIFloat8 Abs(Mini43AIFloat8 value)
        => Mini43AIFloat8Extensions.Abs(value);

    /// <summary>
    /// Is Mini43AIFloat8 Canonical
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>True based on type</returns>
    public static bool IsCanonical(Mini43AIFloat8 value) => true;


    /// <summary>
    /// Is Mini43AIFloat8 a complex number
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>False based on type</returns>
    public static bool IsComplexNumber(Mini43AIFloat8 value) => false;


    /// <summary>
    /// Is value finite
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>True when finite</returns>
    public static bool IsFinite(Mini43AIFloat8 value)
        => !IsNaN(value);

    /// <summary>
    /// Is imaginary number
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>False based on type</returns>
    public static bool IsImaginaryNumber(Mini43AIFloat8 value) => false;

    /// <summary>
    /// Is an infinite value?
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>True when finite</returns>
    public static bool IsInfinity(Mini43AIFloat8 value) =>
        false;



    /// <summary>
    /// Is NaN
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>True when NaN</returns>
    public static bool IsNaN(Mini43AIFloat8 value) =>
        ((value.RawValue & 0x70) == 0x70) && ((value.RawValue & 0x0F) != 0);


    /// <summary>
    /// Is negative?
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>True when negative</returns>
    public static bool IsNegative(Mini43AIFloat8 value) => (value.RawValue & 0x80) != 0;


    /// <summary>
    /// Is negative infinity
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>True when negative infinity</returns>
    public static bool IsNegativeInfinity(Mini43AIFloat8 value) => false;

    /// <summary>
    /// Is normal
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>True when normal</returns>
    public static bool IsNormal(Mini43AIFloat8 value)
    {
        byte num =  (byte)(value.RawValue & 0x80);
        return num < 0x70 && num != 0 && (num & 0x70) != 0;
    }



    /// <summary>
    /// Is positive?
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>True when positive</returns>
    public static bool IsPositive(Mini43AIFloat8 value) => (value.RawValue & 0x8000) == 0;

    /// <summary>
    /// Is positive infinity?
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>True when positive</returns>
    public static bool IsPositiveInfinity(Mini43AIFloat8 value) => false;

    /// <summary>
    /// Is real number
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>True when real number</returns>
    public static bool IsRealNumber(Mini43AIFloat8 value)
    {
        bool isExponentAllOnes = (value.RawValue & Mini43AIFloat8Extensions.ExponentMask)
                                 == Mini43AIFloat8Extensions.ExponentMask;
        bool isMantissaNonZero
            = (value.RawValue & Mini43AIFloat8Extensions.MantissaMask) != 0;

        // If exponent is all ones and mantissa is non-zero, it's NaN, so return false
        return !(isExponentAllOnes && isMantissaNonZero);
    }

    /// <summary>
    /// Is subnormal
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>True when subnormal</returns>
    public static bool IsSubnormal(Mini43AIFloat8 value)
        => (value.RawValue & 0x7F80) == 0 && (value.RawValue & 0x007F) != 0;

    /// <summary>
    /// Is Zero?
    /// </summary>
    /// <param name="value">Mini43AIFloat8</param>
    /// <returns>True when Zero</returns>
    public static bool IsZero(Mini43AIFloat8 value)
        => (value.RawValue & Mini43AIFloat8Extensions.ExponentMantissaMask) == 0;

    /// <summary>
    /// MaxMagnitude
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns larger of x or y, NaN when equal</returns>
    public static Mini43AIFloat8 MaxMagnitude(Mini43AIFloat8 x, Mini43AIFloat8 y)
        =>(Mini43AIFloat8) MathF.MaxMagnitude((float) x, (float) y);

    /// <summary>
    /// MaxMagnitudeNumber
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns x or y based on larger of Abs(x) vs Abs(Y) </returns>
    public static Mini43AIFloat8 MaxMagnitudeNumber(Mini43AIFloat8 x, Mini43AIFloat8 y)
    {
        Mini43AIFloat8 bf1 = Mini43AIFloat8.Abs(x);
        Mini43AIFloat8 bf2 = Mini43AIFloat8.Abs(y);
        return bf1 > bf2 || Mini43AIFloat8.IsNaN(bf2) || bf1
            == bf2 && !Mini43AIFloat8.IsNegative(x) ? x : y;
    }

    /// <summary>
    /// MinMagnitude
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>returns smaller of x or y or NaN when equal</returns>
    public static Mini43AIFloat8 MinMagnitude(Mini43AIFloat8 x, Mini43AIFloat8 y)
        =>(Mini43AIFloat8) MathF.MinMagnitude((float) x, (float) y);

    /// <summary>
    /// MinMagnitudeNumber
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns x or y based on smaller of Abs(x) vs Abs(Y)</returns>
    public static Mini43AIFloat8 MinMagnitudeNumber(Mini43AIFloat8 x, Mini43AIFloat8 y)
    {
        Mini43AIFloat8 bf1 = Mini43AIFloat8.Abs(x);
        Mini43AIFloat8 bf2 = Mini43AIFloat8.Abs(y);
        return bf1 < bf2 || Mini43AIFloat8.IsNaN(bf2) ||
               bf1 == bf2 && Mini43AIFloat8.IsNegative(x) ? x : y;
    }



    /// <summary>
    /// Cast double to Mini43AIFloat8
    /// </summary>
    /// <param name="value">Half value to convert</param>
    /// <returns>Mini43AIFloat8</returns>
    public static explicit operator Mini43AIFloat8(Half value)
        => SingleToMini43AIFloat8((float) value);




    /// <summary>
    /// Parse string
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Parsed Mini43AIFloat8 value when successful</returns>
    public static Mini43AIFloat8 Parse(string s, NumberStyles style,
        IFormatProvider? provider)
        => (Mini43AIFloat8)float.Parse(s, style, provider);




    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Mini43AIFloat8 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider,
        out Mini43AIFloat8 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (Mini43AIFloat8)floatResult;
        return isGood;
    }


#if NET7_0_OR_GREATER


    /// <summary>
    /// Is value an integer?
    /// </summary>
    /// <param name="value">Mini43AIFloat8 to test</param>
    /// <returns>True when integer</returns>
    public static bool IsInteger(Mini43AIFloat8 value) => float.IsInteger((float)value);


    /// <summary>
    /// Is an even integer
    /// </summary>
    /// <param name="value">Mini43AIFloat8 to test</param>
    /// <returns>True when an even integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEvenInteger(Mini43AIFloat8 value) =>
         float.IsEvenInteger((float) value);

    /// <summary>
    /// Is odd integer?
    /// </summary>
    /// <param name="value">Mini43AIFloat8 to test</param>
    /// <returns>True when off integer</returns>
    public static bool IsOddInteger(Mini43AIFloat8 value)
        => float.IsOddInteger((float) value);


    /// <summary>
    /// TryConvertFrom
    /// </summary>
    /// <param name="value">Typed Value to convert</param>
    /// <param name="result">Mini43AIFloat8 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFrom<TOther>(TOther value, out Mini43AIFloat8 result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof (double))
        {
            double num = (double) (object) value;
            result = (Mini43AIFloat8) num;
            return true;
        }
        if (ofOther == typeof (short))
        {
            short num = (short) (object) value;
            result = (Mini43AIFloat8) num;
            return true;
        }
        if (ofOther == typeof (int))
        {
            int num = (int) (object) value;
            result = (Mini43AIFloat8) num;
            return true;
        }
        if (ofOther == typeof (long))
        {
            long num = (long) (object) value;
            result = (Mini43AIFloat8) num;
            return true;
        }
        if (ofOther == typeof (Int128))
        {
            Int128 int128 = (Int128) (object) value;
            result = (Mini43AIFloat8)(float) int128;
            return true;
        }
        if (ofOther == typeof (IntPtr))
        {
            IntPtr num = (IntPtr) (object) value;
            result = (Mini43AIFloat8) num;
            return true;
        }
        if (ofOther == typeof (sbyte))
        {
            sbyte num = (sbyte) (object) value;
            result = (Mini43AIFloat8) num;
            return true;
        }
        if (ofOther == typeof (float))
        {
            float num = (float) (object) value;
            result = (Mini43AIFloat8) num;
            return true;
        }
        if (ofOther == typeof (Half))
        {
            Half num = (Half) (object) value;
            result = (Mini43AIFloat8) num;
            return true;
        }

        result = new Mini43AIFloat8();
        return false;
    }


    /// <summary>
    /// TryConvertFromChecked
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">Mini43AIFloat8 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromChecked<TOther>(TOther value,
        out Mini43AIFloat8 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);

    /// <summary>
    /// TryConvertFromSaturating
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">Mini43AIFloat8 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromSaturating<TOther>(TOther value,
        out Mini43AIFloat8 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);


    /// <summary>
    /// TryConvertFromTruncating
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">BFlaot16 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromTruncating<TOther>(TOther value,
        out Mini43AIFloat8 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);



    /// <summary>
    /// TryConvertTo
    /// </summary>
    /// <param name="value">Mini43AIFloat8 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
     private static bool TryConvertTo<TOther>(Mini43AIFloat8 value,
        [MaybeNullWhen(false)] out TOther result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof(byte))
        {
            byte num = value >= (Mini43AIFloat8)byte.MaxValue
                ? byte.MaxValue
                : (value <= (Mini43AIFloat8)(byte)0 ? (byte)0 : (byte)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(char))
        {
            char ch = value == Mini43AIFloat8.PositiveInfinity
                ? char.MaxValue
                : (value <= Mini43AIFloat8.Zero ? char.MinValue : (char)value);
            result = (TOther)Convert.ChangeType((ValueType)ch, ofOther);
            return true;
        }

        if (ofOther == typeof(decimal))
        {
            decimal num = value == Mini43AIFloat8.PositiveInfinity
                ? decimal.MaxValue
                : (value == Mini43AIFloat8.NegativeInfinity
                    ? decimal.MinValue
                    : (Mini43AIFloat8.IsNaN(value) ? 0.0M : (decimal)(float)value));
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(ushort))
        {
            ushort num = value == Mini43AIFloat8.PositiveInfinity
                ? ushort.MaxValue
                : (value <= Mini43AIFloat8.Zero ? (ushort)0 : (ushort)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(uint))
        {
            uint num = value == Mini43AIFloat8.PositiveInfinity
                ? uint.MaxValue
                : (value <= Mini43AIFloat8.Zero ? 0U : (uint)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(ulong))
        {
            ulong num = value == Mini43AIFloat8.PositiveInfinity
                ? ulong.MaxValue
                : (value <= Mini43AIFloat8.Zero ? 0UL : (Mini43AIFloat8.IsNaN(value) ?
                    0UL : (ulong)value));
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(UInt128))
        {
            UInt128 uint128 = value == Mini43AIFloat8.PositiveInfinity
                ? UInt128.MaxValue
                : (value <= Mini43AIFloat8.Zero ?
                    UInt128.MinValue : (UInt128)(float)value);
            result = (TOther)Convert.ChangeType((ValueType)uint128, ofOther);
            return true;
        }

        if (ofOther == typeof(UIntPtr))
        {
            UIntPtr num = value == Mini43AIFloat8.PositiveInfinity
                ? UIntPtr.MaxValue
                : (value <= Mini43AIFloat8.Zero ? UIntPtr.MinValue : (UIntPtr)value);
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
    /// <param name="value">Mini43AIFloat8 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToChecked<TOther>(Mini43AIFloat8 value,
        out TOther result) where TOther : INumberBase<TOther>
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
        if (ofOther == typeof(Mini43AIFloat8))
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
    /// <param name="value">Mini43AIFloat8 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToSaturating<TOther>(Mini43AIFloat8 value,
        out TOther result) where TOther : INumberBase<TOther>
        =>  Mini43AIFloat8.TryConvertTo<TOther>(value, out result);

    /// <summary>
    /// TryConvertToTruncating
    /// </summary>
    /// <param name="value">Mini43AIFloat8 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToTruncating<TOther>(Mini43AIFloat8 value,
        out TOther result) where TOther : INumberBase<TOther>
        =>  Mini43AIFloat8.TryConvertTo<TOther>(value, out result);

    #nullable enable

#endif
    #endregion

}


