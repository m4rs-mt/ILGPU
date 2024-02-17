// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: BFloat16.cs
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
/// BFloat16 Implementation
/// </summary>
public readonly struct BFloat16
#if NET7_0_OR_GREATER
    : INumber<BFloat16>
#else
    : IComparable, IEquatable<BFloat16>, IComparable<BFloat16>
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
    public static BFloat16 Zero  {get; } = new BFloat16(0x0000);

    /// <summary>
    /// One
    /// </summary>

    public static BFloat16 One { get; } = new BFloat16(0x3F80);


    /// <summary>
    /// Represents positive infinity.
    /// </summary>
    public static BFloat16 PositiveInfinity { get; } = new BFloat16(0x7F80);

    /// <summary>
    /// Represents negative infinity.
    /// </summary>
    public static BFloat16 NegativeInfinity{ get; } = new BFloat16(0xFF80);

    /// <summary>
    /// Epsilon - smallest positive value
    /// </summary>
    public static BFloat16 Epsilon { get; } = new BFloat16(0x0001);

    /// <summary>
    /// MaxValue - most positive value
    /// </summary>
    public static BFloat16 MaxValue { get; } = new BFloat16(0x7F7F);

    /// <summary>
    /// MinValue ~ most negative value
    /// </summary>
    public static BFloat16 MinValue { get; } = new BFloat16(0xFF7F);

    /// <summary>
    /// NaN ~ value with all exponent bits set to 1 and a non-zero mantissa
    /// </summary>
    public static BFloat16 NaN { get; } = new BFloat16(0x7FC0);

    #endregion

    #region Comparable



    /// <summary>
    /// CompareTo
    /// </summary>
    /// <param name="obj">Object to compare</param>
    /// <returns>Zero when equal</returns>
    /// <exception cref="ArgumentException">Thrown when not BFloat16</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(object? obj)
    {
        if (obj is BFloat16 other)
            return CompareTo((BFloat16)other);
        if (obj != null)
            throw new ArgumentException("Must be " + nameof(BFloat16));
        return 1;
    }



    /// <summary>
    /// CompareTo
    /// </summary>
    /// <param name="other">BFloat16 to compare</param>
    /// <returns>Zero when successful</returns>
    public int CompareTo(BFloat16 other) => ((float)this).CompareTo(other);

    #endregion

    #region Equality




    /// <summary>
    /// Equals
    /// </summary>
    /// <param name="obj">Object to compare</param>
    /// <returns>bool</returns>
    public readonly override bool Equals(object? obj) =>
        obj is BFloat16 bFloat16 && Equals(bFloat16);

    /// <summary>
    /// Equals
    /// </summary>
    /// <param name="other">Other to compare</param>
    /// <returns>True when successful</returns>
    public bool Equals(BFloat16 other) => this == other;

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
    /// <param name="first">First BFloat16 value</param>
    /// <param name="second">Second BFloat16 value</param>
    /// <returns>True when equal</returns>
    [CompareIntrinisc(CompareKind.Equal)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BFloat16 first, BFloat16 second) =>
        (ushort)Unsafe.As<BFloat16, ushort>(ref first) ==
        (ushort)Unsafe.As<BFloat16, ushort>(ref second);


    /// <summary>
    /// Operator Not Equals
    /// </summary>
    /// <param name="first">First BFloat16 value</param>
    /// <param name="second">Second BFloat16 value</param>
    /// <returns>True when not equal</returns>
    [CompareIntrinisc(CompareKind.NotEqual)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator != (BFloat16 first, BFloat16 second) =>
        (ushort)Unsafe.As<BFloat16, ushort>(ref first) !=
        (ushort)Unsafe.As<BFloat16, ushort>(ref second);


    /// <summary>
    /// Operator less than
    /// </summary>
    /// <param name="first">First BFloat16 value</param>
    /// <param name="second">Second BFloat16 value</param>
    /// <returns>True when less than</returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BFloat16 first, BFloat16 second) =>
        (float)first < (float)second;


    /// <summary>
    /// Operator less than or equals
    /// </summary>
    /// <param name="first">First BFloat16 value</param>
    /// <param name="second">Second BFloat16 value</param>
    /// <returns>True when less than or equal</returns>
    [CompareIntrinisc(CompareKind.LessEqual)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BFloat16 first, BFloat16 second) =>
        (float)first <= (float)second;


    /// <summary>
    /// Operator greater than
    /// </summary>
    /// <param name="first">First BFloat16 value</param>
    /// <param name="second">Second BFloat16 value</param>
    /// <returns>True when greater than</returns>
    [CompareIntrinisc(CompareKind.GreaterThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BFloat16 first, BFloat16 second) =>
        (float)first > (float)second;


    /// <summary>
    /// Operator greater than or equals
    /// </summary>
    /// <param name="first">First BFloat16 value</param>
    /// <param name="second">Second BFloat16 value</param>
    /// <returns>True when greater than or equal</returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BFloat16 first, BFloat16 second) =>
        (float)first >= (float)second;



    #endregion


    #region AdditionAndIncrement

    /// <summary>
    /// Increment operator
    /// </summary>
    /// <param name="value">BFloat16 value to increment</param>
    /// <returns>Incremented value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator ++(BFloat16 value) => (BFloat16) ((float) value + 1f);



    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="value">BFloat16 self to add</param>
    /// <returns>BFloat16 value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator +(BFloat16 value) => value;

    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="left">First BFloat16 value</param>
    /// <param name="right">Second BFloat16 value</param>
    /// <returns>Returns addition</returns>
    public static BFloat16 operator +(BFloat16 left, BFloat16 right)
        => BFloat16Extensions.AddFP32(left, right);


#if NET7_0_OR_GREATER

    /// <summary>
    /// IAdditiveIdentity
    /// </summary>
    static BFloat16 IAdditiveIdentity<BFloat16, BFloat16>.AdditiveIdentity
        => new BFloat16((ushort) 0);
#endif

    #endregion


    #region DecrementAndSubtraction

    /// <summary>
    /// Decrement operator
    /// </summary>
    /// <param name="value">Value to be decremented by 1</param>
    /// <returns>Decremented value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator --(BFloat16 value) => (BFloat16) ((float) value - 1f);


    /// <summary>
    /// Subtraction
    /// </summary>
    /// <param name="left">First BFloat16 value</param>
    /// <param name="right">Second BFloat16 value</param>
    /// <returns>left - right</returns>
    public static BFloat16 operator -(BFloat16 left, BFloat16 right)
        => BFloat16Extensions.SubFP32(left, right);




    /// <summary>
    /// Negation
    /// </summary>
    /// <param name="value">BFloat16 to Negate</param>
    /// <returns>Negated value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator -(BFloat16 value)
        => BFloat16Extensions.Neg(value);

    #endregion


    #region MultiplicationDivisionAndModulus

    /// <summary>
    /// Multiplication
    /// </summary>
    /// <param name="left">First BFloat16 value</param>
    /// <param name="right">Second BFloat16 value</param>
    /// <returns>Multiplication of left * right</returns>
    public static BFloat16 operator *(BFloat16 left, BFloat16 right)
        => BFloat16Extensions.MulFP32(left,right);

    /// <summary>
    /// Division
    /// </summary>
    /// <param name="left">First BFloat16 value</param>
    /// <param name="right">Second BFloat16 value</param>
    /// <returns>Left / right</returns>
    public static BFloat16 operator /(BFloat16 left, BFloat16 right)
        => BFloat16Extensions.DivFP32(left, right);


    /// <summary>
    /// Multiplicative Identity
    /// </summary>
    public static BFloat16 MultiplicativeIdentity  => new BFloat16(0x3F80);

    /// <summary>
    /// Modulus operator
    /// </summary>
    /// <param name="left">First BFloat16 value</param>
    /// <param name="right">Second BFloat16 value</param>
    /// <returns>Left modulus right</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator %(BFloat16 left, BFloat16 right)
        =>  (BFloat16) ((float) left % (float) right);


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


    #endregion

    #region parsing


    /// <summary>
    /// Parse string to BFloat16
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>BFloat16 value when successful</returns>
    public static BFloat16 Parse(string s, IFormatProvider? provider)
        => (BFloat16)float.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands,
            provider);

    /// <summary>
    /// TryParse string to BFloat16
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">BFloat16 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(string? s, IFormatProvider? provider, out BFloat16 result)
    {
        float value;
        bool itWorked = float.TryParse(s,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (BFloat16)value;
        return itWorked;

    }

    /// <summary>
    /// Parse Span of char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Value if parsed successfully</returns>
    public static BFloat16 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => (BFloat16) float.Parse(s, provider);


    /// <summary>
    /// TryParse Span of char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">BFloat16 out param</param>
    /// <returns>True if parsed successfully</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider,
        out BFloat16 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, provider, out floatResult);
        result = (BFloat16)floatResult;
        return isGood;
    }


    /// <summary>
    /// Parse Span char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formating attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Value if parsed successfully</returns>
    public static BFloat16 Parse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider)
        => (BFloat16)float.Parse(s, style, provider);

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formating attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">BFloat16 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider, out BFloat16 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (BFloat16)floatResult;
        return isGood;
    }

    /// <summary>
    /// Parse
    /// </summary>
    /// <param name="utf8Text">Uft8 encoded by span to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Parsed Half Value</returns>
    public static BFloat16 Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
        => (BFloat16) float.Parse(utf8Text, provider);

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="utf8Text">Utf8 encoded byte span to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">BFloat16 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider,
        out BFloat16 result)
    {
        float value;
        bool itWorked = float.TryParse(utf8Text,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (BFloat16)value;
        return itWorked;
    }

    #endregion

    #region object

    internal ushort RawValue { get; }

    /// <summary>
    /// AsUShort - returns internal value
    /// </summary>
    public ushort AsUShort => RawValue;

    internal BFloat16(ushort rawValue)
    {
        RawValue = rawValue;
    }


    /// <summary>
    /// Raw value
    /// </summary>
    /// <returns>internal ushort value</returns>
    public ushort ToRawUShort => RawValue;

   #endregion


   #region Conversions



    /// <summary>
    /// Convert BFloat16 to float
    /// </summary>
    /// <param name="bFloat16">BFloat16 value to convert</param>
    /// <returns>Value converted to float</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float BFloat16ToSingle(BFloat16 bFloat16)
    {
        uint rawBFloat16 = bFloat16.RawValue;
        uint sign = (rawBFloat16 & 0x8000) << 16; // Move sign bit to correct position
        uint exponent = ((rawBFloat16 >> 7) & 0xFF) - 15 + 127; // Adjust exponent format
        uint mantissa = (rawBFloat16 & 0x7F) << (23 - 7); // Scale mantissa

        // Combine sign, exponent, and mantissa into a 32-bit float representation
        uint floatBits = sign | (exponent << 23) | mantissa;

        // Convert the 32-bit representation into a float
        return BitConverter.ToSingle(BitConverter.GetBytes(floatBits), 0);
    }

    /// <summary>
    /// Convert BFloat16 to double
    /// </summary>
    /// <param name="bFloat16"></param>
    /// <returns>Double</returns>
    private static double BFloat16ToDouble(BFloat16 bFloat16)
    {
        ushort bFloat16Raw = bFloat16.RawValue;

        // Extracting sign, exponent, and mantissa from BFloat16
        ulong sign = (ulong)(bFloat16Raw & 0x8000) << 48; // Shift left for double
        int exponentBits = ((bFloat16Raw >> 7) & 0xFF) - 127 + 1023; // Adjusting exponent

        // Ensuring exponent does not underflow or overflow the valid range for double
        if (exponentBits < 0) exponentBits = 0;
        if (exponentBits > 0x7FF) exponentBits = 0x7FF;

        ulong exponent = (ulong)exponentBits << 52; // Positioning exponent for double

        // Extracting and positioning the mantissa bits
        ulong mantissa = ((ulong)(bFloat16Raw & 0x7F)) << 45; // Align mantissa for double

        // Assembling the double
        ulong doubleBits = sign | exponent | mantissa;

        return BitConverter.UInt64BitsToDouble(doubleBits);
    }

    /// <summary>
    /// StripSign
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>sign bit as BFloat16</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint StripSign(BFloat16 value)
        => (ushort)((uint)value & 0x7FFF);


    /// <summary>
    /// Convert Half to BFloat16
    /// </summary>
    /// <param name="value">Half to convert</param>
    /// <returns>BFloat16</returns>
    private static BFloat16 HalfToBFloat16(Half value)
    {
        // Extracting the binary representation of the float value
        ushort halfBits = value.RawValue;

        // Extracting sign (1 bit)
        ushort sign = (ushort)(halfBits & 0x8000);

        // Adjusting the exponent from Half (5 bits) to BFloat16 (8 bits)
        // This involves shifting and possibly adjusting for the different exponent bias
        // This example assumes no bias adjustment for simplicity
        ushort exponent = (ushort)(((halfBits >> 10) & 0x1F) << 7);
        // Shift left to align with BFloat16's exponent position

        // Adjusting the mantissa from Half (10 bits) to BFloat16 (7 bits)
        // This involves truncating the 3 least significant bits
        ushort mantissa = (ushort)((halfBits & 0x03FF) >> (10 - 7));


        // Combining sign, exponent, and mantissa into BFloat16 format
        ushort bFloat16Bits = (ushort)(sign | exponent | mantissa);

        return new BFloat16(bFloat16Bits);
    }

    /// <summary>
    /// Convert float to BFloat16
    /// </summary>
    /// <param name="value">float to convert</param>
    /// <returns>BFloat16</returns>
    private static BFloat16 SingleToBFloat16(float value)
    {
        // Extracting the binary representation of the float value
        uint floatBits = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);

        // Extracting sign (1 bit)
        ushort sign = (ushort)((floatBits >> 16) & 0x8000);

        // Extracting exponent (8 bits)
        ushort exponent = (ushort)(((floatBits >> 23) & 0xFF) - 127 + 127);
        // Adjust exponent and re-bias if necessary
        exponent = (ushort)(exponent << 7);

        // Extracting mantissa (top 7 bits of the float's 23-bit mantissa)
        ushort mantissa = (ushort)((floatBits >> (23 - 7)) & 0x7F);

        // Combining into BFloat16 format (1 bit sign, 8 bits exponent, 7 bits mantissa)
        ushort bFloat16 = (ushort)(sign | exponent | mantissa);

        return new (bFloat16);
    }


    /// <summary>
    /// Convert double to BFloat16
    /// </summary>
    /// <param name="value">double to convert</param>
    /// <returns>BFloat16</returns>
    private static BFloat16 DoubleToBFloat16(double value)
    {
        // Extracting the binary representation of the double value
        ulong doubleBits = BitConverter.ToUInt64(BitConverter.GetBytes(value), 0);

        // Extracting leading sign (1 bit)
        ushort sign = (ushort)((doubleBits >> 48) & 0x8000); // Extract sign bit


        long exponentBits
            = (long)((doubleBits >> 52) & 0x7FF) - 1023 + 127; // Adjust exponent
        uint exponent
            = (uint)(exponentBits < 0 ? 0 : exponentBits > 0xFF ? 0xFF : exponentBits);


        // Extracting the top 7 bits of the mantissa from the double and position it
        ushort mantissa = (ushort)((doubleBits >> (52 - 7)) & 0x7F); // Shift and mask


        // Combining into BFloat16 format (1 bit sign, 8 bits exponent, 7 bits mantissa)
        ushort bFloat16 =
            (ushort)(sign | (ushort)(exponent << 7) | mantissa);

        return new BFloat16(bFloat16);
    }
    #endregion

    #region operators

    /// <summary>
    /// Cast float to BFloat16
    /// </summary>
    /// <param name="value">float to cast</param>
    /// <returns>BFloat16</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator BFloat16(float value)
        => SingleToBFloat16(value);


    /// <summary>
    /// Cast double to BFloat16
    /// </summary>
    /// <param name="value">double to cast</param>
    /// <returns>BFloat16</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator BFloat16(double value)
        => DoubleToBFloat16(value);

    /// <summary>
    /// Cast BFloat16 to float
    /// </summary>
    /// <param name="value">BFloat16 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(BFloat16 value)
        => BFloat16ToSingle(value);

    /// <summary>
    /// Cast BFloat16 to double
    /// </summary>
    /// <param name="value">BFloat16 value to cast</param>
    /// <returns>double</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(BFloat16 value) =>
        (double)BFloat16ToSingle(value);


    #endregion

    #region INumberbase




    /// <summary>
    /// Absolute Value
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>Absolute of BFloat16</returns>
    [MathIntrinsic(MathIntrinsicKind.Abs)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 Abs(BFloat16 value) => BFloat16Extensions.Abs(value);

    /// <summary>
    /// Is Bfloat16 Canonical
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>True based on type</returns>
    public static bool IsCanonical(BFloat16 value) => true;


    /// <summary>
    /// Is Bfloat16 a complex number
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>False based on type</returns>
    public static bool IsComplexNumber(BFloat16 value) => false;


    /// <summary>
    /// Is value finite
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>True when finite</returns>
    public static bool IsFinite(BFloat16 value)
        => Bitwise.And(!IsNaN(value), !IsInfinity(value));

    /// <summary>
    /// Is imaginary number
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>False based on type</returns>
    public static bool IsImaginaryNumber(BFloat16 value) => false;

    /// <summary>
    /// Is an infinite value?
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>True when finite</returns>
    public static bool IsInfinity(BFloat16 value) =>
        (value.RawValue & 0x7F80) == 0x7F80 && (value.RawValue & 0x007F) == 0;


    /// <summary>
    /// Is NaN
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>True when NaN</returns>
    public static bool IsNaN(BFloat16 value)
        // NaN if all exponent bits are 1 and there is a non-zero value in the mantissa
        =>  (value.RawValue & BFloat16Extensions.ExponentMask)
            == BFloat16Extensions.ExponentMask
            && (value.RawValue & BFloat16Extensions.MantissaMask) != 0;

    /// <summary>
    /// Is negative?
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>True when negative</returns>
    public static bool IsNegative(BFloat16 value) =>  (value.RawValue & 0x8000) != 0;

    /// <summary>
    /// Is negative infinity
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>True when negative infinity</returns>
    public static bool IsNegativeInfinity(BFloat16 value) => value == NegativeInfinity;

    /// <summary>
    /// Is normal
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>True when normal</returns>
    public static bool IsNormal(BFloat16 value)
    {
        uint num = StripSign(value);
        return num < 0x7F80 && num != 0 && (num & 0x7F80) != 0;
    }


    /// <summary>
    /// Is positive?
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>True when positive</returns>
    public static bool IsPositive(BFloat16 value) => (value.RawValue & 0x8000) == 0;

    /// <summary>
    /// Is positive infinity?
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>True when positive</returns>
    public static bool IsPositiveInfinity(BFloat16 value) => value == PositiveInfinity;

    /// <summary>
    /// Is real number
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>True when real number</returns>
    public static bool IsRealNumber(BFloat16 value)
    {
        bool isExponentAllOnes = (value.RawValue & BFloat16Extensions.ExponentMask)
                                 == BFloat16Extensions.ExponentMask;
        bool isMantissaNonZero = (value.RawValue & BFloat16Extensions.MantissaMask) != 0;

        // If exponent is all ones and mantissa is non-zero, it's NaN, so return false
        return !(isExponentAllOnes && isMantissaNonZero);
    }

    /// <summary>
    /// Is subnormal
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>True when subnormal</returns>
    public static bool IsSubnormal(BFloat16 value)
        => (value.RawValue & 0x7F80) == 0 && (value.RawValue & 0x007F) != 0;

    /// <summary>
    /// Is Zero?
    /// </summary>
    /// <param name="value">BFloat16</param>
    /// <returns>True when Zero</returns>
    public static bool IsZero(BFloat16 value)
        => (value.RawValue & BFloat16Extensions.ExponentMantissaMask) == 0;

    /// <summary>
    /// MaxMagnitude
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns larger of x or y, NaN when equal</returns>
    public static BFloat16 MaxMagnitude(BFloat16 x, BFloat16 y)
        =>(BFloat16) MathF.MaxMagnitude((float) x, (float) y);

    /// <summary>
    /// MaxMagnitudeNumber
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns x or y based on larger of Abs(x) vs Abs(Y) </returns>
    public static BFloat16 MaxMagnitudeNumber(BFloat16 x, BFloat16 y)
    {
        BFloat16 bf1 = BFloat16.Abs(x);
        BFloat16 bf2 = BFloat16.Abs(y);
        return bf1 > bf2 || BFloat16.IsNaN(bf2) || bf1
            == bf2 && !BFloat16.IsNegative(x) ? x : y;
    }

    /// <summary>
    /// MinMagnitude
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>returns smaller of x or y or NaN when equal</returns>
    public static BFloat16 MinMagnitude(BFloat16 x, BFloat16 y)
        =>(BFloat16) MathF.MinMagnitude((float) x, (float) y);

    /// <summary>
    /// MinMagnitudeNumber
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns x or y based on smaller of Abs(x) vs Abs(Y)</returns>
    public static BFloat16 MinMagnitudeNumber(BFloat16 x, BFloat16 y)
    {
        BFloat16 bf1 = BFloat16.Abs(x);
        BFloat16 bf2 = BFloat16.Abs(y);
        return bf1 < bf2 || BFloat16.IsNaN(bf2) ||
               bf1 == bf2 && BFloat16.IsNegative(x) ? x : y;
    }



    /// <summary>
    /// Cast double to BFloat16
    /// </summary>
    /// <param name="value">Half value to convert</param>
    /// <returns>BFloat16</returns>
    public static explicit operator BFloat16(Half value)
        => HalfToBFloat16(value);




    /// <summary>
    /// Parse string
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formating attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Parsed BFloat16 value when successful</returns>
    public static BFloat16 Parse(string s, NumberStyles style, IFormatProvider? provider)
        => (BFloat16)float.Parse(s, style, provider);




    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formating attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">BFloat16 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider,
        out BFloat16 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (BFloat16)floatResult;
        return isGood;
    }


