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
        if (y == 0.0 ||
            XMath.IsInfinity(x) ||
            XMath.IsNaN(x) ||
            XMath.IsNaN(y))
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
        if (y == 0.0f ||
            XMath.IsInfinity(x) ||
            XMath.IsNaN(x) ||
            XMath.IsNaN(y))
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
        if (y == 0.0 ||
            XMath.IsInfinity(x) ||
            XMath.IsNaN(x) ||
            XMath.IsNaN(y))
            return double.NaN;

        if (XMath.IsInfinity(y))
            return x;

        return x - (y * XMath.RoundToEven(x * XMath.Rcp(y)));
    }

    /// <summary cref="XMath.IEEERemainder(float, float)"/>
    public static float IEEERemainder(float x, float y)
    {
        if (y == 0.0f ||
            XMath.IsInfinity(x) ||
            XMath.IsNaN(x) ||
            XMath.IsNaN(y))
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
}
