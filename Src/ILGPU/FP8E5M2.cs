// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: FP8E5M2.cs
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
/// FP8E5M2 Implementation
/// </summary>
public readonly struct FP8E5M2
#if NET7_0_OR_GREATER
    : INumber<FP8E5M2>
#else
    : IComparable, IEquatable<FP8E5M2>, IComparable<FP8E5M2>
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
    public static FP8E5M2 Zero  {get; } = new FP8E5M2(0x00);

    /// <summary>
    /// One
    /// </summary>

    public static FP8E5M2 One { get; } = new FP8E5M2(0x3C);


    /// <summary>
    /// Represents positive infinity.
    /// </summary>
    public static FP8E5M2 PositiveInfinity { get; } = new FP8E5M2(0xFC);

    /// <summary>
    /// Represents negative infinity.
    /// </summary>
    public static FP8E5M2 NegativeInfinity{ get; } = new FP8E5M2(0xFC);

    /// <summary>
    /// Epsilon - smallest positive value
    /// </summary>
    public static FP8E5M2 Epsilon { get; } = new FP8E5M2(0x04);

    /// <summary>
    /// MaxValue - most positive value
    /// </summary>
    public static FP8E5M2 MaxValue { get; } = new FP8E5M2(0x7B);

    /// <summary>
    /// MinValue ~ most negative value
    /// </summary>
    public static FP8E5M2 MinValue { get; } = new FP8E5M2(0xFB);

    /// <summary>
    /// NaN ~ value with all exponent bits set to 1 and a non-zero mantissa
    /// </summary>
    public static FP8E5M2 NaN { get; } = new FP8E5M2(0x7F);



    #endregion

    #region Comparable



    /// <summary>
    /// CompareTo
    /// </summary>
    /// <param name="obj">Object to compare</param>
    /// <returns>Zero when equal</returns>
    /// <exception cref="ArgumentException">Thrown when not FP8E5M2</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(object? obj)
    {
        if (obj is FP8E5M2 other)
            return CompareTo((FP8E5M2)other);
        if (obj != null)
            throw new ArgumentException("Must be " + nameof(FP8E5M2));
        return 1;
    }



    /// <summary>
    /// CompareTo
    /// </summary>
    /// <param name="other">FP8E5M2 to compare</param>
    /// <returns>Zero when successful</returns>
    public int CompareTo(FP8E5M2 other) => ((float)this).CompareTo(other);

    #endregion

    #region Equality




    /// <summary>
    /// Equals
    /// </summary>
    /// <param name="obj">Object to compare</param>
    /// <returns>bool</returns>
    public readonly override bool Equals(object? obj) =>
        obj is FP8E5M2 FP8E5M2 && Equals(FP8E5M2);

    /// <summary>
    /// Equals
    /// </summary>
    /// <param name="other">Other to compare</param>
    /// <returns>True when successful</returns>
    public bool Equals(FP8E5M2 other) => this == other;

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
    /// <param name="first">First FP8E5M2 value</param>
    /// <param name="second">Second FP8E5M2 value</param>
    /// <returns>True when equal</returns>
    [CompareIntrinisc(CompareKind.Equal)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(FP8E5M2 first, FP8E5M2 second) =>
        (byte)Unsafe.As<FP8E5M2, byte>(ref first) ==
        (byte)Unsafe.As<FP8E5M2, byte>(ref second);


    /// <summary>
    /// Operator Not Equals
    /// </summary>
    /// <param name="first">First FP8E5M2 value</param>
    /// <param name="second">Second FP8E5M2 value</param>
    /// <returns>True when not equal</returns>
    [CompareIntrinisc(CompareKind.NotEqual)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator != (FP8E5M2 first, FP8E5M2 second) =>
        (byte)Unsafe.As<FP8E5M2, byte>(ref first) !=
        (byte)Unsafe.As<FP8E5M2, byte>(ref second);


    /// <summary>
    /// Operator less than
    /// </summary>
    /// <param name="first">First FP8E5M2 value</param>
    /// <param name="second">Second FP8E5M2 value</param>
    /// <returns>True when less than</returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(FP8E5M2 first, FP8E5M2 second) =>
        (float)first < (float)second;


    /// <summary>
    /// Operator less than or equals
    /// </summary>
    /// <param name="first">First FP8E5M2 value</param>
    /// <param name="second">Second FP8E5M2 value</param>
    /// <returns>True when less than or equal</returns>
    [CompareIntrinisc(CompareKind.LessEqual)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(FP8E5M2 first, FP8E5M2 second) =>
        (float)first <= (float)second;


    /// <summary>
    /// Operator greater than
    /// </summary>
    /// <param name="first">First FP8E5M2 value</param>
    /// <param name="second">Second FP8E5M2 value</param>
    /// <returns>True when greater than</returns>
    [CompareIntrinisc(CompareKind.GreaterThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(FP8E5M2 first, FP8E5M2 second) =>
        (float)first > (float)second;


    /// <summary>
    /// Operator greater than or equals
    /// </summary>
    /// <param name="first">First FP8E5M2 value</param>
    /// <param name="second">Second FP8E5M2 value</param>
    /// <returns>True when greater than or equal</returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(FP8E5M2 first, FP8E5M2 second) =>
        (float)first >= (float)second;



    #endregion


    #region AdditionAndIncrement

    /// <summary>
    /// Increment operator
    /// </summary>
    /// <param name="value">FP8E5M2 value to increment</param>
    /// <returns>Incremented value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 operator ++(FP8E5M2 value)
        => (FP8E5M2) ((float) value + 1f);



    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="value">FP8E5M2 self to add</param>
    /// <returns>FP8E5M2 value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 operator +(FP8E5M2 value) => value;

    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="left">First FP8E5M2 value</param>
    /// <param name="right">Second FP8E5M2 value</param>
    /// <returns>Returns addition</returns>
    public static FP8E5M2 operator +(FP8E5M2 left, FP8E5M2 right)
        => FP8E5M2Extensions.AddFP32(left, right);


#if NET7_0_OR_GREATER

    /// <summary>
    /// IAdditiveIdentity
    /// </summary>
    static FP8E5M2 IAdditiveIdentity<FP8E5M2, FP8E5M2>.AdditiveIdentity
        => new FP8E5M2((byte) 0);
#endif

    #endregion


    #region DecrementAndSubtraction

    /// <summary>
    /// Decrement operator
    /// </summary>
    /// <param name="value">Value to be decremented by 1</param>
    /// <returns>Decremented value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 operator --(FP8E5M2 value)
        => (FP8E5M2) ((float) value - 1f);


    /// <summary>
    /// Subtraction
    /// </summary>
    /// <param name="left">First FP8E5M2 value</param>
    /// <param name="right">Second FP8E5M2 value</param>
    /// <returns>left - right</returns>
    public static FP8E5M2 operator -(FP8E5M2 left, FP8E5M2 right)
        => FP8E5M2Extensions.SubFP32(left, right);




    /// <summary>
    /// Negation
    /// </summary>
    /// <param name="value">FP8E5M2 to Negate</param>
    /// <returns>Negated value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 operator -(FP8E5M2 value)
        => FP8E5M2Extensions.Neg(value);

    #endregion


    #region MultiplicationDivisionAndModulus

    /// <summary>
    /// Multiplication
    /// </summary>
    /// <param name="left">First FP8E5M2 value</param>
    /// <param name="right">Second FP8E5M2 value</param>
    /// <returns>Multiplication of left * right</returns>
    public static FP8E5M2 operator *(FP8E5M2 left, FP8E5M2 right)
        => FP8E5M2Extensions.MulFP32(left,right);

    /// <summary>
    /// Division
    /// </summary>
    /// <param name="left">First FP8E5M2 value</param>
    /// <param name="right">Second FP8E5M2 value</param>
    /// <returns>Left / right</returns>
    public static FP8E5M2 operator /(FP8E5M2 left, FP8E5M2 right)
        => FP8E5M2Extensions.DivFP32(left, right);


    /// <summary>
    /// Multiplicative Identity
    /// </summary>
    public static FP8E5M2 MultiplicativeIdentity  => new FP8E5M2(0x39);

    /// <summary>
    /// Modulus operator
    /// </summary>
    /// <param name="left">First FP8E5M2 value</param>
    /// <param name="right">Second FP8E5M2 value</param>
    /// <returns>Left modulus right</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 operator %(FP8E5M2 left, FP8E5M2 right)
        =>  (FP8E5M2) ((float) left % (float) right);


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
    /// Parse string to FP8E5M2
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>FP8E5M2 value when successful</returns>
    public static FP8E5M2 Parse(string s, IFormatProvider? provider)
        => (FP8E5M2)float.Parse(s,
            NumberStyles.Float | NumberStyles.AllowThousands,
            provider);

    /// <summary>
    /// TryParse string to FP8E5M2
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">FP8E5M2 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(string? s, IFormatProvider? provider,
        out FP8E5M2 result)
    {
        float value;
        bool itWorked = float.TryParse(s,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (FP8E5M2)value;
        return itWorked;

    }

#if NET7_0_OR_GREATER

    /// <summary>
    /// Parse Span of char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Value if parsed successfully</returns>
    public static FP8E5M2 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => (FP8E5M2) float.Parse(s, provider);


    /// <summary>
    /// TryParse Span of char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">FP8E5M2 out param</param>
    /// <returns>True if parsed successfully</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider,
        out FP8E5M2 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, provider, out floatResult);
        result = (FP8E5M2)floatResult;
        return isGood;
    }



    /// <summary>
    /// Parse Span char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Value if parsed successfully</returns>
    public static FP8E5M2 Parse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider)
        => (FP8E5M2)float.Parse(s, style, provider);

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">FP8E5M2 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider, out FP8E5M2 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (FP8E5M2)floatResult;
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
    public static FP8E5M2 Parse(ReadOnlySpan<byte> utf8Text,
        IFormatProvider? provider)
        => (FP8E5M2) float.Parse(utf8Text, provider);

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="utf8Text">Utf8 encoded byte span to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">FP8E5M2 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider,
        out FP8E5M2 result)
    {
        float value;
        bool itWorked = float.TryParse(utf8Text,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (FP8E5M2)value;
        return itWorked;
    }

#endif

    #endregion

    #region object

    internal byte RawValue { get; }

    /// <summary>
    /// create FP8E5M2 from byte value
    /// </summary>
    /// <param name="rawValue">byte value to create new FP8E5M2</param>
    public FP8E5M2(byte rawValue)
    {
        RawValue = rawValue;
    }

    /// <summary>
    /// AsByte - returns internal value
    /// </summary>
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
           result[i] = FP8E5M2Extensions.ByteToSingleForFP8E5M2((byte)i);
       }

       return result;
   }



   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   private static float FP8E5M2ToSingle(FP8E5M2 FP8E5M2)
       => MiniFloatToFloatLookup[FP8E5M2.RawValue];




   private static readonly byte[] ExponentToMiniLookupTable
       = GenerateToMiniExponentLookupTable();

