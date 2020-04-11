// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IntrinsicMath.CPUOnly.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using System;

namespace ILGPU
{
    partial class IntrinsicMath
    {
        /// <summary>
        /// Contains CPU-only math functions that are automatically mapped to IR nodes.
        /// </summary>
        public static class CPUOnly
        {
            #region General

            /// <summary>
            /// Returns true if the given value is NaN.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>True, if the given value is NaN.</returns>
            [MathIntrinsic(MathIntrinsicKind.IsNaNF)]
            public static bool IsNaN(double value) =>
                double.IsNaN(value);

            /// <summary>
            /// Returns true if the given value is NaN.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>True, if the given value is NaN.</returns>
            [MathIntrinsic(MathIntrinsicKind.IsNaNF)]
            public static bool IsNaN(float value) =>
                float.IsNaN(value);

            /// <summary>
            /// Returns true if the given value is infinity.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>True, if the given value is infinity.</returns>
            [MathIntrinsic(MathIntrinsicKind.IsInfF)]
            public static bool IsInfinity(double value) =>
                double.IsInfinity(value);

            /// <summary>
            /// Returns true if the given value is infinity.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>True, if the given value is infinity.</returns>
            [MathIntrinsic(MathIntrinsicKind.IsInfF)]
            public static bool IsInfinity(float value) =>
                float.IsInfinity(value);

            /// <summary>
            /// Computes 1.0 / value.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>1.0 / value.</returns>
            [MathIntrinsic(MathIntrinsicKind.RcpF)]
            public static double Rcp(double value) =>
                1.0 / value;

            /// <summary>
            /// Computes 1.0f / value.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>1.0f / value.</returns>
            [MathIntrinsic(MathIntrinsicKind.RcpF)]
            public static float Rcp(float value) =>
                1.0f / value;

            /// <summary>
            /// Computes 1/sqrt(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>1/sqrt(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.RsqrtF)]
            public static double Rsqrt(double value) =>
                Rcp(Sqrt(value));

            /// <summary>
            /// Computes 1/sqrt(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>1/sqrt(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.RsqrtF)]
            public static float Rsqrt(float value) =>
                Rcp(Sqrt(value));

            #endregion

            #region Double Precision

            /// <summary>
            /// Computes x%y.
            /// </summary>
            /// <param name="x">The nominator.</param>
            /// <param name="y">The denominator.</param>
            /// <returns>x%y.</returns>
            [MathIntrinsic(MathIntrinsicKind.Rem)]
            public static double Rem(double x, double y) =>
                Math.IEEERemainder(x, y);

            /// <summary>
            /// Computes sqrt(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>sqrt(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.SqrtF)]
            public static double Sqrt(double value) =>
                Math.Sqrt(value);

            /// <summary>
            /// Computes sin(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>sin(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.SinF)]
            public static double Sin(double value) =>
                Math.Sin(value);

            /// <summary>
            /// Computes sinh(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>sinh(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.SinHF)]
            public static double Sinh(double value) =>
                Math.Sinh(value);

            /// <summary>
            /// Computes asin(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>asin(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.AsinF)]
            public static double Asin(double value) =>
                Math.Asin(value);

            /// <summary>
            /// Computes cos(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>cos(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.CosF)]
            public static double Cos(double value) =>
                Math.Cos(value);

            /// <summary>
            /// Computes cosh(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>cosh(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.CosHF)]
            public static double Cosh(double value) =>
                Math.Cosh(value);

            /// <summary>
            /// Computes acos(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>acos(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.AcosF)]
            public static double Acos(double value) =>
                Math.Acos(value);

            /// <summary>
            /// Computes tan(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>tan(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.TanF)]
            public static double Tan(double value) =>
                Math.Tan(value);

            /// <summary>
            /// Computes tanh(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>tanh(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.TanHF)]
            public static double Tanh(double value) =>
                Math.Tanh(value);

            /// <summary>
            /// Computes atan(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>atan(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.AtanF)]
            public static double Atan(double value) =>
                Math.Atan(value);

            /// <summary>
            /// Computes atan2(y, x).
            /// </summary>
            /// <param name="y">The y value in radians.</param>
            /// <param name="x">The x value in radians.</param>
            /// <returns>atan2(y, x).</returns>
            [MathIntrinsic(MathIntrinsicKind.Atan2F)]
            public static double Atan2(double y, double x) =>
                Math.Atan2(y, x);

            /// <summary>
            /// Computes basis^exp.
            /// </summary>
            /// <param name="base">The basis.</param>
            /// <param name="exp">The exponent.</param>
            /// <returns>pow(basis, exp).</returns>
            [MathIntrinsic(MathIntrinsicKind.PowF)]
            public static double Pow(double @base, double exp) =>
                Math.Pow(@base, exp);

            /// <summary>
            /// Computes exp(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>exp(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.ExpF)]
            public static double Exp(double value) =>
                Math.Exp(value);

