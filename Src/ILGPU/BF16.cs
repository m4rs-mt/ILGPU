// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: BF16.cs
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
using System.Runtime.InteropServices;

namespace ILGPU;


/// <summary>
/// BF16 Implementation
/// </summary>
public readonly struct BF16
#if NET7_0_OR_GREATER
    : INumber<BF16>
#else
    : IComparable, IEquatable<BF16>, IComparable<BF16>
#endif
{

    #region constants

    private static bool ShouldRoundUp { get; } = GetRuntimeRoundup();

    private static bool GetRuntimeRoundup()
    {
        switch (RuntimeInformation.OSArchitecture)
        {
            case Architecture.Arm:
            case Architecture.Arm64:
                //case Architecture.Armv6: legacy
                return false;
            default:
                return true;
        }
    }


    /// <summary>
    /// Radix
    /// </summary>
    public static int Radix => 2;

    /// <summary>
    /// Zero
    /// </summary>
    public static BF16 Zero  {get; } = new BF16(0x0000);

    /// <summary>
    /// One
    /// </summary>

    public static BF16 One { get; } = new BF16(0x3F80);


    /// <summary>
    /// Represents positive infinity.
    /// </summary>
    public static BF16 PositiveInfinity { get; } = new BF16(0x7F80);

    /// <summary>
    /// Represents negative infinity.
    /// </summary>
    public static BF16 NegativeInfinity{ get; } = new BF16(0xFF80);

    /// <summary>
    /// Epsilon - smallest positive value
    /// </summary>
    public static BF16 Epsilon { get; } = new BF16(0x0001);

    /// <summary>
    /// MaxValue - most positive value
    /// </summary>
    public static BF16 MaxValue { get; } = new BF16(0x7F7F);

    /// <summary>
    /// MinValue ~ most negative value
    /// </summary>
    public static BF16 MinValue { get; } = new BF16(0xFF7F);

    /// <summary>
    /// NaN ~ value with all exponent bits set to 1 and a non-zero mantissa
    /// </summary>
    public static BF16 NaN { get; } = new BF16(0x7FC0);

    #endregion

    #region Comparable



    /// <summary>
    /// CompareTo
    /// </summary>
    /// <param name="obj">Object to compare</param>
    /// <returns>Zero when equal</returns>
    /// <exception cref="ArgumentException">Thrown when not BF16</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(object? obj)
    {
        if (obj is BF16 other)
            return CompareTo((BF16)other);
        if (obj != null)
            throw new ArgumentException("Must be " + nameof(BF16));
        return 1;
    }



    /// <summary>
    /// CompareTo
    /// </summary>
    /// <param name="other">BF16 to compare</param>
    /// <returns>Zero when successful</returns>
    public int CompareTo(BF16 other) => ((float)this).CompareTo(other);

    #endregion

    #region Equality




    /// <summary>
    /// Equals
    /// </summary>
    /// <param name="obj">Object to compare</param>
    /// <returns>bool</returns>
    public readonly override bool Equals(object? obj) =>
        obj is BF16 BF16 && Equals(BF16);

    /// <summary>
    /// Equals
    /// </summary>
    /// <param name="other">Other to compare</param>
    /// <returns>True when successful</returns>
    public bool Equals(BF16 other) => this == other;

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
    /// <param name="first">First BF16 value</param>
    /// <param name="second">Second BF16 value</param>
    /// <returns>True when equal</returns>
    [CompareIntrinisc(CompareKind.Equal)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BF16 first, BF16 second) =>
        (ushort)Unsafe.As<BF16, ushort>(ref first) ==
        (ushort)Unsafe.As<BF16, ushort>(ref second);


    /// <summary>
    /// Operator Not Equals
    /// </summary>
    /// <param name="first">First BF16 value</param>
    /// <param name="second">Second BF16 value</param>
    /// <returns>True when not equal</returns>
    [CompareIntrinisc(CompareKind.NotEqual)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator != (BF16 first, BF16 second) =>
        (ushort)Unsafe.As<BF16, ushort>(ref first) !=
        (ushort)Unsafe.As<BF16, ushort>(ref second);


    /// <summary>
    /// Operator less than
    /// </summary>
    /// <param name="first">First BF16 value</param>
    /// <param name="second">Second BF16 value</param>
    /// <returns>True when less than</returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BF16 first, BF16 second) =>
        (float)first < (float)second;


    /// <summary>
    /// Operator less than or equals
    /// </summary>
    /// <param name="first">First BF16 value</param>
    /// <param name="second">Second BF16 value</param>
    /// <returns>True when less than or equal</returns>
    [CompareIntrinisc(CompareKind.LessEqual)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BF16 first, BF16 second) =>
        (float)first <= (float)second;


    /// <summary>
    /// Operator greater than
    /// </summary>
    /// <param name="first">First BF16 value</param>
    /// <param name="second">Second BF16 value</param>
    /// <returns>True when greater than</returns>
    [CompareIntrinisc(CompareKind.GreaterThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BF16 first, BF16 second) =>
        (float)first > (float)second;


    /// <summary>
    /// Operator greater than or equals
    /// </summary>
    /// <param name="first">First BF16 value</param>
    /// <param name="second">Second BF16 value</param>
    /// <returns>True when greater than or equal</returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BF16 first, BF16 second) =>
        (float)first >= (float)second;



    #endregion


    #region AdditionAndIncrement

    /// <summary>
    /// Increment operator
    /// </summary>
    /// <param name="value">BF16 value to increment</param>
    /// <returns>Incremented value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 operator ++(BF16 value) => (BF16) ((float) value + 1f);



    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="value">BF16 self to add</param>
    /// <returns>BF16 value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 operator +(BF16 value) => value;

    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="left">First BF16 value</param>
    /// <param name="right">Second BF16 value</param>
    /// <returns>Returns addition</returns>
    public static BF16 operator +(BF16 left, BF16 right)
        => BF16Extensions.AddFP32(left, right);


#if NET7_0_OR_GREATER

    /// <summary>
    /// IAdditiveIdentity
    /// </summary>
    static BF16 IAdditiveIdentity<BF16, BF16>.AdditiveIdentity
        => new BF16((ushort) 0);
#endif

    #endregion


    #region DecrementAndSubtraction

    /// <summary>
    /// Decrement operator
    /// </summary>
    /// <param name="value">Value to be decremented by 1</param>
    /// <returns>Decremented value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 operator --(BF16 value) => (BF16) ((float) value - 1f);


    /// <summary>
    /// Subtraction
    /// </summary>
    /// <param name="left">First BF16 value</param>
    /// <param name="right">Second BF16 value</param>
    /// <returns>left - right</returns>
    public static BF16 operator -(BF16 left, BF16 right)
        => BF16Extensions.SubFP32(left, right);




    /// <summary>
    /// Negation
    /// </summary>
    /// <param name="value">BF16 to Negate</param>
    /// <returns>Negated value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 operator -(BF16 value)
        => BF16Extensions.Neg(value);

    #endregion


    #region MultiplicationDivisionAndModulus

    /// <summary>
    /// Multiplication
    /// </summary>
    /// <param name="left">First BF16 value</param>
    /// <param name="right">Second BF16 value</param>
    /// <returns>Multiplication of left * right</returns>
    public static BF16 operator *(BF16 left, BF16 right)
        => BF16Extensions.MulFP32(left,right);

    /// <summary>
    /// Division
    /// </summary>
    /// <param name="left">First BF16 value</param>
    /// <param name="right">Second BF16 value</param>
    /// <returns>Left / right</returns>
    public static BF16 operator /(BF16 left, BF16 right)
        => BF16Extensions.DivFP32(left, right);


    /// <summary>
    /// Multiplicative Identity
    /// </summary>
    public static BF16 MultiplicativeIdentity  => new BF16(0x3F80);

    /// <summary>
    /// Modulus operator
    /// </summary>
    /// <param name="left">First BF16 value</param>
    /// <param name="right">Second BF16 value</param>
    /// <returns>Left modulus right</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 operator %(BF16 left, BF16 right)
        =>  (BF16) ((float) left % (float) right);


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
    /// Parse string to BF16
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>BF16 value when successful</returns>
    public static BF16 Parse(string s, IFormatProvider? provider)
        => (BF16)float.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands,
            provider);

    /// <summary>
    /// TryParse string to BF16
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">BF16 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(string? s, IFormatProvider? provider, out BF16 result)
    {
        float value;
        bool itWorked = float.TryParse(s,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (BF16)value;
        return itWorked;

    }

#if NET7_0_OR_GREATER

    /// <summary>
    /// Parse Span of char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Value if parsed successfully</returns>
    public static BF16 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => (BF16) float.Parse(s, provider);


    /// <summary>
    /// TryParse Span of char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">BF16 out param</param>
    /// <returns>True if parsed successfully</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider,
        out BF16 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, provider, out floatResult);
        result = (BF16)floatResult;
        return isGood;
    }



    /// <summary>
    /// Parse Span char
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Value if parsed successfully</returns>
    public static BF16 Parse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider)
        => (BF16)float.Parse(s, style, provider);

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">BF16 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider, out BF16 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (BF16)floatResult;
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
    public static BF16 Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
        => (BF16) float.Parse(utf8Text, provider);

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="utf8Text">Utf8 encoded byte span to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">BF16 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider,
        out BF16 result)
    {
        float value;
        bool itWorked = float.TryParse(utf8Text,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (BF16)value;
        return itWorked;
    }

#endif

    #endregion

    #region object

    internal ushort RawValue { get; }



    internal BF16(ushort rawValue)
    {
        RawValue = rawValue;
    }

    /// <summary>
    /// AsUShort - returns internal value
    /// </summary>
    public ushort AsUShort => RawValue;




   #endregion


   #region Conversions



    /// <summary>
    /// Convert BF16 to float
    /// </summary>
    /// <param name="bf16">BF16 value to convert</param>
    /// <returns>Value converted to float</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float BF16ToSingle(BF16 bf16)
    {
        // Combine and shift the sign, exponent and mantissa bits together.
        uint floatBits = (uint)(bf16.RawValue) << 16;

        // Convert the 32-bit representation back to a float
        return Unsafe.As<uint, float>(ref floatBits);
    }




    /// <summary>
    /// Convert float to BF16
    /// </summary>
    /// <param name="value">float to convert</param>
    /// <returns>BF16</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BF16 SingleToBF16(float value)
    {
        // Convert the 32-bit float to its binary representation
        uint floatBits = Unsafe.As<float, uint>(ref value);


        ushort truncatedBits = (ushort)(floatBits >> 16);

        //this appears to be 30% slower than the shift
        //ushort truncatedBits = *((ushort*)&floatBits + 1);

        // Check if rounding is needed (halfway or more than halfway)
        bool isHalfwayOrMore = (floatBits & 0x8000) != 0;
        bool isMoreThanHalfway = (floatBits & 0x7FFF) != 0;
        // Check for any bits beyond the halfway bit

        // Apply round to even if exactly at halfway,
        // check if least significant bit of truncatedBits is set (even check)
        // even rounding

        bool shouldRoundUp = ShouldRoundUp?
                // default for even rounding
                (isHalfwayOrMore
                 && (isMoreThanHalfway || (truncatedBits & 1) != 0))
                : // odd rounding for ARM
                (isHalfwayOrMore
                  && (!isMoreThanHalfway || (truncatedBits & 1) == 0));
            // Odd rounding is used by Armv8.6+ based processors
            // Apple M2+ processors / A15+
            // Qualcom Cortex-X2+ / Cortex A510+ / Cortex A710+
            // Neoverse N2 or V2

        if (shouldRoundUp)
        {
            // Increment the BF16 representation if rounding is needed
            // This increment could lead to mantissa overflow, which naturally
            // increments the exponent
            truncatedBits++;

            // Note: No specific handling for overflow into infinity is provided here,
            // which could be relevant for the maximum representable float values.
        }

        return Unsafe.As<ushort,BF16>(ref truncatedBits);  //return new (BF16);
    }


    #endregion

    #region operators

    /// <summary>
    /// Cast float to BF16
    /// </summary>
    /// <param name="value">float to cast</param>
    /// <returns>BF16</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator BF16(float value)
        => SingleToBF16(value);


    /// <summary>
    /// Cast double to BF16
    /// </summary>
    /// <param name="value">double to cast</param>
    /// <returns>BF16</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator BF16(double value)
        => SingleToBF16((float) value);

    /// <summary>
    /// Cast BF16 to FP8E4M3
    /// </summary>
    /// <param name="value">BF16 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator FP8E4M3(BF16 value)
        => (FP8E4M3) BF16ToSingle(value);

    /// <summary>
    /// Cast BF16 to FP8E5M2
    /// </summary>
    /// <param name="value">BF16 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator FP8E5M2(BF16 value)
        => (FP8E5M2) BF16ToSingle(value);

    /// <summary>
    /// Cast BF16 to float
    /// </summary>
    /// <param name="value">BF16 value to cast</param>
    /// <returns>float</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(BF16 value)
        => BF16ToSingle(value);

    /// <summary>
    /// Cast BF16 to double
    /// </summary>
    /// <param name="value">BF16 value to cast</param>
    /// <returns>double</returns>
    [ConvertIntrinisc]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(BF16 value) =>
        (double)BF16ToSingle(value);


    #endregion

    #region INumberbase




    /// <summary>
    /// Absolute Value
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>Absolute of BF16</returns>
    [MathIntrinsic(MathIntrinsicKind.Abs)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BF16 Abs(BF16 value) => BF16Extensions.Abs(value);

    /// <summary>
    /// Is BF16 Canonical
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>True based on type</returns>
    public static bool IsCanonical(BF16 value) => true;


    /// <summary>
    /// Is BF16 a complex number
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>False based on type</returns>
    public static bool IsComplexNumber(BF16 value) => false;


    /// <summary>
    /// Is value finite
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>True when finite</returns>
    public static bool IsFinite(BF16 value)
        => Bitwise.And(!IsNaN(value), !IsInfinity(value));

    /// <summary>
    /// Is imaginary number
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>False based on type</returns>
    public static bool IsImaginaryNumber(BF16 value) => false;

    /// <summary>
    /// Is an infinite value?
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>True when finite</returns>
    public static bool IsInfinity(BF16 value) =>
        (value.RawValue & 0x7F80) == 0x7F80 && (value.RawValue & 0x007F) == 0;


    /// <summary>
    /// Is NaN
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>True when NaN</returns>
    public static bool IsNaN(BF16 value)
        // NaN if all exponent bits are 1 and there is a non-zero value in the mantissa
        =>  (value.RawValue & BF16Extensions.ExponentMask)
            == BF16Extensions.ExponentMask
            && (value.RawValue & BF16Extensions.MantissaMask) != 0;

    /// <summary>
    /// Is negative?
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>True when negative</returns>
    public static bool IsNegative(BF16 value) =>  (value.RawValue & 0x8000) != 0;

    /// <summary>
    /// Is negative infinity
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>True when negative infinity</returns>
    public static bool IsNegativeInfinity(BF16 value) => value == NegativeInfinity;

    /// <summary>
    /// Is normal
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>True when normal</returns>
    public static bool IsNormal(BF16 value)
    {
        uint num = (uint)value & 0x7FFF;
        return num < 0x7F80 && num != 0 && (num & 0x7F80) != 0;
    }


    /// <summary>
    /// Is positive?
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>True when positive</returns>
    public static bool IsPositive(BF16 value) => (value.RawValue & 0x8000) == 0;

    /// <summary>
    /// Is positive infinity?
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>True when positive</returns>
    public static bool IsPositiveInfinity(BF16 value) => value == PositiveInfinity;

    /// <summary>
    /// Is real number
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>True when real number</returns>
    public static bool IsRealNumber(BF16 value)
    {
        bool isExponentAllOnes = (value.RawValue & BF16Extensions.ExponentMask)
                                 == BF16Extensions.ExponentMask;
        bool isMantissaNonZero = (value.RawValue & BF16Extensions.MantissaMask) != 0;

        // If exponent is all ones and mantissa is non-zero, it's NaN, so return false
        return !(isExponentAllOnes && isMantissaNonZero);
    }

    /// <summary>
    /// Is subnormal
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>True when subnormal</returns>
    public static bool IsSubnormal(BF16 value)
        => (value.RawValue & 0x7F80) == 0 && (value.RawValue & 0x007F) != 0;

    /// <summary>
    /// Is Zero?
    /// </summary>
    /// <param name="value">BF16</param>
    /// <returns>True when Zero</returns>
    public static bool IsZero(BF16 value)
        => (value.RawValue & BF16Extensions.ExponentMantissaMask) == 0;

    /// <summary>
    /// MaxMagnitude
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns larger of x or y, NaN when equal</returns>
    public static BF16 MaxMagnitude(BF16 x, BF16 y)
        =>(BF16) MathF.MaxMagnitude((float) x, (float) y);

    /// <summary>
    /// MaxMagnitudeNumber
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns x or y based on larger of Abs(x) vs Abs(Y) </returns>
    public static BF16 MaxMagnitudeNumber(BF16 x, BF16 y)
    {
        BF16 bf1 = BF16.Abs(x);
        BF16 bf2 = BF16.Abs(y);
        return bf1 > bf2 || BF16.IsNaN(bf2) || bf1
            == bf2 && !BF16.IsNegative(x) ? x : y;
    }

    /// <summary>
    /// MinMagnitude
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>returns smaller of x or y or NaN when equal</returns>
    public static BF16 MinMagnitude(BF16 x, BF16 y)
        =>(BF16) MathF.MinMagnitude((float) x, (float) y);

    /// <summary>
    /// MinMagnitudeNumber
    /// </summary>
    /// <param name="x">x parameter</param>
    /// <param name="y">y parameter</param>
    /// <returns>Returns x or y based on smaller of Abs(x) vs Abs(Y)</returns>
    public static BF16 MinMagnitudeNumber(BF16 x, BF16 y)
    {
        BF16 bf1 = BF16.Abs(x);
        BF16 bf2 = BF16.Abs(y);
        return bf1 < bf2 || BF16.IsNaN(bf2) ||
               bf1 == bf2 && BF16.IsNegative(x) ? x : y;
    }



    /// <summary>
    /// Cast double to BF16
    /// </summary>
    /// <param name="value">Half value to convert</param>
    /// <returns>BF16</returns>
    public static explicit operator BF16(Half value)
        => SingleToBF16((float) value);




    /// <summary>
    /// Parse string
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Parsed BF16 value when successful</returns>
    public static BF16 Parse(string s, NumberStyles style, IFormatProvider? provider)
        => (BF16)float.Parse(s, style, provider);




    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">BF16 out param</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider,
        out BF16 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (BF16)floatResult;
        return isGood;
    }


#if NET7_0_OR_GREATER


    /// <summary>
    /// Is value an integer?
    /// </summary>
    /// <param name="value">BF16 to test</param>
    /// <returns>True when integer</returns>
    public static bool IsInteger(BF16 value) => float.IsInteger((float)value);


    /// <summary>
    /// Is an even integer
    /// </summary>
    /// <param name="value">BF16 to test</param>
    /// <returns>True when an even integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEvenInteger(BF16 value) =>
         float.IsEvenInteger((float) value);

    /// <summary>
    /// Is odd integer?
    /// </summary>
    /// <param name="value">BF16 to test</param>
    /// <returns>True when off integer</returns>
    public static bool IsOddInteger(BF16 value) => float.IsOddInteger((float) value);


    /// <summary>
    /// TryConvertFrom
    /// </summary>
    /// <param name="value">Typed Value to convert</param>
    /// <param name="result">BF16 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFrom<TOther>(TOther value, out BF16 result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof (double))
        {
            double num = (double) (object) value;
            result = (BF16) num;
            return true;
        }
        if (ofOther == typeof (short))
        {
            short num = (short) (object) value;
            result = (BF16) num;
            return true;
        }
        if (ofOther == typeof (int))
        {
            int num = (int) (object) value;
            result = (BF16) num;
            return true;
        }
        if (ofOther == typeof (long))
        {
            long num = (long) (object) value;
            result = (BF16) num;
            return true;
        }
        if (ofOther == typeof (Int128))
        {
            Int128 int128 = (Int128) (object) value;
            result = (BF16)(float) int128;
            return true;
        }
        if (ofOther == typeof (IntPtr))
        {
            IntPtr num = (IntPtr) (object) value;
            result = (BF16) num;
            return true;
        }
        if (ofOther == typeof (sbyte))
        {
            sbyte num = (sbyte) (object) value;
            result = (BF16) num;
            return true;
        }
        if (ofOther == typeof (float))
        {
            float num = (float) (object) value;
            result = (BF16) num;
            return true;
        }
        if (ofOther == typeof (Half))
        {
            Half num = (Half) (object) value;
            result = (BF16) num;
            return true;
        }

        result = new BF16();
        return false;
    }


    /// <summary>
    /// TryConvertFromChecked
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">BF16 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromChecked<TOther>(TOther value, out BF16 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);

    /// <summary>
    /// TryConvertFromSaturating
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">BF16 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromSaturating<TOther>(TOther value, out BF16 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);


    /// <summary>
    /// TryConvertFromTruncating
    /// </summary>
    /// <param name="value">Typed value to convert</param>
    /// <param name="result">BFlaot16 out param</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertFromTruncating<TOther>(TOther value, out BF16 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);



    /// <summary>
    /// TryConvertTo
    /// </summary>
    /// <param name="value">BF16 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
     private static bool TryConvertTo<TOther>(BF16 value,
        [MaybeNullWhen(false)] out TOther result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof(byte))
        {
            byte num = value >= (BF16)byte.MaxValue
                ? byte.MaxValue
                : (value <= (BF16)(byte)0 ? (byte)0 : (byte)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(char))
        {
            char ch = value == BF16.PositiveInfinity
                ? char.MaxValue
                : (value <= BF16.Zero ? char.MinValue : (char)value);
            result = (TOther)Convert.ChangeType((ValueType)ch, ofOther);
            return true;
        }

        if (ofOther == typeof(decimal))
        {
            decimal num = value == BF16.PositiveInfinity
                ? decimal.MaxValue
                : (value == BF16.NegativeInfinity
                    ? decimal.MinValue
                    : (BF16.IsNaN(value) ? 0.0M : (decimal)(float)value));
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(ushort))
        {
            ushort num = value == BF16.PositiveInfinity
                ? ushort.MaxValue
                : (value <= BF16.Zero ? (ushort)0 : (ushort)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(uint))
        {
            uint num = value == BF16.PositiveInfinity
                ? uint.MaxValue
                : (value <= BF16.Zero ? 0U : (uint)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(ulong))
        {
            ulong num = value == BF16.PositiveInfinity
                ? ulong.MaxValue
                : (value <= BF16.Zero ? 0UL : (BF16.IsNaN(value) ?
                    0UL : (ulong)value));
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(UInt128))
        {
            UInt128 uint128 = value == BF16.PositiveInfinity
                ? UInt128.MaxValue
                : (value <= BF16.Zero ? UInt128.MinValue : (UInt128)(float)value);
            result = (TOther)Convert.ChangeType((ValueType)uint128, ofOther);
            return true;
        }

        if (ofOther == typeof(UIntPtr))
        {
            UIntPtr num = value == BF16.PositiveInfinity
                ? UIntPtr.MaxValue
                : (value <= BF16.Zero ? UIntPtr.MinValue : (UIntPtr)value);
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
    /// <param name="value">BF16 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToChecked<TOther>(BF16 value, out TOther result)
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
        if (ofOther == typeof(BF16))
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
    /// <param name="value">BF16 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToSaturating<TOther>(BF16 value, out TOther result)
        where TOther : INumberBase<TOther>
        =>  BF16.TryConvertTo<TOther>(value, out result);

    /// <summary>
    /// TryConvertToTruncating
    /// </summary>
    /// <param name="value">BF16 value to convert</param>
    /// <param name="result">Typed out param</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    public static bool TryConvertToTruncating<TOther>(BF16 value, out TOther result)
        where TOther : INumberBase<TOther>
        =>  BF16.TryConvertTo<TOther>(value, out result);

    #nullable enable

#endif
    #endregion

}

