// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: GPUMath.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Compiler.Intrinsic;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Represents math helpers for general double/float
    /// math operations that are supported on the gpu.
    /// </summary>
    public static class GPUMath
    {
        #region Constants

        /// <summary>
        /// The E constant.
        /// </summary>
        public const float E = 2.71828182f;

        /// <summary>
        /// The log2(E) constant.
        /// </summary>
        public const float Log2E = 1.44269504f;

        /// <summary>
        /// The log10(E) constant.
        /// </summary>
        public const float Log10E = 0.43429448f;

        /// <summary>
        /// The ln(2) constant.
        /// </summary>
        public const float Ln2 = 0.69314718f;

        /// <summary>
        /// The ln(10) constant.
        /// </summary>
        public const float Ln10 = 2.30258509f;

        /// <summary>
        /// The PI constant.
        /// </summary>
        public const float PI = 3.14159265f;

        /// <summary>
        /// The PI/2 constant.
        /// </summary>
        public const float PIHalf = 1.57079633f;

        /// <summary>
        /// The PI/4 constant.
        /// </summary>
        public const float PIFourth = 0.78539816f;

        /// <summary>
        /// The 1/PI constant.
        /// </summary>
        public const float OneOverPI = 0.31830989f;

        /// <summary>
        /// The 2/PI constant.
        /// </summary>
        public const float TwoOverPI = 0.63661977f;

        /// <summary>
        /// The sqrt(2) constant.
        /// </summary>
        public const float Sqrt2 = 1.41421356f;

        /// <summary>
        /// The 1/sqrt(2) constant.
        /// </summary>
        public const float OneOverSqrt2 = 0.70710678f;

        /// <summary>
        /// The 1.0f / 3.0f constant.
        /// </summary>
        public const float OneThird = 1.0f / 3.0f;

        #endregion

        #region Function Metadata

        /// <summary>
        /// Contains all available math functions that can be resolved by
        /// their <see cref="MathIntrinsicKind"/> value.
        /// </summary>
        internal static IReadOnlyDictionary<MathIntrinsicKind, MethodInfo> MathFunctionMapping { get; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static GPUMath()
        {
            var mapping = new Dictionary<MathIntrinsicKind, MethodInfo>();
            foreach (var method in typeof(GPUMath).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var attribute = method.GetCustomAttribute<MathIntrinsicAttribute>();
                if (attribute == null)
                    continue;
                mapping.Add(attribute.IntrinsicKind, method);
            }
            MathFunctionMapping = mapping;
        }

        #endregion

        #region Methods

        #region NaN & Infinity

        /// <summary>
        /// Returns true iff the given value is NaN.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is NaN.</returns>
        [PTXMathFunction("__nv_isnand", true)]
        [MathIntrinsic(MathIntrinsicKind.IsNaNF64)]
        public static bool IsNaN(double value)
        {
            return double.IsNaN(value);
        }

        /// <summary>
        /// Returns true iff the given value is NaN.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is NaN.</returns>
        [PTXMathFunction("__nv_isnanf", true)]
        [MathIntrinsic(MathIntrinsicKind.IsNaNF32)]
        public static bool IsNaN(float value)
        {
            return float.IsNaN(value);
        }

        /// <summary>
        /// Returns true iff the given value is infinity.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is infinity.</returns>
        [PTXMathFunction("__nv_isinfd", true)]
        [MathIntrinsic(MathIntrinsicKind.IsInfF64)]
        public static bool IsInfinity(double value)
        {
            return double.IsInfinity(value);
        }

        /// <summary>
        /// Returns true iff the given value is infinity.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True, iff the given value is infinity.</returns>
        [PTXMathFunction("__nv_isinff", true)]
        [MathIntrinsic(MathIntrinsicKind.IsInfF32)]
        public static bool IsInfinity(float value)
        {
            return float.IsInfinity(value);
        }

        #endregion

        #region Mul

        /// <summary>
        /// Computes x*y.
        /// </summary>
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <returns>x*y.</returns>
        [PTXMathFunction("__nv_dmul_rn")]
        [MathIntrinsic(MathIntrinsicKind.MulF64)]
        public static double Mul(double x, double y)
        {
            return x * y;
        }

        /// <summary>
        /// Computes x*y.
        /// </summary>
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <returns>x*y.</returns>
        [PTXMathFunction("__nv_fmul_rn")]
        [MathIntrinsic(MathIntrinsicKind.MulF32)]
        public static float Mul(float x, float y)
        {
            return x * y;
        }

        #endregion

        #region Div

        /// <summary>
        /// Computes x/y.
        /// </summary>
        /// <param name="x">The nominator.</param>
        /// <param name="y">The denominator.</param>
        /// <returns>x/y.</returns>
        [PTXMathFunction("__nv_ddiv_rn")]
        [MathIntrinsic(MathIntrinsicKind.DivF64)]
        public static double Div(double x, double y)
        {
            return x / y;
        }

        /// <summary>
        /// Computes x/y.
        /// </summary>
        /// <param name="x">The nominator.</param>
        /// <param name="y">The denominator.</param>
        /// <returns>x/y.</returns>
        [PTXMathFunction("__nv_fdiv_rn")]
        [PTXFastMathFunction("__nv_fast_fdividef")]
        [MathIntrinsic(MathIntrinsicKind.DivF32)]
        public static float Div(float x, float y)
        {
            return x / y;
        }

        #endregion

        #region Rem

        /// <summary>
        /// Computes x%y.
        /// </summary>
        /// <param name="x">The nominator.</param>
        /// <param name="y">The denominator.</param>
        /// <returns>x%y.</returns>
        [PTXMathFunction("__nv_remainder")]
        [MathIntrinsic(MathIntrinsicKind.RemF64)]
        public static double Rem(double x, double y)
        {
            return x % y;
        }

        /// <summary>
        /// Computes x%y.
        /// </summary>
        /// <param name="x">The nominator.</param>
        /// <param name="y">The denominator.</param>
        /// <returns>x%y.</returns>
        [PTXMathFunction("__nv_remainderf")]
        [MathIntrinsic(MathIntrinsicKind.RemF32)]
        public static float Rem(float x, float y)
        {
            return x % y;
        }

        #endregion

        #region Sqrt / Cbrt

        /// <summary>
        /// Computes sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>sqrt(value).</returns>
        [PTXMathFunction("__nv_sqrt")]
        [MathIntrinsic(MathIntrinsicKind.SqrtF64)]
        public static double Sqrt(double value)
        {
            return Math.Sqrt(value);
        }

        /// <summary>
        /// Computes sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>sqrt(value).</returns>
        [PTXMathFunction("__nv_sqrtf")]
        [MathIntrinsic(MathIntrinsicKind.SqrtF32)]
        public static float Sqrt(float value)
        {
            return (float)Math.Sqrt(value);
        }

        /// <summary>
        /// Computes 1/sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>1/sqrt(value).</returns>
        [PTXMathFunction("__nv_rsqrt")]
        [MathIntrinsic(MathIntrinsicKind.RsqrtF64)]
        public static double Rsqrt(double value)
        {
            return 1.0 / Math.Sqrt(value);
        }

        /// <summary>
        /// Computes 1/sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>1/sqrt(value).</returns>
        [PTXMathFunction("__nv_rsqrtf")]
        [MathIntrinsic(MathIntrinsicKind.RsqrtF32)]
        public static float Rsqrt(float value)
        {
            return 1.0f / (float)Math.Sqrt(value);
        }

        /// <summary>
        /// Computes value^(1/3).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>value^(1/3).</returns>
        [PTXMathFunction("__nv_cbrt")]
        [MathIntrinsic(MathIntrinsicKind.CbrtF64)]
        public static double Cbrt(double value)
        {
            return Math.Pow(value, 1.0 / 3.0);
        }

        /// <summary>
        /// Computes value^(1/3).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>value^(1/3).</returns>
        [PTXMathFunction("__nv_cbrtf")]
        [MathIntrinsic(MathIntrinsicKind.CbrtF32)]
        public static float Cbrt(float value)
        {
            return Pow(value, 1.0f / 3.0f);
        }

        /// <summary>
        /// Computes 1.value^(1/3).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>1.value^(1/3).</returns>
        [PTXMathFunction("__nv_rcbrt")]
        [MathIntrinsic(MathIntrinsicKind.RcbrtF64)]
        public static double Rcbrt(double value)
        {
            return 1.0 / Rcbrt(value);
        }

        /// <summary>
        /// Computes value^(1/3).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>value^(1/3).</returns>
        [PTXMathFunction("__nv_rcbrtf")]
        [MathIntrinsic(MathIntrinsicKind.RcbrtF32)]
        public static float Rcbrt(float value)
        {
            return 1.0f / Rcbrt(value);
        }

        #endregion

        #region Sin / Cos / Tan

        /// <summary>
        /// Computes asin(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>asin(value).</returns>
        [PTXMathFunction("__nv_asin")]
        [MathIntrinsic(MathIntrinsicKind.AsinF64)]
        public static double Asin(double value)
        {
            return Math.Asin(value);
        }

        /// <summary>
        /// Computes asin(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>asin(value).</returns>
        [PTXMathFunction("__nv_asinf")]
        [MathIntrinsic(MathIntrinsicKind.AsinF32)]
        public static float Asin(float value)
        {
            return (float)Math.Asin(value);
        }

        /// <summary>
        /// Computes sin(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>sin(value).</returns>
        [PTXMathFunction("__nv_sin")]
        [MathIntrinsic(MathIntrinsicKind.SinF64)]
        public static double Sin(double value)
        {
            return Math.Sin(value);
        }

        /// <summary>
        /// Computes sin(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>sin(value).</returns>
        [PTXMathFunction("__nv_sinf")]
        [PTXFastMathFunction("__nv_fast_sinf")]
        [MathIntrinsic(MathIntrinsicKind.SinF32)]
        public static float Sin(float value)
        {
            return (float)Math.Sin(value);
        }

        /// <summary>
        /// Computes sinh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>sinh(value).</returns>
        [PTXMathFunction("__nv_sinh")]
        [MathIntrinsic(MathIntrinsicKind.SinHF64)]
        public static double Sinh(double value)
        {
            return Math.Sinh(value);
        }

        /// <summary>
        /// Computes sinh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>sinh(value).</returns>
        [PTXMathFunction("__nv_sinhf")]
        [MathIntrinsic(MathIntrinsicKind.SinHF32)]
        public static float Sinh(float value)
        {
            return (float)Math.Sinh(value);
        }

        /// <summary>
        /// Computes acos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>acos(value).</returns>
        [PTXMathFunction("__nv_acos")]
        [MathIntrinsic(MathIntrinsicKind.AcosF64)]
        public static double Acos(double value)
        {
            return Math.Acos(value);
        }

        /// <summary>
        /// Computes acos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>acos(value).</returns>
        [PTXMathFunction("__nv_acosf")]
        [MathIntrinsic(MathIntrinsicKind.AcosF32)]
        public static float Acos(float value)
        {
            return (float)Math.Acos(value);
        }

        /// <summary>
        /// Computes cos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>cos(value).</returns>
        [PTXMathFunction("__nv_cos")]
        [MathIntrinsic(MathIntrinsicKind.CosF64)]
        public static double Cos(double value)
        {
            return Math.Cos(value);
        }

        /// <summary>
        /// Computes cos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>cos(value).</returns>
        [PTXMathFunction("__nv_cosf")]
        [PTXFastMathFunction("__nv_fast_cosf")]
        [MathIntrinsic(MathIntrinsicKind.CosF32)]
        public static float Cos(float value)
        {
            return (float)Math.Cos(value);
        }

        /// <summary>
        /// Computes cosh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>cosh(value).</returns>
        [PTXMathFunction("__nv_cosh")]
        [MathIntrinsic(MathIntrinsicKind.CosHF64)]
        public static double Cosh(double value)
        {
            return Math.Cosh(value);
        }

        /// <summary>
        /// Computes cosh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>cosh(value).</returns>
        [PTXMathFunction("__nv_coshf")]
        [MathIntrinsic(MathIntrinsicKind.CosHF32)]
        public static float Cosh(float value)
        {
            return (float)Math.Cosh(value);
        }

        /// <summary>
        /// Computes atan(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>atan(value).</returns>
        [PTXMathFunction("__nv_atan")]
        [MathIntrinsic(MathIntrinsicKind.AtanF64)]
        public static double Atan(double value)
        {
            return Math.Atan(value);
        }

        /// <summary>
        /// Computes atan(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>atan(value).</returns>
        [PTXMathFunction("__nv_atanf")]
        [MathIntrinsic(MathIntrinsicKind.AtanF32)]
        public static float Atan(float value)
        {
            return (float)Math.Atan(value);
        }

        /// <summary>
        /// Computes atan2(x, y).
        /// </summary>
        /// <param name="x">The x coordinate of a point.</param>
        /// <param name="y">The y coordinate of a point.</param>
        /// <returns>atan2(x, y).</returns>
        [PTXMathFunction("__nv_atan2")]
        [MathIntrinsic(MathIntrinsicKind.Atan2F64)]
        public static double Atan2(double x, double y)
        {
            return Math.Atan2(x, y);
        }

        /// <summary>
        /// Computes atan2(x, y).
        /// </summary>
        /// <param name="x">The x coordinate of a point.</param>
        /// <param name="y">The y coordinate of a point.</param>
        /// <returns>atan2(x, y).</returns>
        [PTXMathFunction("__nv_atan2f")]
        [MathIntrinsic(MathIntrinsicKind.Atan2F32)]
        public static float Atan2(float x, float y)
        {
            return (float)Math.Atan2(x, y);
        }

        /// <summary>
        /// Computes tan(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>tan(value).</returns>
        [PTXMathFunction("__nv_tan")]
        [MathIntrinsic(MathIntrinsicKind.TanF64)]
        public static double Tan(double value)
        {
            return Math.Tan(value);
        }

        /// <summary>
        /// Computes tan(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>tan(value).</returns>
        [PTXMathFunction("__nv_tanf")]
        [PTXFastMathFunction("__nv_fast_tanf")]
        [MathIntrinsic(MathIntrinsicKind.TanF32)]
        public static float Tan(float value)
        {
            return (float)Math.Tan(value);
        }

        /// <summary>
        /// Computes tanh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>tanh(value).</returns>
        [PTXMathFunction("__nv_tanh")]
        [MathIntrinsic(MathIntrinsicKind.TanhF64)]
        public static double Tanh(double value)
        {
            return Math.Tanh(value);
        }

        /// <summary>
        /// Computes tanh(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <returns>tanh(value).</returns>
        [PTXMathFunction("__nv_tanhf")]
        [MathIntrinsic(MathIntrinsicKind.TanhF32)]
        public static float Tanh(float value)
        {
            return (float)Math.Tanh(value);
        }

        /// <summary>
        /// Computes sin(value) and cos(value).
        /// </summary>
        /// <param name="value">The value in radians.</param>
        /// <param name="sin">The result of sin(value).</param>
        /// <param name="cos">The result of cos(value).</param>
        /// <returns>tanh(value).</returns>
        [PTXMathFunction("__nv_sincos")]
        [MathIntrinsic(MathIntrinsicKind.SinCosF64)]
        public static void SinCos(double value, ref double sin, ref double cos)
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
        [PTXMathFunction("__nv_sincosf")]
        [MathIntrinsic(MathIntrinsicKind.SinCosF32)]
        public static void SinCos(float value, ref float sin, ref float cos)
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
        [PTXMathFunction("__nv_pow")]
        [MathIntrinsic(MathIntrinsicKind.PowF64)]
        public static double Pow(double @base, double exp)
        {
            return Math.Pow(@base, exp);
        }

        /// <summary>
        /// Computes basis^exp.
        /// </summary>
        /// <param name="base">The basis.</param>
        /// <param name="exp">The exponent.</param>
        /// <returns>pow(basis, exp).</returns>
        [PTXMathFunction("__nv_powf")]
        [PTXFastMathFunction("__nv_fast_powf")]
        [MathIntrinsic(MathIntrinsicKind.PowF32)]
        public static float Pow(float @base, float exp)
        {
            return (float)Math.Pow(@base, exp);
        }

        /// <summary>
        /// Computes exp(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>exp(value).</returns>
        [PTXMathFunction("__nv_exp")]
        [MathIntrinsic(MathIntrinsicKind.ExpF64)]
        public static double Exp(double value)
        {
            return Math.Exp(value);
        }

        /// <summary>
        /// Computes exp(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>exp(value).</returns>
        [PTXMathFunction("__nv_expf")]
        [PTXFastMathFunction("__nv_fast_expf")]
        [MathIntrinsic(MathIntrinsicKind.ExpF32)]
        public static float Exp(float value)
        {
            return (float)Math.Exp(value);
        }

        /// <summary>
        /// Computes 10^value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>10^value.</returns>
        [PTXMathFunction("__nv_exp10")]
        [MathIntrinsic(MathIntrinsicKind.Exp10F64)]
        public static double Exp10(double value)
        {
            return Math.Pow(10.0, value);
        }

        /// <summary>
        /// Computes exp(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>exp(value).</returns>
        [PTXMathFunction("__nv_exp10f")]
        [PTXFastMathFunction("__nv_fast_exp10f")]
        [MathIntrinsic(MathIntrinsicKind.Exp10F32)]
        public static float Exp10(float value)
        {
            return (float)Math.Pow(10.0, value);
        }

        #endregion

        #region Floor & Ceiling

        /// <summary>
        /// Computes floor(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>floor(value).</returns>
        [PTXMathFunction("__nv_floor")]
        [MathIntrinsic(MathIntrinsicKind.FloorF64)]
        public static double Floor(double value)
        {
            return Math.Floor(value);
        }

        /// <summary>
        /// Computes floor(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>floor(value).</returns>
        [PTXMathFunction("__nv_floorf")]
        [MathIntrinsic(MathIntrinsicKind.FloorF32)]
        public static float Floor(float value)
        {
            return (float)Math.Floor(value);
        }

        /// <summary>
        /// Computes ceiling(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>ceiling(value).</returns>
        [PTXMathFunction("__nv_ceil")]
        [MathIntrinsic(MathIntrinsicKind.CeilingF64)]
        public static double Ceiling(double value)
        {
            return Math.Ceiling(value);
        }

        /// <summary>
        /// Computes ceiling(value).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>ceiling(value).</returns>
        [PTXMathFunction("__nv_ceilf")]
        [MathIntrinsic(MathIntrinsicKind.CeilingF32)]
        public static float Ceiling(float value)
        {
            return (float)Math.Ceiling(value);
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
            return Log10(value) / Log10(newBase);
        }

        /// <summary>
        /// Computes log_newBase(value) to base newBase.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="newBase">The desired base.</param>
        /// <returns>log_newBase(value).</returns>
        public static float Log(float value, float newBase)
        {
            return Log10(value) / Log10(newBase);
        }

        /// <summary>
        /// Computes log(value) to base e.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>log(value).</returns>
        [PTXMathFunction("__nv_log")]
        [MathIntrinsic(MathIntrinsicKind.LogF64)]
        public static double Log(double value)
        {
            return Math.Log(value);
        }

        /// <summary>
        /// Computes log(value) to base e.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>log(value).</returns>
        [PTXMathFunction("__nv_logf")]
        [PTXFastMathFunction("__nv_fast_logf")]
        [MathIntrinsic(MathIntrinsicKind.LogF32)]
        public static float Log(float value)
        {
            return (float)Math.Log(value);
        }

        /// <summary>
        /// Computes log10(value) to base 10.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>log10(value).</returns>
        [PTXMathFunction("__nv_log10")]
        [MathIntrinsic(MathIntrinsicKind.Log10F64)]
        public static double Log10(double value)
        {
            return Math.Log10(value);
        }

        /// <summary>
        /// Computes log10(value) to base 10.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>log10(value).</returns>
        [PTXMathFunction("__nv_log10f")]
        [PTXFastMathFunction("__nv_fast_log10f")]
        [MathIntrinsic(MathIntrinsicKind.Log10F32)]
        public static float Log10(float value)
        {
            return (float)Math.Log10(value);
        }

        /// <summary>
        /// Computes log2(value) to base 2.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>log2(value).</returns>
        [PTXMathFunction("__nv_log2")]
        [MathIntrinsic(MathIntrinsicKind.Log2F64)]
        public static double Log2(double value)
        {
            return Log(2.0, value);
        }

        /// <summary>
        /// Computes log2(value) to base 2.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>log2(value).</returns>
        [PTXMathFunction("__nv_log2f")]
        [PTXFastMathFunction("__nv_fast_log2f")]
        [MathIntrinsic(MathIntrinsicKind.Log2F32)]
        public static float Log2(float value)
        {
            return Log(2.0f, value);
        }

        #endregion

        #region Abs

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [PTXMathFunction("__nv_fabs")]
        [MathIntrinsic(MathIntrinsicKind.AbsF64)]
        public static double Abs(double value)
        {
            return Math.Abs(value);
        }

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [PTXMathFunction("__nv_fabsf")]
        [MathIntrinsic(MathIntrinsicKind.AbsF32)]
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
        [PTXMathFunction("__nv_abs")]
        [MathIntrinsic(MathIntrinsicKind.AbsI32)]
        public static int Abs(int value)
        {
            return Math.Abs(value);
        }

        /// <summary>
        /// Computes |value|.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>|value|.</returns>
        [PTXMathFunction("__nv_llabs")]
        [MathIntrinsic(MathIntrinsicKind.AbsI64)]
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
        [PTXMathFunction("__nv_fmin")]
        [MathIntrinsic(MathIntrinsicKind.MinF64)]
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
        [PTXMathFunction("__nv_fminf")]
        [MathIntrinsic(MathIntrinsicKind.MinF32)]
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
        [PTXMathFunction("__nv_min")]
        [MathIntrinsic(MathIntrinsicKind.MinI32)]
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
        [PTXMathFunction("__nv_llmin")]
        [MathIntrinsic(MathIntrinsicKind.MinI64)]
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
        [PTXMathFunction("__nv_umin")]
        [MathIntrinsic(MathIntrinsicKind.MinUI32)]
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
        [PTXMathFunction("__nv_ullmin")]
        [MathIntrinsic(MathIntrinsicKind.MinUI64)]
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
        [PTXMathFunction("__nv_fmax")]
        [MathIntrinsic(MathIntrinsicKind.MaxF64)]
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
        [PTXMathFunction("__nv_fmaxf")]
        [MathIntrinsic(MathIntrinsicKind.MaxF32)]
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
        [PTXMathFunction("__nv_max")]
        [MathIntrinsic(MathIntrinsicKind.MaxI32)]
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
        [PTXMathFunction("__nv_llmax")]
        [MathIntrinsic(MathIntrinsicKind.MaxI64)]
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
        [PTXMathFunction("__nv_umax")]
        [MathIntrinsic(MathIntrinsicKind.MaxUI32)]
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
        [PTXMathFunction("__nv_ullmax")]
        [MathIntrinsic(MathIntrinsicKind.MaxUI64)]
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
            return Math.Max(Math.Min(value, max), min);
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
            return Math.Max(Math.Min(value, max), min);
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
            return Math.Max(Math.Min(value, max), min);
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
            return Math.Max(Math.Min(value, max), min);
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
            return Math.Max(Math.Min(value, max), min);
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
            return Math.Max(Math.Min(value, max), min);
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
            return Math.Max(Math.Min(value, max), min);
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
            return Math.Max(Math.Min(value, max), min);
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
            return Math.Max(Math.Min(value, max), min);
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
            return Math.Max(Math.Min(value, max), min);
        }


        #endregion

        #region Round & Truncate

        /// <summary>
        /// Rounds the value to the nearest even value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest even value.</returns>
        [PTXMathFunction("__nv_rint")]
        [MathIntrinsic(MathIntrinsicKind.RoundToEvenF64)]
        public static double RoundToEven(double value)
        {
            return Math.Round(value, MidpointRounding.ToEven);
        }

        /// <summary>
        /// Rounds the value to the nearest even value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest even value.</returns>
        [PTXMathFunction("__nv_rintf")]
        [MathIntrinsic(MathIntrinsicKind.RoundToEvenF32)]
        public static float RoundToEven(float value)
        {
            return (float)Math.Round(value, MidpointRounding.ToEven);
        }

        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded away from zero).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest value.</returns>
        [PTXMathFunction("__nv_round")]
        [MathIntrinsic(MathIntrinsicKind.RoundAwayFromZeroF64)]
        public static double RoundAwayFromZero(double value)
        {
            return Math.Round(value, MidpointRounding.ToEven);
        }

        /// <summary>
        /// Rounds the value to the nearest value (halfway cases are rounded away from zero).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The nearest value.</returns>
        [PTXMathFunction("__nv_roundf")]
        [MathIntrinsic(MathIntrinsicKind.RoundAwayFromZeroF32)]
        public static float RoundAwayFromZero(float value)
        {
            return (float)Math.Round(value, MidpointRounding.ToEven);
        }

        /// <summary>
        /// Rounds the given value according to the provided rounding mode.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="rounding">The rounding mode.</param>
        /// <returns>The rounded value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Round(double value, MidpointRounding rounding)
        {
            if (rounding == MidpointRounding.ToEven)
                return RoundToEven(value);
            return RoundAwayFromZero(value);
        }

        /// <summary>
        /// Rounds the given value according to the provided rounding mode.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="rounding">The rounding mode.</param>
        /// <returns>The rounded value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Round(float value, MidpointRounding rounding)
        {
            if (rounding == MidpointRounding.ToEven)
                return RoundToEven(value);
            return RoundAwayFromZero(value);
        }

        /// <summary>
        /// Truncates the given value.
        /// </summary>
        /// <param name="value">The value to truncate.</param>
        /// <returns>The truncated value.</returns>
        [PTXMathFunction("__nv_trunc")]
        [MathIntrinsic(MathIntrinsicKind.TruncateF64)]
        public static double Truncate(double value)
        {
            return Math.Truncate(value);
        }

        /// <summary>
        /// Truncates the given value.
        /// </summary>
        /// <param name="value">The value to truncate.</param>
        /// <returns>The truncated value.</returns>
        [PTXMathFunction("__nv_truncf")]
        [MathIntrinsic(MathIntrinsicKind.TruncateF32)]
        public static float Truncate(float value)
        {
            return (float)Math.Truncate(value);
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
            return (numerator - denominator + 1) / denominator;
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
            return (numerator - denominator + 1L) / denominator;
        }

        #endregion

        #endregion
    }
}
