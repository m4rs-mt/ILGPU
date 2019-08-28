// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: XMath.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Represents basic math helpers for general double/float
    /// math operations that are supported on the CPU and the GPU.
    /// </summary>
    /// <remarks>
    /// NOTE: This class will be replaced by a specific fast-math library
    /// for cross platform (CPU/GPU) math functions in a future release.
    /// CAUTION: Therefore, these functions are not optimized for performance or
    /// precision in any way.
    /// </remarks>
    public static class XMath
    {
        #region Constants

        /// <summary>
        /// The E constant.
        /// </summary>
        public const float E = 2.71828182f;

        /// <summary>
        /// The E constant.
        /// </summary>
        public const double ED = Math.E;

        /// <summary>
        /// The log2(E) constant.
        /// </summary>
        public const float Log2E = 1.44269504f;

        /// <summary>
        /// The log2(E) constant.
        /// </summary>
        public const double Log2ED = 1.4426950408889634;

        /// <summary>
        /// The 1/log2(2) constant.
        /// </summary>
        public const float OneOverLog2E = 1.0f / Log2E;

        /// <summary>
        /// The 1/log2(2) constant.
        /// </summary>
        public const double OneOverLog2ED = 1.0 / Log2ED;

        /// <summary>
        /// The log10(E) constant.
        /// </summary>
        public const float Log10E = 0.43429448f;

        /// <summary>
        /// The log10(E) constant.
        /// </summary>
        public const double Log10ED = 0.4342944819032518;

        /// <summary>
        /// The ln(2) constant.
        /// </summary>
        public const float Ln2 = 0.69314718f;

        /// <summary>
        /// The ln(2) constant.
        /// </summary>
        public const double Ln2D = 0.6931471805599453;

        /// <summary>
        /// The 1/ln(2) constant.
        /// </summary>
        public const float OneOverLn2 = 1.0f / Ln2;

        /// <summary>
        /// The 1/ln(2) constant.
        /// </summary>
        public const double OneOverLn2D = 1.0 / Ln2D;

        /// <summary>
        /// The ln(10) constant.
        /// </summary>
        public const float Ln10 = 2.30258509f;

        /// <summary>
        /// The ln(10) constant.
        /// </summary>
        public const double Ln10D = 2.3025850929940457;

        /// <summary>
        /// The 1/ln(10) constant.
        /// </summary>
        public const float OneOverLn10 = 1.0f / Ln10;

        /// <summary>
        /// The 1/ln(10) constant.
        /// </summary>
        public const double OneOverLn10D = 1.0 / Ln10D;

        /// <summary>
        /// The PI constant.
        /// </summary>
        public const float PI = 3.14159265f;

        /// <summary>
        /// The PI constant.
        /// </summary>
        public const double PID = Math.PI;

        /// <summary>
        /// The PI/2 constant.
        /// </summary>
        public const float PIHalf = PI / 2.0f;

        /// <summary>
        /// The PI/4 constant.
        /// </summary>
        public const float PIFourth = PI / 4.0f;

        /// <summary>
        /// The 1/PI constant.
        /// </summary>
        public const float OneOverPI = 1.0f / PI;

        /// <summary>
        /// The 2/PI constant.
        /// </summary>
        public const float TwoOverPI = 2.0f / PI;

        /// <summary>
        /// The sqrt(2) constant.
        /// </summary>
        public const float Sqrt2 = 1.41421356f;

        /// <summary>
        /// The 1/sqrt(2) constant.
        /// </summary>
        public const float OneOverSqrt2 = 1.0f / Sqrt2;

        /// <summary>
        /// The 1.0f / 3.0f constant.
        /// </summary>
        public const float OneThird = 1.0f / 3.0f;

        #endregion

        #region NaN & Infinity

        /// <summary>
        /// Returns true iff the given value is NaN.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is NaN.</returns>
        [MathIntrinsic(MathIntrinsicKind.IsNaNF)]
        public static bool IsNaN(double value)
        {
            return double.IsNaN(value);
        }

        /// <summary>
        /// Returns true iff the given value is NaN.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is NaN.</returns>
        [MathIntrinsic(MathIntrinsicKind.IsNaNF)]
        public static bool IsNaN(float value)
        {
            return float.IsNaN(value);
        }

        /// <summary>
        /// Returns true iff the given value is infinity.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is infinity.</returns>
        [MathIntrinsic(MathIntrinsicKind.IsInfF)]
        public static bool IsInfinity(double value)
        {
            return double.IsInfinity(value);
        }

        /// <summary>
        /// Returns true iff the given value is infinity.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is infinity.</returns>
        [MathIntrinsic(MathIntrinsicKind.IsInfF)]
        public static bool IsInfinity(float value)
        {
            return float.IsInfinity(value);
        }

        #endregion

        #region Rem

        /// <summary>
        /// Computes x%y.
        /// </summary>
        /// <param name="x">The nominator.</param>
        /// <param name="y">The denominator.</param>
        /// <returns>x%y.</returns>
        [MathIntrinsic(MathIntrinsicKind.Rem)]
        public static double Rem(double x, double y)
        {
            var xDivY = Abs(x * Rcp(y));
            var result = (xDivY - Floor(xDivY)) * Abs(y);
            return x < 0.0 ? -result : result;
        }

        /// <summary>
        /// Computes x%y.
        /// </summary>
        /// <param name="x">The nominator.</param>
        /// <param name="y">The denominator.</param>
        /// <returns>x%y.</returns>
        [MathIntrinsic(MathIntrinsicKind.Rem)]
        public static float Rem(float x, float y)
        {
            var xDivY = Abs(x * Rcp(y));
            var result = (xDivY - Floor(xDivY)) * Abs(y);
            return x < 0.0f ? -result : result;
        }

        #endregion

        #region Rcp

        /// <summary>
        /// Computes 1.0 / value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>1.0 / value.</returns>
        [MathIntrinsic(MathIntrinsicKind.RcpF)]
        public static double Rcp(double value)
        {
            return 1.0 / value;
        }

        /// <summary>
        /// Computes 1.0f / value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>1.0f / value.</returns>
        [MathIntrinsic(MathIntrinsicKind.RcpF)]
        public static float Rcp(float value)
        {
            return 1.0f / value;
        }

        #endregion

        #region Sqrt

        /// <summary>
        /// Computes sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>sqrt(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.SqrtF)]
        public static double Sqrt(double value)
        {
            return Math.Sqrt(value);
        }

        /// <summary>
        /// Computes sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>sqrt(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.SqrtF)]
        public static float Sqrt(float value)
        {
            return (float)Math.Sqrt(value);
        }

        /// <summary>
        /// Computes 1/sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>1/sqrt(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.RsqrtF)]
        public static double Rsqrt(double value)
        {
            return Rcp(Sqrt(value));
        }

        /// <summary>
        /// Computes 1/sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>1/sqrt(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.RsqrtF)]
        public static float Rsqrt(float value)
        {
            return Rcp(Sqrt(value));
        }

        #endregion

        #region Sin / Cos / Tan

        /// <summary>
        /// Computes sin(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>sin(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.SinF)]
        public static double Sin(double value)
        {
            return Math.Sin(value);
        }

        /// <summary>
        /// Computes sin(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>sin(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.SinF)]
        public static float Sin(float value)
        {
            return (float)Math.Sin(value);
        }

        /// <summary>
        /// Computes sinh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>sinh(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.SinHF)]
        public static double Sinh(double value)
        {
            return 0.5 * (Exp(value) - Exp(-value));
        }

        /// <summary>
        /// Computes sinh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>sinh(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.SinHF)]
        public static float Sinh(float value)
        {
            return 0.5f * (Exp(value) - Exp(-value));
        }

        /// <summary>
        /// Computes cos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>cos(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.CosF)]
        public static double Cos(double value)
        {
            return Math.Cos(value);
        }

        /// <summary>
        /// Computes cos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>cos(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.CosF)]
        public static float Cos(float value)
        {
            return (float)Math.Cos(value);
        }

        /// <summary>
        /// Computes cosh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>cosh(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.CosHF)]
        public static double Cosh(double value)
        {
            return 0.5 * (Exp(value) + Exp(-value));
        }

        /// <summary>
        /// Computes cosh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>cosh(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.CosHF)]
        public static float Cosh(float value)
        {
            return 0.5f * (Exp(value) + Exp(-value));
        }

        /// <summary>
        /// Computes tan(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>tan(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.TanF)]
        public static double Tan(double value)
        {
            var sin = Sin(value);
            var cos = Cos(value);
            return sin * Rcp(cos);
        }

        /// <summary>
        /// Computes tan(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>tan(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.TanF)]
        public static float Tan(float value)
        {
            var sin = Sin(value);
            var cos = Cos(value);
            return sin * Rcp(cos);
        }

        /// <summary>
        /// Computes tanh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>tanh(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.TanHF)]
        public static double Tanh(double value)
        {
            var exp = Exp(2.0 * value);
            var denominator = Rcp(exp + 1.0);
            return (exp - 1.0) * denominator;
        }

        /// <summary>
        /// Computes tanh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>tanh(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.TanHF)]
        public static float Tanh(float value)
        {
            var exp = Exp(2.0f * value);
            var denominator = Rcp(exp + 1.0f);
            return (exp - 1.0f) * denominator;
        }

        /// <summary>
        /// Computes sin(value) and cos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <param name="sin">The result of sin(value).</param>
        /// <param name="cos">The result of cos(value).</param>
        /// <returns>tanh(value).</returns>
        public static void SinCos(double value, out double sin, out double cos)
        {
            sin = Sin(value);
            cos = Cos(value);
        }

        /// <summary>
        /// Computes sin(value) and cos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <param name="sin">The result of sin(value).</param>
        /// <param name="cos">The result of cos(value).</param>
        /// <returns>tanh(value).</returns>
        public static void SinCos(float value, out float sin, out float cos)
        {
            sin = Sin(value);
            cos = Cos(value);
        }

        #endregion

        #region Pow & Exp

        /// <summary>
        /// Computes basis^exp.
        /// </summary>
        /// <param name="base">The basis.</param>
        /// <param name="exp">The exponent.</param>
        /// <returns>pow(basis, exp).</returns>
        [MathIntrinsic(MathIntrinsicKind.PowF)]
        public static double Pow(double @base, double exp)
        {
            return Exp(exp * Log(@base));
        }

        /// <summary>
        /// Computes basis^exp.
        /// </summary>
        /// <param name="base">The basis.</param>
        /// <param name="exp">The exponent.</param>
        /// <returns>pow(basis, exp).</returns>
        [MathIntrinsic(MathIntrinsicKind.PowF)]
        public static float Pow(float @base, float exp)
        {
            return Exp(exp * Log(@base));
        }

        /// <summary>
        /// Computes exp(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>exp(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.ExpF)]
        public static double Exp(double value)
        {
            return Exp2(value * OneOverLn2D);
        }

        /// <summary>
        /// Computes exp(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>exp(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.ExpF)]
        public static float Exp(float value)
        {
            return Exp2(value * OneOverLn2);
        }

        /// <summary>
        /// Computes 2^value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>2^value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Exp2F)]
        public static double Exp2(double value)
        {
            return Math.Pow(2.0, value);
        }

        /// <summary>
        /// Computes 2^value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>2^value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Exp2F)]
        public static float Exp2(float value)
        {
            return (float)Math.Pow(2.0, value);
        }

        #endregion

        #region Floor & Ceiling

        /// <summary>
        /// Computes floor(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>floor(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.FloorF)]
        public static double Floor(double value)
        {
            var intValue = (int)value;
            return value < intValue ? intValue - 1.0 : intValue;
        }

        /// <summary>
        /// Computes floor(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>floor(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.FloorF)]
        public static float Floor(float value)
        {
            var intValue = (int)value;
            return value < intValue ? intValue - 1.0f : intValue;
        }

        /// <summary>
        /// Computes ceiling(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>ceiling(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.CeilingF)]
        public static double Ceiling(double value)
        {
            return -Floor(-value);
        }

        /// <summary>
        /// Computes ceiling(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>ceiling(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.CeilingF)]
        public static float Ceiling(float value)
        {
            return -Floor(-value);
        }

        #endregion

        #region Log

        /// <summary>
        /// Computes log_newBase(value) to base newBase.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="newBase">The desired base.</param>
        /// <returns>log_newBase(value).</returns>
        public static double Log(double value, double newBase)
        {
            return Log(value) / Log(newBase);
        }

        /// <summary>
        /// Computes log_newBase(value) to base newBase.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="newBase">The desired base.</param>
        /// <returns>log_newBase(value).</returns>
        public static float Log(float value, float newBase)
        {
            return Log(value) / Log(newBase);
        }

        /// <summary>
        /// Computes log(value) to base e.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>log(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.LogF)]
        public static double Log(double value)
        {
            return Log2(value) * OneOverLog2ED;
        }

        /// <summary>
        /// Computes log(value) to base e.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>log(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.LogF)]
        public static float Log(float value)
        {
            return Log2(value) * OneOverLog2E;
        }

        /// <summary>
        /// Computes log10(value) to base 10.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>log10(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.Log10F)]
        public static double Log10(double value)
        {
            return Log2(value) * OneOverLn10;
        }

        /// <summary>
        /// Computes log10(value) to base 10.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>log10(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.Log10F)]
        public static float Log10(float value)
        {
            return Log2(value) * OneOverLn10;
        }

        /// <summary>
        /// Computes log2(value) to base 2.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>log2(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.Log2F)]
        public static double Log2(double value)
        {
            return Math.Log(value) * OneOverLn2;
        }

        /// <summary>
        /// Computes log2(value) to base 2.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>log2(value).</returns>
        [MathIntrinsic(MathIntrinsicKind.Log2F)]
        public static float Log2(float value)
        {
            return (float)Math.Log(value) * OneOverLn2;
        }

        #endregion

        #region Abs

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MathIntrinsic(MathIntrinsicKind.Abs)]
        public static double Abs(double value)
        {
            return Math.Abs(value);
        }

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MathIntrinsic(MathIntrinsicKind.Abs)]
        public static float Abs(float value)
        {
            return Math.Abs(value);
        }

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [CLSCompliant(false)]
        public static sbyte Abs(sbyte value)
        {
            return (sbyte)Abs((int)value);
        }

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        public static short Abs(short value)
        {
            return (short)Abs((int)value);
        }

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MathIntrinsic(MathIntrinsicKind.Abs)]
        public static int Abs(int value)
        {
            return Math.Abs(value);
        }

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [MathIntrinsic(MathIntrinsicKind.Abs)]
        public static long Abs(long value)
        {
            return Math.Abs(value);
        }

        #endregion

        #region Min/Max

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Min)]
        public static double Min(double first, double second)
        {
            return Math.Min(first, second);
        }

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Min)]
        public static float Min(float first, float second)
        {
            return Math.Min(first, second);
        }

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [CLSCompliant(false)]
        public static sbyte Min(sbyte first, sbyte second)
        {
            return (sbyte)Min((int)first, second);
        }

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        public static short Min(short first, short second)
        {
            return (short)Min((int)first, second);
        }

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Min)]
        public static int Min(int first, int second)
        {
            return Math.Min(first, second);
        }

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Min)]
        public static long Min(long first, long second)
        {
            return Math.Min(first, second);
        }

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        public static byte Min(byte first, byte second)
        {
            return (byte)Min((uint)first, second);
        }

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [CLSCompliant(false)]
        public static ushort Min(ushort first, ushort second)
        {
            return (ushort)Min((uint)first, second);
        }

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [CLSCompliant(false)]
        [MathIntrinsic(MathIntrinsicKind.Min, ArithmeticFlags.Unsigned)]
        public static uint Min(uint first, uint second)
        {
            return Math.Min(first, second);
        }

        /// <summary>
        /// Computes min(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The minimum of first and second value.</returns>
        [CLSCompliant(false)]
        [MathIntrinsic(MathIntrinsicKind.Min, ArithmeticFlags.Unsigned)]
        public static ulong Min(ulong first, ulong second)
        {
            return Math.Min(first, second);
        }

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Max)]
        public static double Max(double first, double second)
        {
            return Math.Max(first, second);
        }

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Max)]
        public static float Max(float first, float second)
        {
            return Math.Max(first, second);
        }

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [CLSCompliant(false)]
        public static sbyte Max(sbyte first, sbyte second)
        {
            return (sbyte)Max((int)first, second);
        }

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        public static short Max(short first, short second)
        {
            return (short)Max((int)first, second);
        }

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Max)]
        public static int Max(int first, int second)
        {
            return Math.Max(first, second);
        }

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [MathIntrinsic(MathIntrinsicKind.Max)]
        public static long Max(long first, long second)
        {
            return Math.Max(first, second);
        }

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        public static byte Max(byte first, byte second)
        {
            return (byte)Max((uint)first, second);
        }

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [CLSCompliant(false)]
        public static ushort Max(ushort first, ushort second)
        {
            return (ushort)Max((uint)first, second);
        }

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [CLSCompliant(false)]
        [MathIntrinsic(MathIntrinsicKind.Max, ArithmeticFlags.Unsigned)]
        public static uint Max(uint first, uint second)
        {
            return Math.Max(first, second);
        }

        /// <summary>
        /// Computes max(first, second).
        /// </summary>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        /// <returns>The maximum of first and second value.</returns>
        [CLSCompliant(false)]
        [MathIntrinsic(MathIntrinsicKind.Max, ArithmeticFlags.Unsigned)]
        public static ulong Max(ulong first, ulong second)
        {
            return Math.Max(first, second);
        }

        #endregion

        #region Clamp

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static double Clamp(double value, double min, double max)
        {
            return Max(Min(value, max), min);
        }

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static float Clamp(float value, float min, float max)
        {
            return Max(Min(value, max), min);
        }

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [CLSCompliant(false)]
        public static sbyte Clamp(sbyte value, sbyte min, sbyte max)
        {
            return Max(Min(value, max), min);
        }

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static short Clamp(short value, short min, short max)
        {
            return Max(Min(value, max), min);
        }

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static int Clamp(int value, int min, int max)
        {
            return Max(Min(value, max), min);
        }

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static long Clamp(long value, long min, long max)
        {
            return Max(Min(value, max), min);
        }

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        public static byte Clamp(byte value, byte min, byte max)
        {
            return Max(Min(value, max), min);
        }

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [CLSCompliant(false)]
        public static ushort Clamp(ushort value, ushort min, ushort max)
        {
            return Max(Min(value, max), min);
        }

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [CLSCompliant(false)]
        public static uint Clamp(uint value, uint min, uint max)
        {
            return Max(Min(value, max), min);
        }

        /// <summary>
        /// Computes clamp(value, min, max) = Max(Min(clamp, max), min).
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The first argument.</param>
        /// <param name="max">The second argument.</param>
        /// <returns>The clamped value in the interval [min, max].</returns>
        [CLSCompliant(false)]
        public static ulong Clamp(ulong value, ulong min, ulong max)
        {
            return Max(Min(value, max), min);
        }


        #endregion

        #region Round & Truncate

        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded away from zero).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double RoundAwayFromZero(double value)
        {
            return value < 0.0 ? Floor(value) : Ceiling(value);
        }

        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded away from zero).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RoundAwayFromZero(float value)
        {
            return value < 0.0f ? Floor(value) : Ceiling(value);
        }

        /// <summary>
        /// Truncates the given value.
        /// </summary>
        /// <param name="value">The value to truncate.</param>
        /// <returns>The truncated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Truncate(double value)
        {
            return value < 0.0 ? Ceiling(value) : Floor(value);
        }

        /// <summary>
        /// Truncates the given value.
        /// </summary>
        /// <param name="value">The value to truncate.</param>
        /// <returns>The truncated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Truncate(float value)
        {
            return value < 0.0f ? Ceiling(value) : Floor(value);
        }

        #endregion

        #region Sign

        /// <summary>
        /// Computes the sign of the provided value.
        /// Sign will return 0 for NaN, Infitity or 0 values.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>-1 for negative value, 1 for positive values, and 0 for
        /// 0, NaN or Infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(double value)
        {
            if (value < 0)
                return -1;
            if (value > 0)
                return 1;
            return 0;
        }

        /// <summary>
        /// Computes the sign of the provided value.
        /// Sign will return 0 for NaN, Infitity or 0 values.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>-1 for negative value, 1 for positive values, and 0 for
        /// 0, NaN or Infinity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(float value)
        {
            if (value < 0f)
                return -1;
            if (value > 0f)
                return 1;
            return 0;
        }

        #endregion

        #region Deg & Rad

        /// <summary>
        /// Converts the given value in degrees to radians.
        /// </summary>
        /// <param name="degrees">The value in degrees to convert.</param>
        /// <returns>The converted value in radians.</returns>
        public static double DegToRad(double degrees)
        {
            const double _PIOver180 = Math.PI / 180.0;
            return degrees * _PIOver180;
        }

        /// <summary>
        /// Converts the given value in degrees to radians.
        /// </summary>
        /// <param name="degrees">The value in degrees to convert.</param>
        /// <returns>The converted value in radians.</returns>
        public static float DegToRad(float degrees)
        {
            const float _PIOver180 = PI / 180.0f;
            return degrees * _PIOver180;
        }

        /// <summary>
        /// Converts the given value in radians to degrees.
        /// </summary>
        /// <param name="radians">The value in radians to convert.</param>
        /// <returns>The converted value in degrees.</returns>
        public static double RadToDeg(double radians)
        {
            const double _180OverPi = 180.0 / Math.PI;
            return radians * _180OverPi;
        }

        /// <summary>
        /// Converts the given value in radians to degrees.
        /// </summary>
        /// <param name="radians">The value in radians to convert.</param>
        /// <returns>The converted value in degrees.</returns>
        public static float RadToDeg(float radians)
        {
            const float _180OverPi = 180.0f / PI;
            return radians * _180OverPi;
        }

        #endregion

        #region Int Divisions

        /// <summary>
        /// Realizes an integer division of <paramref name="numerator"/>
        /// divided by <paramref name="denominator"/> while rounding the result
        /// down to zero.
        /// </summary>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The denominator.</param>
        /// <returns>The numerator divided by the denominator rounded to zero.</returns>
        public static int DivRoundDown(int numerator, int denominator)
        {
            return numerator / denominator;
        }

        /// <summary>
        /// Realizes an integer division of <paramref name="numerator"/>
        /// divided by <paramref name="denominator"/> while rounding the result
        /// up (away from zero).
        /// </summary>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The denominator.</param>
        /// <returns>The numerator divided by the denominator rounded up (away from zero).</returns>
        public static int DivRoundUp(int numerator, int denominator)
        {
            return (numerator + denominator - 1) / denominator;
        }

        /// <summary>
        /// Realizes an integer division of <paramref name="numerator"/>
        /// divided by <paramref name="denominator"/> while rounding the result
        /// down to zero.
        /// </summary>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The denominator.</param>
        /// <returns>The numerator divided by the denominator rounded to zero.</returns>
        public static long DivRoundDown(long numerator, long denominator)
        {
            return numerator / denominator;
        }

        /// <summary>
        /// Realizes an integer division of <paramref name="numerator"/>
        /// divided by <paramref name="denominator"/> while rounding the result
        /// up (away from zero).
        /// </summary>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The denominator.</param>
        /// <returns>The numerator divided by the denominator rounded up (away from zero).</returns>
        public static long DivRoundUp(long numerator, long denominator)
        {
            return (numerator + denominator - 1L) / denominator;
        }

        #endregion
    }
}
