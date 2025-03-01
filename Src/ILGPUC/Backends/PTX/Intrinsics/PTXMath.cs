// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXMath.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPUC.Intrinsic;
using System.Runtime.CompilerServices;

// disable: max_line_length

namespace ILGPUC.Backends.PTX.Intrinsics;

/// <summary>
/// Custom PTX-specific implementations.
/// </summary>
static partial class PTXMath
{
    #region Rem

    /// <summary cref="XMath.Rem(double, double)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Rem(double x, double y) => GenericMath.Rem(x, y);

    /// <summary cref="XMath.Rem(float, float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Rem(float x, float y) => GenericMath.Rem(x, y);

    /// <summary cref="XMath.IEEERemainder(double, double)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double IEEERemainder(double x, double y) =>
        GenericMath.IEEERemainder(x, y);

    /// <summary cref="XMath.IEEERemainder(float, float)"/>
    public static float IEEERemainder(float x, float y) =>
        GenericMath.IEEERemainder(x, y);

    #endregion

    #region Sqrt

    /// <summary cref="XMath.Rsqrt(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Rsqrt(double value) =>
        XMath.Rcp(XMath.Sqrt(value));

    /// <summary cref="XMath.Rsqrt(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Rsqrt(float value) =>
        XMath.Rcp(XMath.Sqrt(value));

    #endregion

    #region Floor & Ceiling

    /// <summary cref="XMath.Floor(double)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Floor(double value) =>
        XMath.RoundingModes.RoundToNegativeInfinity(value);

    /// <summary cref="XMath.Floor(float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Floor(float value) =>
        XMath.RoundingModes.RoundToNegativeInfinity(value);

    /// <summary cref="XMath.Ceiling(double)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Ceiling(double value) =>
        XMath.RoundingModes.RoundToPositiveInfinity(value);

    /// <summary cref="XMath.Ceiling(float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Ceiling(float value) =>
        XMath.RoundingModes.RoundToPositiveInfinity(value);

    #endregion

    #region Trig

    /// <summary cref="XMath.Sin(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sin(double value) =>
        Cordic.Sin(value);

    /// <summary cref="XMath.Sinh(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sinh(double value) =>
        0.5 * (Exp(value) - Exp(-value));

    /// <summary cref="XMath.Sinh(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sinh(float value) =>
        0.5f * (Exp(value) - Exp(-value));

    /// <summary cref="XMath.Asin(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Asin(double value)
    {
        if (XMath.IsNaN(value) ||
            value < -1.0 ||
            value > 1.0)
        {
            return double.NaN;
        }

        if (value == 1.0)
            return XMath.PIHalfD;
        else if (value == -1.0)
            return -XMath.PIHalfD;

        double arg = value * Rsqrt(1.0 - value * value);
        return Atan(arg);
    }

    /// <summary cref="XMath.Asin(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Asin(float value)
    {
        if (XMath.IsNaN(value) ||
            value < -1.0f ||
            value > 1.0f)
        {
            return float.NaN;
        }

        if (value == 1.0f)
            return XMath.PIHalf;
        else if (value == -1.0f)
            return -XMath.PIHalf;

        float arg = value * Rsqrt(1.0f - value * value);
        return Atan(arg);
    }

    /// <summary cref="XMath.Cos(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Cos(double value) =>
        Cordic.Cos(value);

    /// <summary cref="XMath.Cosh(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Cosh(double value) =>
        0.5 * (Exp(value) + Exp(-value));

    /// <summary cref="XMath.Cosh(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cosh(float value) =>
        0.5f * (Exp(value) + Exp(-value));

    /// <summary cref="XMath.Acos(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Acos(double value) =>
        XMath.PIHalfD - Asin(value);

    /// <summary cref="XMath.Acos(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Acos(float value) =>
        XMath.PIHalf - Asin(value);

    /// <summary cref="XMath.Tan(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Tan(double value) =>
        Cordic.Tan(value);

    /// <summary cref="XMath.Tan(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Tan(float value) =>
        Cordic.Tan(value);

    /// <summary cref="XMath.Tanh(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Tanh(double value)
    {
        if (XMath.IsNaN(value))
            return value;
        else if (value == double.PositiveInfinity)
            return 1.0;
        else if (value == double.NegativeInfinity)
            return -1.0;

        var exp = Exp(2.0 * value);
        var denominator = XMath.Rcp(exp + 1.0);
        return (exp - 1.0) * denominator;
    }

    /// <summary cref="XMath.Tanh(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Tanh(float value)
    {
        if (XMath.IsNaN(value))
            return value;
        else if (value == float.PositiveInfinity)
            return 1.0f;
        else if (value == float.NegativeInfinity)
            return -1.0f;

        var exp = Exp(2.0f * value);
        var denominator = XMath.Rcp(exp + 1.0f);
        return (exp - 1.0f) * denominator;
    }

    /// <summary cref="XMath.Atan(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Atan(double value) =>
        Cordic.Atan(value);

    /// <summary cref="XMath.Atan(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Atan(float value) =>
        Cordic.Atan(value);