#if NET7_0_OR_GREATER


    /// <summary>
    /// Is value an integer?
    /// </summary>
    /// <param name="value">BFloat16 to test</param>
    /// <returns>True when integer</returns>
    public static bool IsInteger(BFloat16 value) => float.IsInteger((float)value);


    /// <summary>
    /// Is an even integer
    /// </summary>
    /// <param name="value">BFloat16 to test</param>
    /// <returns>True when an even integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEvenInteger(BFloat16 value) =>
         float.IsEvenInteger((float) value);

    /// <summary>
    /// Is odd integer?
    /// </summary>
    /// <param name="value">BFloat16 to test</param>
    /// <returns>True when off integer</returns>
    public static bool IsOddInteger(BFloat16 value) => float.IsOddInteger((float) value);


    /// <summary>
    /// TryConvertFrom
    /// </summary>
    /// <param name="value">Typed Value to convert</param>
    /// <param name="result">BFloat16 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFrom<TOther>(TOther value, out BFloat16 result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof (double))
        {
            double num = (double) (object) value;
            result = (BFloat16) num;
            return true;
        }
        if (ofOther == typeof (short))
        {
            short num = (short) (object) value;
            result = (BFloat16) num;
            return true;
        }
        if (ofOther == typeof (int))
        {
            int num = (int) (object) value;
            result = (BFloat16) num;
            return true;
        }
        if (ofOther == typeof (long))
        {
            long num = (long) (object) value;
            result = (BFloat16) num;
            return true;
        }
        if (ofOther == typeof (Int128))
        {
            Int128 int128 = (Int128) (object) value;
            result = (BFloat16)(float) int128;
            return true;
        }
        if (ofOther == typeof (IntPtr))
        {
            IntPtr num = (IntPtr) (object) value;
            result = (BFloat16) num;
            return true;
        }
        if (ofOther == typeof (sbyte))
        {
            sbyte num = (sbyte) (object) value;
            result = (BFloat16) num;
            return true;
        }
        if (ofOther == typeof (float))
        {
            float num = (float) (object) value;
            result = (BFloat16) num;
            return true;
        }
        if (ofOther == typeof (Half))
        {
            Half num = (Half) (object) value;
            result = (BFloat16) num;
            return true;
        }

        result = new BFloat16();
        return false;
    }


    /// <summary>
    /// TryConvertFromChecked
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">BFloat16 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromChecked<TOther>(TOther value, out BFloat16 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);

    /// <summary>
    /// TryConvertFromSaturating
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">BFloat16 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromSaturating<TOther>(TOther value, out BFloat16 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);


    /// <summary>
    /// TryConvertFromTruncating
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">BFlaot16 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromTruncating<TOther>(TOther value, out BFloat16 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);



    /// <summary>
    /// TryConvertTo
    /// </summary>
    /// <param name="value">BFloat16 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
     private static bool TryConvertTo<TOther>(BFloat16 value,
        [MaybeNullWhen(false)] out TOther result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof(byte))
        {
            byte num = value >= (BFloat16)byte.MaxValue
                ? byte.MaxValue
                : (value <= (BFloat16)(byte)0 ? (byte)0 : (byte)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(char))
        {
            char ch = value == BFloat16.PositiveInfinity
                ? char.MaxValue
                : (value <= BFloat16.Zero ? char.MinValue : (char)value);
            result = (TOther)Convert.ChangeType((ValueType)ch, ofOther);
            return true;
        }

        if (ofOther == typeof(decimal))
        {
            decimal num = value == BFloat16.PositiveInfinity
                ? decimal.MaxValue
                : (value == BFloat16.NegativeInfinity
                    ? decimal.MinValue
                    : (BFloat16.IsNaN(value) ? 0.0M : (decimal)(float)value));
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(ushort))
        {
            ushort num = value == BFloat16.PositiveInfinity
                ? ushort.MaxValue
                : (value <= BFloat16.Zero ? (ushort)0 : (ushort)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(uint))
        {
            uint num = value == BFloat16.PositiveInfinity
                ? uint.MaxValue
                : (value <= BFloat16.Zero ? 0U : (uint)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(ulong))
        {
            ulong num = value == BFloat16.PositiveInfinity
                ? ulong.MaxValue
                : (value <= BFloat16.Zero ? 0UL : (BFloat16.IsNaN(value) ?
                    0UL : (ulong)value));
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(UInt128))
        {
            UInt128 uint128 = value == BFloat16.PositiveInfinity
                ? UInt128.MaxValue
                : (value <= BFloat16.Zero ? UInt128.MinValue : (UInt128)(float)value);
            result = (TOther)Convert.ChangeType((ValueType)uint128, ofOther);
            return true;
        }

        if (ofOther == typeof(UIntPtr))
        {
            UIntPtr num = value == BFloat16.PositiveInfinity
                ? UIntPtr.MaxValue
                : (value <= BFloat16.Zero ? UIntPtr.MinValue : (UIntPtr)value);
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
    /// <param name="value">BFloat16 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToChecked<TOther>(BFloat16 value, out TOther result)
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
        if (ofOther == typeof(BFloat16))
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
    /// <param name="value">BFloat16 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToSaturating<TOther>(BFloat16 value, out TOther result)
        where TOther : INumberBase<TOther>
        =>  BFloat16.TryConvertTo<TOther>(value, out result);

    /// <summary>
    /// TryConvertToTruncating
    /// </summary>
    /// <param name="value">BFloat16 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToTruncating<TOther>(BFloat16 value, out TOther result)
        where TOther : INumberBase<TOther>
        =>  BFloat16.TryConvertTo<TOther>(value, out result);

    #nullable enable

#endif
    #endregion

}