            /// <summary>
            /// Computes 2^value.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>2^value.</returns>
            [MathIntrinsic(MathIntrinsicKind.Exp2F)]
            public static double Exp2(double value) =>
                Math.Pow(2.0, value);

            /// <summary>
            /// Computes floor(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>floor(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.FloorF)]
            public static double Floor(double value) =>
                Math.Floor(value);

            /// <summary>
            /// Computes ceiling(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>ceiling(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.CeilingF)]
            public static double Ceiling(double value) =>
                Math.Ceiling(value);

            /// <summary>
            /// Computes log_newBase(value) to base newBase.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <param name="newBase">The desired base.</param>
            /// <returns>log_newBase(value).</returns>
            public static double Log(double value, double newBase) =>
                Math.Log(value, newBase);

            /// <summary>
            /// Computes log(value) to base e.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>log(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.LogF)]
            public static double Log(double value) =>
                Math.Log(value);

            /// <summary>
            /// Computes log10(value) to base 10.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>log10(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.Log10F)]
            public static double Log10(double value) =>
                Math.Log10(value);

            /// <summary>
            /// Computes log2(value) to base 2.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>log2(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.Log2F)]
            public static double Log2(double value) =>
                Log(value, 2.0);

            #endregion

            #region Single Precision

#if NETCORE
            /// <summary>
            /// Computes x%y.
            /// </summary>
            /// <param name="x">The nominator.</param>
            /// <param name="y">The denominator.</param>
            /// <returns>x%y.</returns>
            [MathIntrinsic(MathIntrinsicKind.Rem)]
            public static float Rem(float x, float y) =>
                MathF.IEEERemainder(x, y);

            /// <summary>
            /// Computes sqrt(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>sqrt(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.SqrtF)]
            public static float Sqrt(float value) =>
                MathF.Sqrt(value);

            /// <summary>
            /// Computes sin(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>sin(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.SinF)]
            public static float Sin(float value) =>
                MathF.Sin(value);

            /// <summary>
            /// Computes sinh(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>sinh(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.SinHF)]
            public static float Sinh(float value) =>
                MathF.Sinh(value);

            /// <summary>
            /// Computes asin(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>asin(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.AsinF)]
            public static float Asin(float value) =>
                MathF.Asin(value);

            /// <summary>
            /// Computes cos(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>cos(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.CosF)]
            public static float Cos(float value) =>
                MathF.Cos(value);

            /// <summary>
            /// Computes cosh(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>cosh(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.CosHF)]
            public static float Cosh(float value) =>
                MathF.Cosh(value);

            /// <summary>
            /// Computes acos(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>acos(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.AcosF)]
            public static float Acos(float value) =>
                MathF.Acos(value);

            /// <summary>
            /// Computes tan(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>tan(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.TanF)]
            public static float Tan(float value) =>
                MathF.Tan(value);

            /// <summary>
            /// Computes tanh(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>tanh(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.TanHF)]
            public static float Tanh(float value) =>
                MathF.Tanh(value);

            /// <summary>
            /// Computes atan(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>atan(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.AtanF)]
            public static float Atan(float value) =>
                MathF.Atan(value);

            /// <summary>
            /// Computes atan2(y, x).
            /// </summary>
            /// <param name="y">The y value in radians.</param>
            /// <param name="x">The x value in radians.</param>
            /// <returns>atan2(y, x).</returns>
            [MathIntrinsic(MathIntrinsicKind.Atan2F)]
            public static float Atan2(float y, float x) =>
                MathF.Atan2(y, x);

            /// <summary>
            /// Computes basis^exp.
            /// </summary>
            /// <param name="base">The basis.</param>
            /// <param name="exp">The exponent.</param>
            /// <returns>pow(basis, exp).</returns>
            [MathIntrinsic(MathIntrinsicKind.PowF)]
            public static float Pow(float @base, float exp) =>
                MathF.Pow(@base, exp);

            /// <summary>
            /// Computes exp(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>exp(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.ExpF)]
            public static float Exp(float value) =>
                MathF.Exp(value);

            /// <summary>
            /// Computes 2^value.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>2^value.</returns>
            [MathIntrinsic(MathIntrinsicKind.Exp2F)]
            public static float Exp2(float value) =>
                MathF.Pow(2.0f, value);

            /// <summary>
            /// Computes floor(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>floor(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.FloorF)]
            public static float Floor(float value) =>
                MathF.Floor(value);

            /// <summary>
            /// Computes ceiling(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>ceiling(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.CeilingF)]
            public static float Ceiling(float value) =>
                MathF.Ceiling(value);

            /// <summary>
            /// Computes log_newBase(value) to base newBase.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <param name="newBase">The desired base.</param>
            /// <returns>log_newBase(value).</returns>
            public static float Log(float value, float newBase) =>
                MathF.Log(value, newBase);