    /// <summary cref="XMath.Atan2(double, double)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Atan2(double y, double x) =>
        Cordic.Atan2(y, x);

    /// <summary cref="XMath.Atan2(float, float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Atan2(float y, float x) =>
        Cordic.Atan2(y, x);

    #endregion

    #region Pow

    //
    // List of expected return values for Math.Pow
    //
    // Source: https://docs.microsoft.com/en-us/dotnet/api/system.math.pow
    //
    // IMPORTANT: There are implementation differences between .NET Framework and .NET Core.
    // For example, calling Math.Pow(NaN, 0.0) :
    //  net471 returns NaN, following rule #1
    //  netcoreapp2.1 returns 1.0, following rule #2
    //
    // IMPORTANT: Our unit tests currently run using netcoreapp2.1, so we are matching that implementation.
    // Also, netcoreapp2.1 appears to more closely match the IEEE 754 standard.
    //
    // Rule #   Parameters                                  Return value            Notes
    // (1)      x or y = NaN.                               NaN
    //
    // (2)      x = Any value except NaN; y = 0.            1                       * netcoreapp2.1 returns 1 even when y = NaN
    //                                                                              * netcoreapp2.1 applies this first, before Rule #1
    //
    // (3)      x = NegativeInfinity; y < 0.                0
    //
    // (4)      x = NegativeInfinity; y is a positive       NegativeInfinity
    //          odd integer.
    //
    // (5)      x = NegativeInfinity; y is positive         PositiveInfinity
    //          but not an odd integer.
    //
    // (6)      x < 0 but not NegativeInfinity; y is        NaN
    //          not an integer, NegativeInfinity, or
    //          PositiveInfinity.
    //
    // (7)      x = -1; y = NegativeInfinity or             NaN                     * netcoreapp2.1 returns 1
    //          PositiveInfinity.                                                   * net471 returns NaN
    //
    // (8)      -1 < x < 1; y = NegativeInfinity.           PositiveInfinity
    //
    // (9)      -1 < x < 1; y = PositiveInfinity.           0
    //
    // (10)     x < -1 or x > 1; y = NegativeInfinity.      0
    //
    // (11)     x < -1 or x > 1; y = PositiveInfinity.      PositiveInfinity
    //
    // (12)     x = 0; y < 0.                               PositiveInfinity
    //
    // (13)     x = 0; y > 0.                               0
    //
    // (14)     x = 1; y is any value except NaN.           1                       * netcoreapp2.1 applies this first, before Rule #1
    //
    // (15)     x = PositiveInfinity; y < 0.                0
    //
    // (16)     x = PositiveInfinity; y > 0.                PositiveInfinity
    //

    /// <summary cref="XMath.Pow(double, double)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Pow(double @base, double exp)
    {
        if (exp == 0.0)
        {
            // Rule #2
            // Ignoring Rule #1
            return 1.0;
        }
        else if (@base == 1.0)
        {
            // Rule #14 but ignoring second part about y = NaN
            // Ignoring Rule #1
            return 1.0;
        }
        else if (XMath.IsNaN(@base) || XMath.IsNaN(exp))
        {
            // Rule #1
            return double.NaN;
        }
        else if (@base == double.NegativeInfinity)
        {
            if (exp < 0.0)
            {
                // Rule #3
                return 0.0;
            }
            else if (IsOddInteger(exp))
            {
                // Rule #4
                return double.NegativeInfinity;
            }
            else
            {
                // Rule #5
                return double.PositiveInfinity;
            }
        }
        else if (@base < 0.0 && !IsInteger(exp) && !XMath.IsInfinity(exp))
        {
            // Rule #6
            return double.NaN;
        }
        else if (@base == -1.0 && XMath.IsInfinity(exp))
        {
            // Rule #7
            return 1.0;
        }
        else if (-1.0 < @base && @base < 1.0 && exp == double.NegativeInfinity)
        {
            // Rule #8
            return double.PositiveInfinity;
        }
        else if (-1.0 < @base && @base < 1.0 && exp == double.PositiveInfinity)
        {
            // Rule #9
            return 0.0;
        }
        else if ((@base < -1.0 || @base > 1.0) && exp == double.NegativeInfinity)
        {
            // Rule #10
            return 0.0;
        }
        else if ((@base < -1.0 || @base > 1.0) && exp == double.PositiveInfinity)
        {
            // Rule #11
            return double.PositiveInfinity;
        }
        else if (@base == 0.0)
        {
            if (exp < 0.0)
            {
                // Rule #12
                return double.PositiveInfinity;
            }
            else
            {
                // Rule #13
                // NB: exp == 0.0 already handled by Rule #2
                return 0.0;
            }
        }
        else if (@base == double.PositiveInfinity)
        {
            if (exp < 0.0)
            {
                // Rule #15
                return 0.0;
            }
            else
            {
                // Rule #16
                // NB: exp == 0.0 already handled by Rule #2
                return double.PositiveInfinity;
            }
        }

        if (@base < 0.0)
        {
            // 'exp' is an integer, due to Rule #6.
            var sign = IsOddInteger(exp) ? -1.0 : 1.0;
            return sign * Exp(exp * Log(XMath.Abs(@base)));
        }

        return Exp(exp * Log(@base));
    }

