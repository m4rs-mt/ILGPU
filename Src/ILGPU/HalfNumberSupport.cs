// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: HalfNumberSupport.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU;

// for testing System.Half
// System.Half has several equality and casting errors.
#if USESYSTEMHALF && NET7_0_OR_GREATER
#elif NET7_0_OR_GREATER

public readonly partial struct Half : INumber<Half>
{

    #region constants

    private const ushort PositiveInfinityBits = 0x7C00;
    internal const ushort BiasedExponentMask = 0x7C00;

    #endregion

    #region IComparable

    /// <summary>
    /// CompareTo
    /// </summary>
    /// <param name="obj">value for comparison</param>
    /// <returns>true Half comparison of object when object is half value</returns>
    /// <exception cref="ArgumentException">Thrown when not Half value</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(object? obj)
    {
        if (obj is Half other)
            return CompareTo((Half)other);
        if (obj != null)
            throw new ArgumentException("Must be "+nameof(Half));
        return 1;
    }


    #endregion

    /// <summary>
    /// Parse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Float value of parse</returns>
    public static Half Parse(string s, IFormatProvider? provider)
        => (Half) float.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands,
            provider);


    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Parsed value</param>
    /// <returns>True when parse worked</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider?
            provider, out Half result)
    {
        float value;
        bool itWorked = float.TryParse(s,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (Half)value;
        return itWorked;
    }

    /// <summary>
    /// Parse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Half value</returns>
    public static Half Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => (Half) float.Parse(s, provider);


    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Out Half value</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider,
        out Half result)
    {
        float value;
        bool itWorked = float.TryParse(s,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (Half)value;
        return itWorked;

    }


    /// <summary>
    /// IsCanonical
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <returns>True based on type</returns>
    public static bool IsCanonical(Half value) => true;

    /// <summary>
    /// IsComplexNumber
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <returns>Returns false based on type</returns>
    public static bool IsComplexNumber(Half value) => false;

    /// <summary>
    /// IsEvenInteger
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <returns>True if value is an even integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEvenInteger(Half value) => float.IsEvenInteger((float) value);

    /// <summary>
    /// IsImaginaryNumber
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <returns>Returns false for Half type</returns>
    public static bool IsImaginaryNumber(Half value) => false;

    /// <summary>
    /// IsInteger
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <returns>Returns true when value is an integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInteger(Half value) => float.IsInteger((float) value);

    /// <summary>
    /// IsNegative
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <returns>True when value is less than 0</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNegative(Half value) =>  (short) value < (short) 0;

    /// <summary>
    /// StripSign
    /// </summary>
    /// <param name="value">Half to check sign</param>
    /// <returns>Sign bit</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint StripSign(Half value)
        => (uint) (ushort) ((uint) value & 4294934527U);


    /// <summary>
    /// IsNormal
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <returns>True when value is normal</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNormal(Half value)
    {
        uint absValue = StripSign(value);
        return (absValue < PositiveInfinityBits)
               && (absValue != 0)
               && ((absValue & BiasedExponentMask) != 0);
    }


    /// <summary>
    /// IsOddInteger
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <returns>True when value is an odd integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOddInteger(Half value) => float.IsOddInteger((float) value);

    /// <summary>
    /// IsPositive
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <returns>True when value greater than or equal to zero</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPositive(Half value) => (short) value >= (short) 0;


    /// <summary>
    /// IsRealNumber
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <returns>True when value is exact match with itself</returns>
    public static bool IsRealNumber(Half value)
#pragma warning disable CS1718
        => (value == value);
#pragma warning restore CS1718


    /// <summary>
    /// IsSubnormal
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <returns>True when value is subnormal</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSubnormal(Half value)
    {
        uint absValue = StripSign(value);
        return (absValue < PositiveInfinityBits)
               && (absValue != 0)
               && ((absValue & BiasedExponentMask) == 0);
    }

    /// <summary>
    /// MaxMagnitude
    /// </summary>
    /// <param name="x">Half 1 to compare</param>
    /// <param name="y">Half 2 to compare</param>
    /// <returns>Larger value or NaN when equal</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half MaxMagnitude(Half x, Half y) =>
        (Half) MathF.MaxMagnitude((float) x, (float) y);


    /// <summary>
    /// MaxMagnitudeNumber
    /// </summary>
    /// <param name="x">Half 1 to compare</param>
    /// <param name="y">Half 2 to compare</param>
    /// <returns>Largest absolute number that is not NaN </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half MaxMagnitudeNumber(Half x, Half y)
    {
        Half half1 = Half.Abs(x);
        Half half2 = Half.Abs(y);
        return half1 > half2 || Half.IsNaN(half2) || half1
            == half2 && !Half.IsNegative(x) ? x : y;
    }

    /// <summary>
    /// MinMagnitude
    /// </summary>
    /// <param name="x">Half 1 to compare</param>
    /// <param name="y">Half 2 to compare</param>
    /// <returns>Smallest number or NaN when equal</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half MinMagnitude(Half x, Half y) =>
        (Half) MathF.MinMagnitude((float) x, (float) y);

    /// <summary>
    /// MinMagnitudeNumber
    /// </summary>
    /// <param name="x">Half 1 to compare</param>
    /// <param name="y">Half 2 to compare</param>
    /// <returns>Smallest absolute number</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half MinMagnitudeNumber(Half x, Half y)
    {
        Half half1 = Half.Abs(x);
        Half half2 = Half.Abs(y);
        return half1 < half2 || Half.IsNaN(half2) ||
               half1 == half2 && Half.IsNegative(x) ? x : y;
    }

    /// <summary>
    /// Parse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formating attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Parsed Half Value</returns>
    public static Half Parse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider) => (Half) float.Parse(s, style, provider);

    /// <summary>
    /// Parse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Style formatting attributes</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Parsed Half value</returns>
    public static Half Parse(string s, NumberStyles style, IFormatProvider? provider)
        => (Half) float.Parse(s, style, provider);