// Generates the lookup table for exponent conversion from
// single-precision float to FP8E5M2 format.
    private static byte[] GenerateToMiniExponentLookupTable()
    {
        byte[] table = new byte[512]; // 8-bit exponent field can have
                                      // 512 different values as sign is included
        for (int i = 0; i < 256; i++)
        {
            // Adjusting from single-precision bias (127) to FP8E5M2
            // bias (15) and clamping
            int adjustedExponent = i - 127 + 15; // Adjust for FP8E5M2 format
            adjustedExponent = Math.Max(0, Math.Min(31, adjustedExponent));
            // Clamp to [0, 31] for 5-bit exponent
            table[i] = (byte)(adjustedExponent<<2);
            // negative sign bit
            table[i+256] = (byte)(adjustedExponent << 2 | 0x80) ;
        }
        return table;
    }



    /// <summary>
    /// Convert float to FP8E5M2
    /// </summary>
    /// <param name="value">float value to convert</param>
    /// <returns>Value converted to FP8E5M2</returns>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FP8E5M2 SingleToFP8E5M2(float value)
    {
        // Extracting the binary representation of the float value
        uint floatBits = Unsafe.As<float, uint>(ref value);

        ushort exponentIndex = (ushort)(floatBits >> 23);
        // Extract sign + 8-bit exponent

        // Extract mantissa bits for rounding
        uint mantissaBits = (floatBits & 0x007FFFFF);

        if ((exponentIndex & 0x00FF) == 0x00FF)
        {
            if (mantissaBits == 0) // Infinity check
            {
                if ((floatBits & 0x80000000) != 0) // Positive Infinity
                    return FP8E5M2.NegativeInfinity;
                else // Negative Infinity
                    return FP8E5M2.PositiveInfinity;
            }
            else // NaN check
            {
                return FP8E5M2.NaN;
            }
        }

        // Using the lookup table to convert the exponent
        byte exponent = ExponentToMiniLookupTable[exponentIndex];
        // Convert using the lookup table

        byte mantissa = (byte)((mantissaBits >> 21) & 0x3); // Direct extraction
        // byte roundBit = (byte)((mantissaBits >> 20) & 0x1);

        bool roundBit = (mantissaBits & 0x100000) != 0;
        // 0(000 0000 0)|(00)(X) 0000 0000 0000 0000 0000

        // Rounding, note the .Net optimizer comes up with the same speed no
        // matter how this is expressed
        if (roundBit)
        {
            bool stickyBit = (mantissaBits & 0x0007FFFF) > 0;
            if (stickyBit || (mantissa & 0x1) == 1)
            {
                mantissa++;
                if (mantissa == 0x4)
                {
                    mantissa = 0;
                    if (0x7C > ((exponent + 0x04) & 0x7C))
                    {
                        // 0111 1100 = 7C - 2 bit mantissa
                        // Simplified handling for overflow
                        exponent =(byte) (exponent + 0x04);
                    }
                    else
                    {
                        exponent = (byte) (exponent | 0x7C);
                    }
                }
            }
        }

        // Combining into FP8E5M2 format
        // (1 bit sign bit + 5 bits exponent, 2 bits mantissa)
        byte result = (byte)(exponent | mantissa);

        return new FP8E5M2(result);
    }


    #endregion

    #region operators

    /// <summary>
    /// Cast float to FP8E5M2
    /// </summary>
    /// <param name="value">float to cast</param>
    /// <returns>FP8E5M2</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator FP8E5M2(float value)
        => SingleToFP8E5M2(value);


    /// <summary>
    /// Cast double to FP8E5M2
    /// </summary>
    /// <param name="value">double to cast</param>
    /// <returns>FP8E5M2</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator FP8E5M2(double value)
        => SingleToFP8E5M2((float) value);

    /// <summary>
    /// Cast FP8E5M2 to FP8E4M3
    /// </summary>
    /// <param name="value">FP8E5M2 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator FP8E4M3(FP8E5M2 value)
        => (FP8E4M3)FP8E5M2ToSingle(value);

    /// <summary>
    /// Cast FP8E5M2 to BF16
    /// </summary>
    /// <param name="value">FP8E5M2 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BF16(FP8E5M2 value)
        => (BF16)FP8E5M2ToSingle(value);

    /// <summary>
    /// Cast FP8E5M2 to Half
    /// </summary>
    /// <param name="value">FP8E5M2 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Half(FP8E5M2 value)
        => (Half)FP8E5M2ToSingle(value);


    /// <summary>
    /// Cast FP8E5M2 to float
    /// </summary>
    /// <param name="value">FP8E5M2 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(FP8E5M2 value)
        => FP8E5M2ToSingle(value);

    /// <summary>
    /// Cast FP8E5M2 to double
    /// </summary>
    /// <param name="value">FP8E5M2 value to cast</param>
    /// <returns>double</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(FP8E5M2 value) =>
        (double)FP8E5M2ToSingle(value);


    #endregion

    #region INumberbase




    /// <summary>
    /// Absolute Value
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>Absolute of FP8E5M2</returns>
    [MathIntrinsic(MathIntrinsicKind.Abs)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FP8E5M2 Abs(FP8E5M2 value)
        => FP8E5M2Extensions.Abs(value);

    /// <summary>
    /// Is FP8E5M2 Canonical
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>True based on type</returns>
    public static bool IsCanonical(FP8E5M2 value) => true;


    /// <summary>
    /// Is FP8E5M2 a complex number
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>False based on type</returns>
    public static bool IsComplexNumber(FP8E5M2 value) => false;


    /// <summary>
    /// Is value finite
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>True when finite</returns>
    public static bool IsFinite(FP8E5M2 value)
        => Bitwise.And(!IsNaN(value), !IsInfinity(value));

    /// <summary>
    /// Is imaginary number
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>False based on type</returns>
    public static bool IsImaginaryNumber(FP8E5M2 value) => false;

    /// <summary>
    /// Is an infinite value?
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>True when finite</returns>
    public static bool IsInfinity(FP8E5M2 value) =>
        (value.RawValue & 0x80) != 0 && (value.RawValue & 0x7F) == 0;



    /// <summary>
    /// Is NaN
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>True when NaN</returns>
    public static bool IsNaN(FP8E5M2 value) =>
        ((value.RawValue & 0x80) == 0x80) && ((value.RawValue & 0x7F) != 0);



    /// <summary>
    /// Is negative?
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>True when negative</returns>
    public static bool IsNegative(FP8E5M2 value) => (value.RawValue & 0x80) != 0;


    /// <summary>
    /// Is negative infinity
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>True when negative infinity</returns>
    public static bool IsNegativeInfinity(FP8E5M2 value)
        => value == NegativeInfinity;

    /// <summary>
    /// Is normal
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>True when normal</returns>
    public static bool IsNormal(FP8E5M2 value)
    {
        byte num = (byte)(value.RawValue & 0x7F);
        return num < 0x80 && num != 0 && (num & 0x80) != 0;
    }



    /// <summary>
    /// Is positive?
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>True when positive</returns>
    public static bool IsPositive(FP8E5M2 value) => (value.RawValue & 0x8000) == 0;

    /// <summary>
    /// Is positive infinity?
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>True when positive</returns>
    public static bool IsPositiveInfinity(FP8E5M2 value)
        => value == PositiveInfinity;

    /// <summary>
    /// Is real number
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>True when real number</returns>
    public static bool IsRealNumber(FP8E5M2 value)
    {
        bool isExponentAllOnes = (value.RawValue & FP8E5M2Extensions.ExponentMask)
                                 == FP8E5M2Extensions.ExponentMask;
        bool isMantissaNonZero =
            (value.RawValue & FP8E5M2Extensions.MantissaMask) != 0;

        // If exponent is all ones and mantissa is non-zero, it's NaN, so return false
        return !(isExponentAllOnes && isMantissaNonZero);
    }

    /// <summary>
    /// Is subnormal
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>True when subnormal</returns>
    public static bool IsSubnormal(FP8E5M2 value)
        => (value.RawValue & 0x7F80) == 0 && (value.RawValue & 0x007F) != 0;

    /// <summary>
    /// Is Zero?
    /// </summary>
    /// <param name="value">FP8E5M2</param>
    /// <returns>True when Zero</returns>
    public static bool IsZero(FP8E5M2 value)
        => (value.RawValue & FP8E5M2Extensions.ExponentMantissaMask) == 0;

    /// <summary>
    /// MaxMagnitude
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns larger of x or y, NaN when equal</returns>
    public static FP8E5M2 MaxMagnitude(FP8E5M2 x, FP8E5M2 y)
        =>(FP8E5M2) MathF.MaxMagnitude((float) x, (float) y);

    /// <summary>
    /// MaxMagnitudeNumber
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns x or y based on larger of Abs(x) vs Abs(Y) </returns>
    public static FP8E5M2 MaxMagnitudeNumber(FP8E5M2 x, FP8E5M2 y)
    {
        FP8E5M2 bf1 = FP8E5M2.Abs(x);
        FP8E5M2 bf2 = FP8E5M2.Abs(y);
        return bf1 > bf2 || FP8E5M2.IsNaN(bf2) || bf1
            == bf2 && !FP8E5M2.IsNegative(x) ? x : y;
    }

    /// <summary>
    /// MinMagnitude
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>returns smaller of x or y or NaN when equal</returns>
    public static FP8E5M2 MinMagnitude(FP8E5M2 x, FP8E5M2 y)
        =>(FP8E5M2) MathF.MinMagnitude((float) x, (float) y);

    /// <summary>
    /// MinMagnitudeNumber
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns x or y based on smaller of Abs(x) vs Abs(Y)</returns>
    public static FP8E5M2 MinMagnitudeNumber(FP8E5M2 x, FP8E5M2 y)
    {
        FP8E5M2 bf1 = FP8E5M2.Abs(x);
        FP8E5M2 bf2 = FP8E5M2.Abs(y);
        return bf1 < bf2 || FP8E5M2.IsNaN(bf2) ||
               bf1 == bf2 && FP8E5M2.IsNegative(x) ? x : y;
    }



    /// <summary>
    /// Cast double to FP8E5M2
    /// </summary>
    /// <param name="value">Half value to convert</param>
    /// <returns>FP8E5M2</returns>
    public static explicit operator FP8E5M2(Half value)
        => (FP8E5M2)((float) value);




    /// <summary>
    /// Parse string
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Parsed FP8E5M2 value when successful</returns>
    public static FP8E5M2 Parse(string s, NumberStyles style,
        IFormatProvider? provider)
        => (FP8E5M2)float.Parse(s, style, provider);




    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">FP8E5M2 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider,
        out FP8E5M2 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (FP8E5M2)floatResult;
        return isGood;
    }