    /// <summary cref="XMath.Pow(float, float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Pow(float @base, float exp)
    {
        if (exp == 0.0f)
        {
            // Rule #2
            // Ignoring Rule #1
            return 1.0f;
        }
        else if (@base == 1.0f)
        {
            // Rule #14 but ignoring second part about y = NaN
            // Ignoring Rule #1
            return 1.0f;
        }
        else if (XMath.IsNaN(@base) || XMath.IsNaN(exp))
        {
            // Rule #1
            return float.NaN;
        }
        else if (@base == float.NegativeInfinity)
        {
            if (exp < 0.0f)
            {
                // Rule #3
                return 0.0f;
            }
            else if (IsOddInteger(exp))
            {
                // Rule #4
                return float.NegativeInfinity;
            }
            else
            {
                // Rule #5
                return float.PositiveInfinity;
            }
        }
        else if (@base < 0.0f && !IsInteger(exp) && !XMath.IsInfinity(exp))
        {
            // Rule #6
            return float.NaN;
        }
        else if (@base == -1.0f && XMath.IsInfinity(exp))
        {
            // Rule #7
            return 1.0f;
        }
        else if (-1.0f < @base && @base < 1.0f && exp == float.NegativeInfinity)
        {
            // Rule #8
            return float.PositiveInfinity;
        }
        else if (-1.0f < @base && @base < 1.0f && exp == float.PositiveInfinity)
        {
            // Rule #9
            return 0.0f;
        }
        else if ((@base < -1.0f || @base > 1.0f) && exp == float.NegativeInfinity)
        {
            // Rule #10
            return 0.0f;
        }
        else if ((@base < -1.0f || @base > 1.0f) && exp == float.PositiveInfinity)
        {
            // Rule #11
            return float.PositiveInfinity;
        }
        else if (@base == 0.0f)
        {
            if (exp < 0.0f)
            {
                // Rule #12
                return float.PositiveInfinity;
            }
            else
            {
                // Rule #13
                // NB: exp == 0.0 already handled by Rule #2
                return 0.0f;
            }
        }
        else if (@base == float.PositiveInfinity)
        {
            if (exp < 0.0f)
            {
                // Rule #15
                return 0.0f;
            }
            else
            {
                // Rule #16
                // NB: exp == 0.0 already handled by Rule #2
                return float.PositiveInfinity;
            }
        }

        if (@base < 0.0)
        {
            // 'exp' is an integer, due to Rule #6.
            var sign = IsOddInteger(exp) ? -1.0f : 1.0f;
            return sign * Exp(exp * Log(XMath.Abs(@base)));
        }

        return Exp(exp * Log(@base));
    }

    /// <summary>
    /// Tests if a floating point value is an integer
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>True, if the value is an integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInteger(double value)
    {
        var remainder = XMath.Abs(value) % 2.0;
        return remainder == 0.0 || remainder == 1.0;
    }

    /// <summary>
    /// Tests if a floating point value is an integer
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>True, if the value is an integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInteger(float value)
    {
        var remainder = XMath.Abs(value) % 2.0f;
        return remainder == 0.0f || remainder == 1.0f;
    }

    /// <summary>
    /// Tests if a floating point value is an odd integer
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>True, if the value is an odd integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOddInteger(double value)
    {
        var remainder = XMath.Abs(value) % 2.0;
        return remainder == 1.0;
    }

    /// <summary>
    /// Tests if a floating point value is an odd integer
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <returns>True, if the value is an odd integer</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOddInteger(float value)
    {
        var remainder = XMath.Abs(value) % 2.0f;
        return remainder == 1.0f;
    }

    /// <summary cref="XMath.Exp(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Exp(double value) =>
        Cordic.Exp(value);

    /// <summary cref="XMath.Exp(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Exp(float value) =>
        XMath.Exp2(value * XMath.OneOverLn2);

    /// <summary cref="XMath.Exp2(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Exp2(double value) =>
        Exp(value * XMath.OneOverLog2ED);

    #endregion

    #region Log

    /// <summary cref="XMath.Log(double, double)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double LogBinary(double value, double newBase) =>
        GenericMath.Log(value, newBase);

    /// <summary cref="XMath.Log(float, float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LogBinary(float value, float newBase) =>
        GenericMath.Log(value, newBase);

    /// <summary cref="XMath.Log(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Log(double value) =>
        Cordic.Log(value);

    /// <summary cref="XMath.Log(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Log(float value) =>
        XMath.Log2(value) * XMath.OneOverLog2E;

    /// <summary cref="XMath.Log10(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Log10(double value) =>
        Log(value) * XMath.OneOverLn10D;

    /// <summary cref="XMath.Log10(float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Log10(float value) =>
        XMath.Log(value) * XMath.OneOverLn10;

    /// <summary cref="XMath.Log2(double)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Log2(double value) =>
        Log(value) * XMath.OneOverLn2D;

    #endregion
}
