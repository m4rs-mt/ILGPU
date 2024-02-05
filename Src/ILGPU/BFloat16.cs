using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ILGPU;

#if NET7_0_OR_GREATER

/// <summary>
/// BFloat16 Implementation
/// </summary>
public readonly struct BFloat16 : INumber<BFloat16>
{

    private const ushort ExponentMantissaMask = 0x7FFF; // 0111 1111 1111 1111 (ignores the sign bit)
    private const ushort ExponentMask = 0x7F80;        // 0111 1111 1000 0000 (covers only the exponent)
    private const ushort MantissaMask = 0x007F;

    public static int Radix => 2;

    static BFloat16 _zero = new BFloat16(0x0000);

    static BFloat16 _one = new BFloat16(0x3F80);

    public static BFloat16 Zero { get { return _zero; } }
    public static BFloat16 One { get { return _one; } }


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



    public static explicit operator BFloat16(float value) => SingleToBFloat16(value);
    public static explicit operator BFloat16(double value) => DoubleToBFloat16(value);
    public static explicit operator float(BFloat16 value) => BFloat16ToSingle(value);
    public static explicit operator double(BFloat16 value) =>
        (double)BFloat16ToSingle(value);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(object? obj)
    {

        if (obj is BFloat16 other)
            return CompareTo((BFloat16)other);
        if (obj != null)
            throw new ArgumentException("Must be " + nameof(BFloat16));
        return 1;

    }

    public int CompareTo(BFloat16 other) => ((float)this).CompareTo(other);

    public readonly override bool Equals(object? obj) =>
        obj is BFloat16 bFloat16 && Equals(bFloat16);

    public bool Equals(BFloat16 other) => this == other;

    public readonly override int GetHashCode() => RawValue;

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        ((float)this).ToString(format, formatProvider);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format,
        IFormatProvider? provider)
        => ((float)this).TryFormat(destination, out charsWritten, format, provider );


    internal ushort RawValue { get; }

    internal BFloat16(ushort rawValue)
    {
        RawValue = rawValue;
    }

    [CompareIntrinisc(CompareKind.Equal)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BFloat16 first, BFloat16 second) =>
        (ushort)Unsafe.As<BFloat16, ushort>(ref first) ==
        (ushort)Unsafe.As<BFloat16, ushort>(ref second);


    [CompareIntrinisc(CompareKind.NotEqual)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator != (BFloat16 first, BFloat16 second) =>
        (ushort)Unsafe.As<BFloat16, ushort>(ref first) !=
        (ushort)Unsafe.As<BFloat16, ushort>(ref second);


    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BFloat16 first, BFloat16 second) =>
        (float)first < (float)second;

    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BFloat16 first, BFloat16 second) =>
        (float)first <= (float)second;


    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BFloat16 first, BFloat16 second) =>
        (float)first > (float)second;


    [CompareIntrinisc(CompareKind.LessThan)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BFloat16 first, BFloat16 second) =>
        (float)first >= (float)second;

    public static float BFloat16ToSingle(BFloat16 bFloat16)
    {

        int sign = (ushort)(Unsafe.As<BFloat16, ushort>(ref bFloat16) >> 15) & 0x1;
        int exponent = (ushort)(Unsafe.As<BFloat16, ushort>(ref bFloat16) >> 7) & 0xFF;
        int mantissa = (ushort)Unsafe.As<BFloat16, ushort>(ref bFloat16) & 0x7F;

        int floatBits = (sign << 31) | (exponent << 23) | (mantissa << 16);

        return BitConverter.ToSingle(BitConverter.GetBytes(floatBits), 0);

    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint StripSign(BFloat16 value)
        => (ushort)((uint)value & 0x7FFF);

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


    public static BFloat16 Parse(string s, IFormatProvider? provider)
        => (BFloat16)float.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands,
            provider);

    public static bool TryParse(string? s, IFormatProvider? provider, out BFloat16 result)
    {
        float value;
        bool itWorked = float.TryParse(s,
            NumberStyles.Float | NumberStyles.AllowThousands, provider, out value);
        result = (BFloat16)value;
        return itWorked;

    }

    public static BFloat16 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => (BFloat16) float.Parse(s, provider);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out BFloat16 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, provider, out floatResult);
        result = (BFloat16)floatResult;
        return isGood;
    }

    /// <summary>
    /// IAdditiveIdentity
    /// </summary>
    static BFloat16 IAdditiveIdentity<BFloat16, BFloat16>.AdditiveIdentity
        => new BFloat16((ushort) 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator --(BFloat16 value) => (BFloat16) ((float) value - 1f);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator ++(BFloat16 value) => (BFloat16) ((float) value + 1f);


    /// <summary>
    /// Modulus
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator %(BFloat16 left, BFloat16 right)
        =>  (BFloat16) ((float) left % (float) right);


    /// <summary>
    /// MultiplicativeIdentity
    /// </summary>
    public static BFloat16 MultiplicativeIdentity  => new BFloat16(0x3F80);

    /// <summary>
    /// Addition
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator +(BFloat16 value) => value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 operator -(BFloat16 value)
        => BFloat16Extensions.Neg(value);

    public static BFloat16 operator -(BFloat16 left, BFloat16 right)
        => BFloat16Extensions.SubFP32(left, right);

    public static BFloat16 operator +(BFloat16 left, BFloat16 right)
        => BFloat16Extensions.AddFP32(left, right);

    public static BFloat16 operator *(BFloat16 left, BFloat16 right)
        => BFloat16Extensions.MulFP32(left,right);

    public static BFloat16 operator /(BFloat16 left, BFloat16 right)
        => BFloat16Extensions.DivFP32(left, right);

    [MathIntrinsic(MathIntrinsicKind.Abs)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BFloat16 Abs(BFloat16 value) => BFloat16Extensions.Abs(value);

    public static bool IsCanonical(BFloat16 value) => true;

    public static bool IsComplexNumber(BFloat16 value) => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEvenInteger(BFloat16 value) =>
         float.IsEvenInteger((float) value);

    public static bool IsFinite(BFloat16 value)
        => Bitwise.And(!IsNaN(value), !IsInfinity(value));
    public static bool IsImaginaryNumber(BFloat16 value) => false;

    public static bool IsInfinity(BFloat16 value) =>
        (value.RawValue & 0x7F80) == 0x7F80 && (value.RawValue & 0x007F) == 0;
    public static bool IsInteger(BFloat16 value) => float.IsInteger((float)value);

    public static bool IsNaN(BFloat16 bfloat16)
        // NaN if all exponent bits are 1 and there is a non-zero value in the mantissa
        =>  (bfloat16.RawValue & ExponentMask) == ExponentMask && (bfloat16.RawValue & MantissaMask) != 0;

    public static bool IsNegative(BFloat16 value) =>  (value.RawValue & 0x8000) != 0;
    public static bool IsNegativeInfinity(BFloat16 value) => value == NegativeInfinity;
    public static bool IsNormal(BFloat16 value)
    {
        uint num = StripSign(value);
        return num < 0x7F80 && num != 0 && (num & 0x7F80) != 0;
    }
    public static bool IsOddInteger(BFloat16 value) => float.IsOddInteger((float) value);

    public static bool IsPositive(BFloat16 value) => (value.RawValue & 0x8000) == 0;

    public static bool IsPositiveInfinity(BFloat16 value) => throw new NotImplementedException();

    public static bool IsRealNumber(BFloat16 value)
    {
        bool isExponentAllOnes = (value.RawValue & ExponentMask) == ExponentMask;
        bool isMantissaNonZero = (value.RawValue & MantissaMask) != 0;

        // If exponent is all ones and mantissa is non-zero, it's NaN, so return false
        return !(isExponentAllOnes && isMantissaNonZero);
    }
    public static bool IsSubnormal(BFloat16 value)
        => (value.RawValue & 0x7F80) == 0 && (value.RawValue & 0x007F) != 0;

    public static bool IsZero(BFloat16 value) => (value.RawValue & ExponentMantissaMask) == 0;

    public static BFloat16 MaxMagnitude(BFloat16 x, BFloat16 y)
        =>(BFloat16) MathF.MaxMagnitude((float) x, (float) y);
    public static BFloat16 MaxMagnitudeNumber(BFloat16 x, BFloat16 y)
    {
        BFloat16 bf1 = BFloat16.Abs(x);
        BFloat16 bf2 = BFloat16.Abs(y);
        return bf1 > bf2 || BFloat16.IsNaN(bf2) || bf1
            == bf2 && !BFloat16.IsNegative(x) ? x : y;
    }
    public static BFloat16 MinMagnitude(BFloat16 x, BFloat16 y)
        =>(BFloat16) MathF.MinMagnitude((float) x, (float) y);
    public static BFloat16 MinMagnitudeNumber(BFloat16 x, BFloat16 y)
    {
        BFloat16 bf1 = BFloat16.Abs(x);
        BFloat16 bf2 = BFloat16.Abs(y);
        return bf1 < bf2 || BFloat16.IsNaN(bf2) ||
               bf1 == bf2 && BFloat16.IsNegative(x) ? x : y;
    }
    public static BFloat16 Parse(ReadOnlySpan<char> s, NumberStyles style,
        IFormatProvider? provider)
        => (BFloat16)float.Parse(s, style, provider);

    public static BFloat16 Parse(string s, NumberStyles style, IFormatProvider? provider)
        => (BFloat16)float.Parse(s, style, provider);

    public static bool TryConvertFromChecked<TOther>(TOther value, out BFloat16 result) where TOther : INumberBase<TOther> => throw new NotImplementedException();

    public static bool TryConvertFromSaturating<TOther>(TOther value, out BFloat16 result) where TOther : INumberBase<TOther> => throw new NotImplementedException();

    public static bool TryConvertFromTruncating<TOther>(TOther value, out BFloat16 result) where TOther : INumberBase<TOther> => throw new NotImplementedException();

    public static bool TryConvertToChecked<TOther>(BFloat16 value, out TOther result) where TOther : INumberBase<TOther> => throw new NotImplementedException();

    public static bool TryConvertToSaturating<TOther>(BFloat16 value, out TOther result) where TOther : INumberBase<TOther> => throw new NotImplementedException();

    public static bool TryConvertToTruncating<TOther>(BFloat16 value, out TOther result) where TOther : INumberBase<TOther> => throw new NotImplementedException();

    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider,
        out BFloat16 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (BFloat16)floatResult;
        return isGood;
    }

    public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider,
        out BFloat16 result)
    {
        float floatResult;
        bool isGood = float.TryParse(s, style, provider, out floatResult);
        result = (BFloat16)floatResult;
        return isGood;
    }

}
#endif