#if NET7_0_OR_GREATER


    /// <summary>
    /// Is value an integer?
    /// </summary>
    /// <param name="value">FP8E5M2 to test</param>
    /// <returns>True when integer</returns>
    public static bool IsInteger(FP8E5M2 value) => float.IsInteger((float)value);


    /// <summary>
    /// Is an even integer
    /// </summary>
    /// <param name="value">FP8E5M2 to test</param>
    /// <returns>True when an even integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEvenInteger(FP8E5M2 value) =>
         float.IsEvenInteger((float) value);

    /// <summary>
    /// Is odd integer?
    /// </summary>
    /// <param name="value">FP8E5M2 to test</param>
    /// <returns>True when off integer</returns>
    public static bool IsOddInteger(FP8E5M2 value)
        => float.IsOddInteger((float) value);


    /// <summary>
    /// TryConvertFrom
    /// </summary>
    /// <param name="value">Typed Value to convert</param>
    /// <param name="result">FP8E5M2 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFrom<TOther>(TOther value, out FP8E5M2 result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof (double))
        {
            double num = (double) (object) value;
            result = (FP8E5M2) num;
            return true;
        }
        if (ofOther == typeof (short))
        {
            short num = (short) (object) value;
            result = (FP8E5M2) num;
            return true;
        }
        if (ofOther == typeof (int))
        {
            int num = (int) (object) value;
            result = (FP8E5M2) num;
            return true;
        }
        if (ofOther == typeof (long))
        {
            long num = (long) (object) value;
            result = (FP8E5M2) num;
            return true;
        }
        if (ofOther == typeof (Int128))
        {
            Int128 int128 = (Int128) (object) value;
            result = (FP8E5M2)(float) int128;
            return true;
        }
        if (ofOther == typeof (IntPtr))
        {
            IntPtr num = (IntPtr) (object) value;
            result = (FP8E5M2) num;
            return true;
        }
        if (ofOther == typeof (sbyte))
        {
            sbyte num = (sbyte) (object) value;
            result = (FP8E5M2) num;
            return true;
        }
        if (ofOther == typeof (float))
        {
            float num = (float) (object) value;
            result = (FP8E5M2) num;
            return true;
        }
        if (ofOther == typeof (Half))
        {
            Half num = (Half) (object) value;
            result = (FP8E5M2) num;
            return true;
        }

        result = new FP8E5M2();
        return false;
    }


    /// <summary>
    /// TryConvertFromChecked
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">FP8E5M2 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromChecked<TOther>(TOther value,
        out FP8E5M2 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);

    /// <summary>
    /// TryConvertFromSaturating
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">FP8E5M2 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromSaturating<TOther>(TOther value,
        out FP8E5M2 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);


    /// <summary>
    /// TryConvertFromTruncating
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">BFlaot16 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromTruncating<TOther>(TOther value,
        out FP8E5M2 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);



    /// <summary>
    /// TryConvertTo
    /// </summary>
    /// <param name="value">FP8E5M2 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
     private static bool TryConvertTo<TOther>(FP8E5M2 value,
        [MaybeNullWhen(false)] out TOther result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof(byte))
        {
            byte num = value >= (FP8E5M2)byte.MaxValue
                ? byte.MaxValue
                : (value <= (FP8E5M2)(byte)0 ? (byte)0 : (byte)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(char))
        {
            char ch = value == FP8E5M2.PositiveInfinity
                ? char.MaxValue
                : (value <= FP8E5M2.Zero ? char.MinValue : (char)value);
            result = (TOther)Convert.ChangeType((ValueType)ch, ofOther);
            return true;
        }

        if (ofOther == typeof(decimal))
        {
            decimal num = value == FP8E5M2.PositiveInfinity
                ? decimal.MaxValue
                : (value == FP8E5M2.NegativeInfinity
                    ? decimal.MinValue
                    : (FP8E5M2.IsNaN(value) ? 0.0M : (decimal)(float)value));
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(ushort))
        {
            ushort num = value == FP8E5M2.PositiveInfinity
                ? ushort.MaxValue
                : (value <= FP8E5M2.Zero ? (ushort)0 : (ushort)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(uint))
        {
            uint num = value == FP8E5M2.PositiveInfinity
                ? uint.MaxValue
                : (value <= FP8E5M2.Zero ? 0U : (uint)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(ulong))
        {
            ulong num = value == FP8E5M2.PositiveInfinity
                ? ulong.MaxValue
                : (value <= FP8E5M2.Zero ? 0UL : (FP8E5M2.IsNaN(value) ?
                    0UL : (ulong)value));
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(UInt128))
        {
            UInt128 uint128 = value == FP8E5M2.PositiveInfinity
                ? UInt128.MaxValue
                : (value <= FP8E5M2.Zero ? UInt128.MinValue : (UInt128)(float)value);
            result = (TOther)Convert.ChangeType((ValueType)uint128, ofOther);
            return true;
        }

        if (ofOther == typeof(UIntPtr))
        {
            UIntPtr num = value == FP8E5M2.PositiveInfinity
                ? UIntPtr.MaxValue
                : (value <= FP8E5M2.Zero ? UIntPtr.MinValue : (UIntPtr)value);
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
    /// <param name="value">FP8E5M2 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToChecked<TOther>(FP8E5M2 value, out TOther result)
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
        if (ofOther == typeof(FP8E5M2))
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
    /// <param name="value">FP8E5M2 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToSaturating<TOther>(FP8E5M2 value,
        out TOther result) where TOther : INumberBase<TOther>
        =>  FP8E5M2.TryConvertTo<TOther>(value, out result);

    /// <summary>
    /// TryConvertToTruncating
    /// </summary>
    /// <param name="value">FP8E5M2 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToTruncating<TOther>(FP8E5M2 value,
        out TOther result) where TOther : INumberBase<TOther>
        =>  FP8E5M2.TryConvertTo<TOther>(value, out result);

    #nullable enable

#endif
    #endregion

}

