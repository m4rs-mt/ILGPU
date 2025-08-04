// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericMath.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Util;
using System.Runtime.CompilerServices;

// disable: max_line_length

namespace ILGPUC.Intrinsic;

/// <summary>
/// Contains software implementation for Log with two parameters.
/// </summary>
static class GenericMath
{
    /// <summary cref="XMath.Rem(double, double)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Rem(double x, double y)
    {
        if (y == 0.0 | XMath.IsInfinity(x) | XMath.IsNaN(x) | XMath.IsNaN(y))
            return double.NaN;

        if (XMath.IsInfinity(y))
            return x;

        var xDivY = XMath.Abs(x * XMath.Rcp(y));
        var result = (xDivY - XMath.Floor(xDivY)) * XMath.Abs(y);
        return Utilities.Select(x < 0.0, -result, result);
    }

    /// <summary cref="XMath.Rem(float, float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Rem(float x, float y)
    {
        if (y == 0.0f | XMath.IsInfinity(x) | XMath.IsNaN(x) | XMath.IsNaN(y))
            return float.NaN;

        if (XMath.IsInfinity(y))
            return x;

        var xDivY = XMath.Abs(x * XMath.Rcp(y));
        var result = (xDivY - XMath.Floor(xDivY)) * XMath.Abs(y);
        return Utilities.Select(x < 0.0f, -result, result);
    }

    /// <summary cref="XMath.IEEERemainder(double, double)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double IEEERemainder(double x, double y)
    {
        if (y == 0.0 | XMath.IsInfinity(x) | XMath.IsNaN(x) | XMath.IsNaN(y))
            return double.NaN;

        if (XMath.IsInfinity(y))
            return x;

        return x - (y * XMath.RoundToEven(x * XMath.Rcp(y)));
    }

    /// <summary cref="XMath.IEEERemainder(float, float)"/>
    public static float IEEERemainder(float x, float y)
    {
        if (y == 0.0f | XMath.IsInfinity(x) | XMath.IsNaN(x) | XMath.IsNaN(y))
            return float.NaN;

        if (XMath.IsInfinity(y))
            return x;

        return x - (y * XMath.RoundToEven(x * XMath.Rcp(y)));
    }

    /// <summary>
    /// Implements Log with two parameters.
    /// </summary>
    public static double Log(double value, double newBase)
    {
        if (value < 0.0 |
            newBase < 0.0 |
            (value != 1.0 & newBase == 0.0) ||
            (value != 1.0 & newBase == double.PositiveInfinity) |
            XMath.IsNaN(value) | XMath.IsNaN(newBase) |
            newBase > 1.0 - 1e-9)
        {
            return double.NaN;
        }

        if (value == 0.0)
        {
            if (0.0 < newBase & newBase < 1.0)
                return double.PositiveInfinity;
            else if (newBase > 1.0)
                return double.NegativeInfinity;
        }

        if (value == double.PositiveInfinity)
        {
            if (0.0 < newBase & newBase < 1.0)
                return double.NegativeInfinity;
            else if (newBase > 1.0)
                return double.PositiveInfinity;
        }

        if (value == 1.0 & (newBase == 0.0 | newBase == double.PositiveInfinity))
            return 0.0;

        return XMath.Log(value) * XMath.Rcp(XMath.Log(newBase));
    }

    /// <summary>
    /// Implements Log with two parameters.
    /// </summary>
    public static float Log(float value, float newBase)
    {
        if (value < 0.0f | newBase < 0.0f |
            (value != 1.0f & newBase == 0.0f) |
            (value != 1.0f & newBase == float.PositiveInfinity) |
            XMath.IsNaN(value) || XMath.IsNaN(newBase) ||
            newBase > 1.0f - 1e-6f)
        {
            return float.NaN;
        }

        if (value == 0.0f)
        {
            if (0.0f < newBase & newBase < 1.0f)
                return float.PositiveInfinity;
            else if (newBase > 1.0f)
                return float.NegativeInfinity;
        }

        if (value == float.PositiveInfinity)
        {
            if (0.0f < newBase & newBase < 1.0f)
                return float.NegativeInfinity;
            else if (newBase > 1.0f)
                return float.PositiveInfinity;
        }

        if (value == 1.0f & (newBase == 0.0f | newBase == float.PositiveInfinity))
            return 0.0f;

        return XMath.Log(value) * XMath.Rcp(XMath.Log(newBase));
    }

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
            else if (XMath.IsOddInteger(exp))
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
        else if (@base < 0.0 && !XMath.IsInteger(exp) && !XMath.IsInfinity(exp))
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
            var sign = XMath.IsOddInteger(exp) ? -1.0 : 1.0;
            return sign * XMath.Exp(exp * XMath.Log(XMath.Abs(@base)));
        }

        return XMath.Exp(exp * XMath.Log(@base));
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
            else if (XMath.IsOddInteger(exp))
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
        else if (@base < 0.0f && !XMath.IsInteger(exp) && !XMath.IsInfinity(exp))
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
            var sign = XMath.IsOddInteger(exp) ? -1.0f : 1.0f;
            return sign * XMath.Exp(exp * XMath.Log(XMath.Abs(@base)));
        }

        return XMath.Exp(exp * XMath.Log(@base));
    }
}