            /// <summary>
            /// Computes log(value) to base e.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>log(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.LogF)]
            public static float Log(float value) =>
                MathF.Log(value);

            /// <summary>
            /// Computes log10(value) to base 10.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>log10(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.Log10F)]
            public static float Log10(float value) =>
                MathF.Log10(value);

            /// <summary>
            /// Computes log2(value) to base 2.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>log2(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.Log2F)]
            public static float Log2(float value) =>
                Log(value, 2.0f);
#else
            /// <summary>
            /// Computes x%y.
            /// </summary>
            /// <param name="x">The nominator.</param>
            /// <param name="y">The denominator.</param>
            /// <returns>x%y.</returns>
            [MathIntrinsic(MathIntrinsicKind.Rem)]
            public static float Rem(float x, float y) =>
                (float)Math.IEEERemainder(x, y);

            /// <summary>
            /// Computes sqrt(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>sqrt(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.SqrtF)]
            public static float Sqrt(float value) =>
                (float)Math.Sqrt(value);

            /// <summary>
            /// Computes sin(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>sin(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.SinF)]
            public static float Sin(float value) =>
                (float)Math.Sin(value);

            /// <summary>
            /// Computes sinh(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>sinh(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.SinHF)]
            public static float Sinh(float value) =>
                (float)Math.Sinh(value);

            /// <summary>
            /// Computes asin(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>asin(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.AsinF)]
            public static float Asin(float value) =>
                (float)Math.Asin(value);

            /// <summary>
            /// Computes cos(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>cos(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.CosF)]
            public static float Cos(float value) =>
                (float)Math.Cos(value);

            /// <summary>
            /// Computes cosh(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>cosh(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.CosHF)]
            public static float Cosh(float value) =>
                (float)Math.Cosh(value);

            /// <summary>
            /// Computes acos(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>acos(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.AcosF)]
            public static float Acos(float value) =>
                (float)Math.Acos(value);

            /// <summary>
            /// Computes tan(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>tan(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.TanF)]
            public static float Tan(float value) =>
                (float)Math.Tan(value);

            /// <summary>
            /// Computes tanh(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>tanh(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.TanHF)]
            public static float Tanh(float value) =>
                (float)Math.Tanh(value);

            /// <summary>
            /// Computes atan(value).
            /// </summary>
            /// <param name="value">The value in radians.</param>
            /// <returns>atan(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.AtanF)]
            public static float Atan(float value) =>
                (float)Math.Atan(value);

            /// <summary>
            /// Computes atan2(y, x).
            /// </summary>
            /// <param name="y">The y value in radians.</param>
            /// <param name="x">The x value in radians.</param>
            /// <returns>atan2(y, x).</returns>
            [MathIntrinsic(MathIntrinsicKind.Atan2F)]
            public static float Atan2(float y, float x) =>
                (float)Math.Atan2(y, x);

            /// <summary>
            /// Computes basis^exp.
            /// </summary>
            /// <param name="base">The basis.</param>
            /// <param name="exp">The exponent.</param>
            /// <returns>pow(basis, exp).</returns>
            [MathIntrinsic(MathIntrinsicKind.PowF)]
            public static float Pow(float @base, float exp) =>
                (float)Math.Pow(@base, exp);

            /// <summary>
            /// Computes exp(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>exp(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.ExpF)]
            public static float Exp(float value) =>
                (float)Math.Exp(value);

            /// <summary>
            /// Computes 2^value.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>2^value.</returns>
            [MathIntrinsic(MathIntrinsicKind.Exp2F)]
            public static float Exp2(float value) =>
                (float)Math.Pow(2.0, value);

            /// <summary>
            /// Computes floor(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>floor(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.FloorF)]
            public static float Floor(float value) =>
                (float)Math.Floor(value);

            /// <summary>
            /// Computes ceiling(value).
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>ceiling(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.CeilingF)]
            public static float Ceiling(float value) =>
                (float)Math.Ceiling(value);

            /// <summary>
            /// Computes log_newBase(value) to base newBase.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <param name="newBase">The desired base.</param>
            /// <returns>log_newBase(value).</returns>
            public static float Log(float value, float newBase) =>
                (float)Math.Log(value, newBase);

            /// <summary>
            /// Computes log(value) to base e.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>log(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.LogF)]
            public static float Log(float value) =>
                (float)Math.Log(value);

            /// <summary>
            /// Computes log10(value) to base 10.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>log10(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.Log10F)]
            public static float Log10(float value) =>
                (float)Math.Log10(value);

            /// <summary>
            /// Computes log2(value) to base 2.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>log2(value).</returns>
            [MathIntrinsic(MathIntrinsicKind.Log2F)]
            public static float Log2(float value) =>
                (float)Log(value, 2.0);
#endif

            #endregion
        }
    }
}
