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


    private const ushort ExponentMantissaMask = 0x7FFF;
        // 0111 1111 1111 1111 (ignores the sign bit)
    private const ushort ExponentMask = 0x7F80;
        // 0111 1111 1000 0000 (covers only the exponent)
    private const ushort MantissaMask = 0x007F;

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
    public static readonly BFloat16 PositiveInfinity = new BFloat16(
        0x7F80);

    /// <summary>
    /// Represents negative infinity.
    /// </summary>
    public static readonly BFloat16 NegativeInfinity = new BFloat16(
        0xFF80);


    #endregion

    /// <summary>
    /// CompareTo
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>0:1</returns>
    /// <exception cref="ArgumentException"></exception>
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
    /// <param name="other"></param>
    /// <returns></returns>
    public int CompareTo(BFloat16 other) => ((float)this).CompareTo(other);

    /// <summary>
    /// Equals
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>bool</returns>
    public readonly override bool Equals(object? obj) =>
        obj is BFloat16 bFloat16 && Equals(bFloat16);

    /// <summary>
    /// Equals
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(BFloat16 other) => this == other;

    /// <summary>
    /// GetHasCode
    /// </summary>
    /// <returns></returns>
    public readonly override int GetHashCode() => RawValue;

    /// <summary>
    /// ToString
    /// </summary>
    /// <param name="format"></param>
    /// <param name="formatProvider"></param>
    /// <returns></returns>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        ((float)this).ToString(format, formatProvider);

    /// <summary>
    /// TryFormat
    /// </summary>
    /// <param name="destination"></param>
    /// <param name="charsWritten"></param>
    /// <param name="format"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public bool TryFormat(Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider)
        => ((float)this).TryFormat(destination, out charsWritten, format, provider );



    internal ushort RawValue { get; }

    internal BFloat16(ushort rawValue)
    {
        RawValue = rawValue;
    }


    /// <summary>
    /// Convert BFloat16 to float
    /// </summary>
    /// <param name="bFloat16"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float BFloat16ToSingle(BFloat16 bFloat16)
    {

        int sign = (ushort)(Unsafe.As<BFloat16, ushort>(ref bFloat16) >> 15) & 0x1;
        int exponent = (ushort)(Unsafe.As<BFloat16, ushort>(ref bFloat16) >> 7) & 0xFF;
        int mantissa = (ushort)Unsafe.As<BFloat16, ushort>(ref bFloat16) & 0x7F;

        int floatBits = (sign << 31) | (exponent << 23) | (mantissa << 16);

        return BitConverter.ToSingle(BitConverter.GetBytes(floatBits), 0);

    }

    /// <summary>
    /// Convert BFloat16 to double
    /// </summary>
    /// <param name="bFloat16"></param>
    /// <returns></returns>
    public static double ConvertToDouble(BFloat16 bFloat16)
    {
        // Extracting sign, exponent, and mantissa from BFloat16
        long sign = ((ushort)Unsafe.As<BFloat16, ushort>(ref bFloat16) >> 15) & 0x1;
        long exponent = ((ushort)Unsafe.As<BFloat16, ushort>(ref bFloat16) >> 7) & 0xFF;
        long mantissa = (ushort)Unsafe.As<BFloat16, ushort>(ref bFloat16) & 0x7F;

        // Adjusting the exponent from BFloat16 bias to double bias
        // BFloat16 and float bias is 127, double bias is 1023
        long adjustedExponent = exponent - 127 + 1023;

        if (adjustedExponent < 0) adjustedExponent = 0; // Underflow
        if (adjustedExponent > 0x7FF) adjustedExponent = 0x7FF; // Overflow

        // Assembling the double (1 bit sign, 11 bits exponent, 52 bits mantissa)
        // Mantissa needs to be shifted to align with double's mantissa
        long doubleBits = (sign << 63) | (adjustedExponent << 52) | (mantissa << 45);

        return BitConverter.Int64BitsToDouble(doubleBits);
    }

    /// <summary>
    /// StripSign
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint StripSign(BFloat16 value)
        => (ushort)((uint)value & 0x7FFF);

    /// <summary>
    /// Convert float to BFloat16
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static BFloat16 SingleToBFloat16(float value)
    {
        // Extracting the binary representation of the float value
        uint floatBits = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);

        // Extracting sign (1 bit)
        uint sign = (floatBits >> 31) & 0x1;

        // Extracting exponent (8 bits)
        uint exponent = (floatBits >> 23) & 0xFF;

        // Extracting mantissa (7 bits)
        uint mantissa = (floatBits >> 16) & 0x7F;

        // Combining into BFloat16 format (1 bit sign, 8 bits exponent, 7 bits mantissa)
        ushort bfloat16 = (ushort)((sign << 15) | (exponent << 7) | mantissa);

        return (BFloat16)bfloat16;
    }

    /// <summary>
    /// Convert double to BFloat16
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static BFloat16 DoubleToBFloat16(double value)
    {
        // Extracting the binary representation of the double value
        ulong doubleBits = BitConverter.ToUInt64(BitConverter.GetBytes(value), 0);

        // Extracting sign (1 bit)
        ulong sign = (doubleBits >> 63) & 0x1;

        // Extracting exponent (8 bits)
        // Note: The exponent of a double is 11 bits, so we need to adjust it for BFloat16
        ulong exponent = (doubleBits >> 52) & 0x7FF;
        int exponentAdjustment =
            ((int)exponent - 1023) + 127; // Adjust from double's exponent bias to float's
        if (exponentAdjustment < 0) exponentAdjustment = 0; // Clamp to zero if underflow
        if (exponentAdjustment > 0xFF)
            exponentAdjustment = 0xFF; // Clamp to max if overflow

        // Extracting mantissa (7 bits)
        // Note: We only take the top 7 bits of the mantissa
        ulong mantissa = (doubleBits >> 45) & 0x7F;

        // Combining into BFloat16 format (1 bit sign, 8 bits exponent, 7 bits mantissa)
        ushort bfloat16 =
            (ushort)((sign << 15) | ((uint)exponentAdjustment << 7) | (uint)mantissa);

        return (BFloat16)bfloat16;
    }


    #region operators




    /// <summary>
    /// Cast float to BFloat16
    /// </summary>
    /// <param name="value"></param>
    /// <returns>BFloat16</returns>
    public static explicit operator BFloat16(float value) => SingleToBFloat16(value);

    /// <summary>
    /// Cast double to BFloat16
    /// </summary>
    /// <param name="value"></param>
    /// <returns>BFloat16</returns>
    public static explicit operator BFloat16(double value) => DoubleToBFloat16(value);

    /// <summary>
    /// Cast BFloat16 to float
    /// </summary>
    /// <param name="value"></param>
    /// <returns>float</returns>
    public static explicit operator float(BFloat16 value) => BFloat16ToSingle(value);

    /// <summary>
    /// Cast BFloat16 to double
    /// </summary>
    /// <param name="value"></param>
    /// <returns>double</returns>
    public static explicit operator double(BFloat16 value) =>
        (double)BFloat16ToSingle(value);

    /// <summary>
    /// Cast Half to BFloat16
    /// </summary>
    /// <param name="halfValue"></param>
    /// <returns>BFloat16</returns>
    public static explicit operator BFloat16(Half halfValue)
        => SingleToBFloat16((float)halfValue);




        /// <summary>
    /// Operator Equals
    /// </summary>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <returns></returns>
    [CompareIntrinisc(CompareKind.Equal)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BFloat16 first, BFloat16 second) =>
        (ushort)Unsafe.As<BFloat16, ushort>(ref first) ==
        (ushort)Unsafe.As<BFloat16, ushort>(ref second);


    /// <summary>
    /// Operator Not Equals
    /// </summary>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <returns></returns>
    [CompareIntrinisc(CompareKind.NotEqual)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator != (BFloat16 first, BFloat16 second) =>
        (ushort)Unsafe.As<BFloat16, ushort>(ref first) !=
        (ushort)Unsafe.As<BFloat16, ushort>(ref second);


    /// <summary>
    /// Operator less than
    /// </summary>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <returns></returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BFloat16 first, BFloat16 second) =>
        (float)first < (float)second;


    /// <summary>
    /// Operator less than or equals
    /// </summary>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <returns></returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BFloat16 first, BFloat16 second) =>
        (float)first <= (float)second;


    /// <summary>
    /// Operator greater than
    /// </summary>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <returns></returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BFloat16 first, BFloat16 second) =>
        (float)first > (float)second;


    /// <summary>
    /// Operator greater than or equals
    /// </summary>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <returns></returns>
    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BFloat16 first, BFloat16 second) =>
        (float)first >= (float)second;



    /// <summary>
    /// Decrement operator
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator --(BFloat16 value) => (BFloat16) ((float) value - 1f);


    /// <summary>
    /// Increment operator
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator ++(BFloat16 value) => (BFloat16) ((float) value + 1f);


    /// <summary>
    /// Modulus operator
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator %(BFloat16 left, BFloat16 right)
        =>  (BFloat16) ((float) left % (float) right);


    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator +(BFloat16 value) => value;

    /// <summary>
    /// Subtraction
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator -(BFloat16 value)
        => BFloat16Extensions.Neg(value);

    /// <summary>
    /// Subtraction
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static BFloat16 operator -(BFloat16 left, BFloat16 right)
        => BFloat16Extensions.SubFP32(left, right);

    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static BFloat16 operator +(BFloat16 left, BFloat16 right)
        => BFloat16Extensions.AddFP32(left, right);

    /// <summary>
    /// Multiplication
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static BFloat16 operator *(BFloat16 left, BFloat16 right)
        => BFloat16Extensions.MulFP32(left,right);

    /// <summary>
    /// Division
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    public static BFloat16 operator /(BFloat16 left, BFloat16 right)
        => BFloat16Extensions.DivFP32(left, right);

    #endregion



    /// <summary>
    /// MultiplicativeIdentity
    /// </summary>
    public static BFloat16 MultiplicativeIdentity  => new BFloat16(0x3F80);


    /// <summary>
    /// Absolute Value
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MathIntrinsic(MathIntrinsicKind.Abs)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 Abs(BFloat16 value) => BFloat16Extensions.Abs(value);

    /// <summary>
    /// Is Bfloat16 Canonical
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsCanonical(BFloat16 value) => true;


    /// <summary>
    /// Is Bfloat16 a complex number
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsComplexNumber(BFloat16 value) => false;


    /// <summary>
    /// Is value finite
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsFinite(BFloat16 value)
        => Bitwise.And(!IsNaN(value), !IsInfinity(value));

    /// <summary>
    /// Is imaginary number
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsImaginaryNumber(BFloat16 value) => false;

    /// <summary>
    /// Is an infinite value?
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsInfinity(BFloat16 value) =>
        (value.RawValue & 0x7F80) == 0x7F80 && (value.RawValue & 0x007F) == 0;


    /// <summary>
    /// Is NaN
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsNaN(BFloat16 value)
        // NaN if all exponent bits are 1 and there is a non-zero value in the mantissa
        =>  (value.RawValue & ExponentMask) == ExponentMask
            && (value.RawValue & MantissaMask) != 0;

    /// <summary>
    /// Is negative?
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsNegative(BFloat16 value) =>  (value.RawValue & 0x8000) != 0;

    /// <summary>
    /// Is negative infinity
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsNegativeInfinity(BFloat16 value) => value == NegativeInfinity;

    /// <summary>
    /// Is normal
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsNormal(BFloat16 value)
    {
        uint num = StripSign(value);
        return num < 0x7F80 && num != 0 && (num & 0x7F80) != 0;
    }


    /// <summary>
    /// Is positive?
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsPositive(BFloat16 value) => (value.RawValue & 0x8000) == 0;

    /// <summary>
    /// Is positive infinity?
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsPositiveInfinity(BFloat16 value) => value == PositiveInfinity;

    /// <summary>
    /// Is real number
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsRealNumber(BFloat16 value)
    {
        bool isExponentAllOnes = (value.RawValue & ExponentMask) == ExponentMask;
        bool isMantissaNonZero = (value.RawValue & MantissaMask) != 0;

        // If exponent is all ones and mantissa is non-zero, it's NaN, so return false
        return !(isExponentAllOnes && isMantissaNonZero);
    }

    /// <summary>
    /// Is subnormal
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsSubnormal(BFloat16 value)
        => (value.RawValue & 0x7F80) == 0 && (value.RawValue & 0x007F) != 0;

    /// <summary>
    /// Is Zero?
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsZero(BFloat16 value)
        => (value.RawValue & ExponentMantissaMask) == 0;

    /// <summary>
    /// MaxMagnitude
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public static BFloat16 MaxMagnitude(BFloat16 x, BFloat16 y)
        =>(BFloat16) MathF.MaxMagnitude((float) x, (float) y);

    /// <summary>
    /// MaxMagnitudeNumber
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
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
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public static BFloat16 MinMagnitude(BFloat16 x, BFloat16 y)
        =>(BFloat16) MathF.MinMagnitude((float) x, (float) y);

    /// <summary>
    /// MinMagnitudeNumber
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public static BFloat16 MinMagnitudeNumber(BFloat16 x, BFloat16 y)
    {
        BFloat16 bf1 = BFloat16.Abs(x);
        BFloat16 bf2 = BFloat16.Abs(y);
        return bf1 < bf2 || BFloat16.IsNaN(bf2) ||
               bf1 == bf2 && BFloat16.IsNegative(x) ? x : y;
    }



    /// <summary>
    /// Parse string to BFloat16
    /// </summary>
    /// <param name="s"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public static BFloat16 Parse(string s, IFormatProvider? provider)
        => (BFloat16)float.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands,
            provider);

    /// <summary>
    /// TryParse string to BFloat16
    /// </summary>
    /// <param name="s"></param>
    /// <param name="provider"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool TryParse(string? s, IFormatProvider? provider, out BFloat16 result)
    {
        float value;
        bool itWorked = float.TryParse(s,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (BFloat16)value;
        return itWorked;

    }




    /// <summary>
    /// Parse Span char
    /// </summary>
    /// <param name="s"></param>
    /// <param name="style"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public static BFloat16 Parse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider)
        => (BFloat16)float.Parse(s, style, provider);

    /// <summary>
    /// Parse string
    /// </summary>
    /// <param name="s"></param>
    /// <param name="style"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public static BFloat16 Parse(string s, NumberStyles style, IFormatProvider? provider)
        => (BFloat16)float.Parse(s, style, provider);


    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s"></param>
    /// <param name="style"></param>
    /// <param name="provider"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider, out BFloat16 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (BFloat16)floatResult;
        return isGood;
    }

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s"></param>
    /// <param name="style"></param>
    /// <param name="provider"></param>
    /// <param name="result"></param>
    /// <returns></returns>
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
    /// IAdditiveIdentity
    /// </summary>
    static BFloat16 IAdditiveIdentity<BFloat16, BFloat16>.AdditiveIdentity
        => new BFloat16((ushort) 0);


    /// <summary>
    /// Is value an integer?
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsInteger(BFloat16 value) => float.IsInteger((float)value);


    /// <summary>
    /// Is an even integer
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEvenInteger(BFloat16 value) =>
         float.IsEvenInteger((float) value);

    /// <summary>
    /// Is odd integer?
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsOddInteger(BFloat16 value) => float.IsOddInteger((float) value);


    /// <summary>
    /// Parse Span of char
    /// </summary>
    /// <param name="s"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public static BFloat16 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => (BFloat16) float.Parse(s, provider);


    /// <summary>
    /// TryParse Span of char
    /// </summary>
    /// <param name="s"></param>
    /// <param name="provider"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider,
        out BFloat16 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, provider, out floatResult);
        result = (BFloat16)floatResult;
        return isGood;
    }


    /// <summary>
    /// TryConvertFrom
    /// </summary>
    /// <param name="value"></param>
    /// <param name="result"></param>
    /// <typeparam name="TOther"></typeparam>
    /// <returns></returns>
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
    /// <param name="value"></param>
    /// <param name="result"></param>
    /// <typeparam name="TOther"></typeparam>
    /// <returns></returns>
    public static bool TryConvertFromChecked<TOther>(TOther value, out BFloat16 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);

    /// <summary>
    /// TryConvertFromSaturating
    /// </summary>
    /// <param name="value"></param>
    /// <param name="result"></param>
    /// <typeparam name="TOther"></typeparam>
    /// <returns></returns>
    public static bool TryConvertFromSaturating<TOther>(TOther value, out BFloat16 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);


    /// <summary>
    /// TryConvertFromTruncating
    /// </summary>
    /// <param name="value"></param>
    /// <param name="result"></param>
    /// <typeparam name="TOther"></typeparam>
    /// <returns></returns>
    public static bool TryConvertFromTruncating<TOther>(TOther value, out BFloat16 result)
        where TOther : INumberBase<TOther> => TryConvertFrom(value, out result);



    /// <summary>
    /// TryConvertTo
    /// </summary>
    /// <param name="value"></param>
    /// <param name="result"></param>
    /// <typeparam name="TOther"></typeparam>
    /// <returns></returns>
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
    /// <param name="value"></param>
    /// <param name="result"></param>
    /// <typeparam name="TOther"></typeparam>
    /// <returns></returns>
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
    /// <param name="value"></param>
    /// <param name="result"></param>
    /// <typeparam name="TOther"></typeparam>
    /// <returns></returns>
    public static bool TryConvertToSaturating<TOther>(BFloat16 value, out TOther result)
        where TOther : INumberBase<TOther>
        =>  BFloat16.TryConvertTo<TOther>(value, out result);

    /// <summary>
    /// TryConvertToTruncating
    /// </summary>
    /// <param name="value"></param>
    /// <param name="result"></param>
    /// <typeparam name="TOther"></typeparam>
    /// <returns></returns>
    public static bool TryConvertToTruncating<TOther>(BFloat16 value, out TOther result)
        where TOther : INumberBase<TOther>
        =>  BFloat16.TryConvertTo<TOther>(value, out result);

    #nullable enable



#endif

}