#if NET8_0_OR_GREATER

    #region INumberBase


    /// <summary>
    /// Parse
    /// </summary>
    /// <param name="utf8Text">Uft8 encoded by span to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>Parsed Half Value</returns>
    public static Half Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
        => (Half) float.Parse(utf8Text, provider);

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="utf8Text">Utf8 encoded byte span to parse</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">True when successful</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider,
        out Half result)
    {
        float value;
        bool itWorked = float.TryParse(utf8Text,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (Half)value;
        return itWorked;
    }


    /// <summary>
    /// TryConvertFrom
    /// </summary>
    /// <param name="value">Value to convert</param>
    /// <param name="result">Have value result</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>Try is successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertFrom<TOther>(TOther value, out Half result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof (double))
        {
            double num = (double) (object) value;
            result = (Half) num;
            return true;
        }
        if (ofOther == typeof (short))
        {
            short num = (short) (object) value;
            result = (Half) num;
            return true;
        }
        if (ofOther == typeof (int))
        {
            int num = (int) (object) value;
            result = (Half) num;
            return true;
        }
        if (ofOther == typeof (long))
        {
            long num = (long) (object) value;
            result = (Half) num;
            return true;
        }
        if (ofOther == typeof (Int128))
        {
            Int128 int128 = (Int128) (object) value;
            result = (Half)(float) int128;
            return true;
        }
        if (ofOther == typeof (IntPtr))
        {
            IntPtr num = (IntPtr) (object) value;
            result = (Half) num;
            return true;
        }
        if (ofOther == typeof (sbyte))
        {
            sbyte num = (sbyte) (object) value;
            result = (Half) num;
            return true;
        }
        if (ofOther == typeof (float))
        {
            float num = (float) (object) value;
            result = (Half) num;
            return true;
        }
        result = new Half();
        return false;
    }


    /// <summary>
    /// TryConvertFromChecked
    /// </summary>
    /// <param name="value">Value to convert</param>
    /// <param name="result">Half result</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool INumberBase<Half>.TryConvertFromChecked<TOther>(TOther value,
        out Half result)
        => Half.TryConvertFrom<TOther>(value, out result);

    /// <summary>
    /// TryConvertFromSaturating
    /// </summary>
    /// <param name="value">Value to convert</param>
    /// <param name="result">Half value result</param>
    /// <typeparam name="TOther">Type to convert from</typeparam>
    /// <returns>True when successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool INumberBase<Half>.TryConvertFromSaturating<TOther>(TOther value,
        out Half result)
        => Half.TryConvertFrom<TOther>(value, out result);


    /// <summary>
    /// TryConvertFromTruncating
    /// </summary>
    /// <param name="value">Value to convert</param>
    /// <param name="result">Half value when successful</param>
    /// <typeparam name="TOther">Type to convert</typeparam>
    /// <returns>True when successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool INumberBase<Half>.TryConvertFromTruncating<TOther>(TOther value,
        out Half result)
        => Half.TryConvertFrom<TOther>(value, out result);


    /// <summary>
    /// TryConvertToChecked
    /// </summary>
    /// <param name="value">Value to convert</param>
    /// <param name="result">value in desired type</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool INumberBase<Half>.TryConvertToChecked<TOther>(Half value,
        [MaybeNullWhen(false)] out TOther result)
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
    /// TryConvertTo
    /// </summary>
    /// <param name="value">Value to convert</param>
    /// <param name="result">Value converted</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConvertTo<TOther>(Half value,
        [MaybeNullWhen(false)] out TOther result)
        where TOther : INumberBase<TOther>
    {
        Type ofOther = typeof(TOther);
        if (ofOther == typeof(byte))
        {
            byte num = value >= (Half)byte.MaxValue
                ? byte.MaxValue
                : (value <= (Half)(byte)0 ? (byte)0 : (byte)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(char))
        {
            char ch = value == Half.PositiveInfinity
                ? char.MaxValue
                : (value <= Half.Zero ? char.MinValue : (char)value);
            result = (TOther)Convert.ChangeType((ValueType)ch, ofOther);
            return true;
        }

        if (ofOther == typeof(decimal))
        {
            decimal num = value == Half.PositiveInfinity
                ? decimal.MaxValue
                : (value == Half.NegativeInfinity
                    ? decimal.MinValue
                    : (Half.IsNaN(value) ? 0.0M : (decimal)(float)value));
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(ushort))
        {
            ushort num = value == Half.PositiveInfinity
                ? ushort.MaxValue
                : (value <= Half.Zero ? (ushort)0 : (ushort)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(uint))
        {
            uint num = value == Half.PositiveInfinity
                ? uint.MaxValue
                : (value <= Half.Zero ? 0U : (uint)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(ulong))
        {
            ulong num = value == Half.PositiveInfinity
                ? ulong.MaxValue
                : (value <= Half.Zero ? 0UL : (Half.IsNaN(value) ? 0UL : (ulong)value));
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        if (ofOther == typeof(UInt128))
        {
            UInt128 uint128 = value == Half.PositiveInfinity
                ? UInt128.MaxValue
                : (value <= Half.Zero ? UInt128.MinValue : (UInt128)(float)value);
            result = (TOther)Convert.ChangeType((ValueType)uint128, ofOther);
            return true;
        }

        if (ofOther == typeof(UIntPtr))
        {
            UIntPtr num = value == Half.PositiveInfinity
                ? UIntPtr.MaxValue
                : (value <= Half.Zero ? UIntPtr.MinValue : (UIntPtr)value);
            result = (TOther)Convert.ChangeType((ValueType)num, ofOther);
            return true;
        }

        result = default(TOther);
        return false;
    }

    /// <summary>
    /// TryConvertToSaturating
    /// </summary>
    /// <param name="value">Value to convert</param>
    /// <param name="result">Value converted</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool INumberBase<Half>.TryConvertToSaturating<TOther>(Half value,
        [MaybeNullWhen(false)] out TOther result)
        => Half.TryConvertTo<TOther>(value, out result);

    /// <summary>
    /// TryConvertToTruncating
    /// </summary>
    /// <param name="value">Value to convert</param>
    /// <param name="result">Value converted</param>
    /// <typeparam name="TOther">Type to convert to</typeparam>
    /// <returns>True when successful</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool INumberBase<Half>.TryConvertToTruncating<TOther>(Half value,
        [MaybeNullWhen(false)] out TOther result)
        => Half.TryConvertTo<TOther>(value, out result);

    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Number style to consider</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Out Half when successful</param>
    /// <returns>True when successful</returns>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider,
        out Half result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (Half)floatResult;
        return isGood;
    }


    /// <summary>
    /// TryParse
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="style">Number style to consider</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <param name="result">Out half when successful</param>
    /// <returns>True when successful</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, NumberStyles style,
        IFormatProvider? provider,
        out Half result)
    {
        float floatResult;

        bool isGood = float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands,
            provider, out floatResult);
        result = (Half) floatResult;
        return isGood;
    }



    /// <summary>
    /// One
    /// </summary>
    public static Half One  => new Half(0x1);

    /// <summary>
    /// Zero
    /// </summary>
    public static Half Zero => new Half(0x0);


    /// <inheritdoc cref="INumberBase{TSelf}.Radix" />
    static int INumberBase<Half>.Radix => 2;





    /// <summary>
    /// IAdditiveIdentity
    /// </summary>
    static Half IAdditiveIdentity<Half, Half>.AdditiveIdentity => new Half((ushort) 0);

    /// <summary>
    /// Decrement
    /// </summary>
    /// <param name="value">Value to decrement</param>
    /// <returns>Decremented Half</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half operator --(Half value) => (Half) ((float) value - 1f);

    /// <summary>
    /// Increment
    /// </summary>
    /// <param name="value">Value to increment</param>
    /// <returns>Incremented value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half operator ++(Half value) => (Half) ((float) value + 1f);

    /// <summary>
    /// Modulus
    /// </summary>
    /// <param name="left">First Half value</param>
    /// <param name="right">Second Half value</param>
    /// <returns>Modulus of Left via right</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half operator %(Half left, Half right)
        =>  (Half) ((float) left % (float) right);

    /// <summary>
    /// MultiplicativeIdentity
    /// </summary>
    public static Half MultiplicativeIdentity  => new Half((ushort) 15360);

    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="value">Half Value</param>
    /// <returns>Half Value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half operator +(Half value) => value;

    #endregion


    /// <summary>
    /// TryFormat
    /// </summary>
    /// <param name="destination">Target span to fill when sucessful</param>
    /// <param name="charsWritten">Number of chars when successful</param>
    /// <param name="format">Format string as Span</param>
    /// <param name="provider">Culture specific parsing provider</param>
    /// <returns>True when successful</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider) =>
        ((float)this).TryFormat(destination, out charsWritten, format, provider );

    /// <summary>
    /// ToString
    /// </summary>
    /// <param name="format">Desired format</param>
    /// <param name="formatProvider">Culture specific parsing provider</param>
    /// <returns>Half as string per format/culture</returns>
    public string ToString([StringSyntax("NumericFormat")] string? format,
        IFormatProvider? formatProvider)
        => ((float) this).ToString(format, formatProvider);

#endif
}


#endif

