// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Mini43AIFloat8Conversion.tt/Mini43AIFloat8Conversion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------



// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeInformation.ttinclude
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using System;
using System.Runtime.CompilerServices;


namespace ILGPU
{
   public static partial class Mini43AIFloat8Extensions
   {



        /// <summary>
        /// The reciprocal operation.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 RcpFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Rcp((float)value);

        /// <summary>
        /// Computes sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 SqrtFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Sqrt((float)value);

        /// <summary>
        /// Computes 1/sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 RsqrtFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Rsqrt((float)value);

        /// <summary>
        /// Computes asin(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 AsinFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Asin((float)value);

        /// <summary>
        /// Computes sin(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 SinFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Sin((float)value);

        /// <summary>
        /// Computes sinh(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 SinhFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Sinh((float)value);

        /// <summary>
        /// Computes acos(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 AcosFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Acos((float)value);

        /// <summary>
        /// Computes cos(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 CosFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Cos((float)value);

        /// <summary>
        /// Computes cosh(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 CoshFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Cosh((float)value);

        /// <summary>
        /// Computes tan(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 TanFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Tan((float)value);

        /// <summary>
        /// Computes tanh(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 TanhFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Tanh((float)value);

        /// <summary>
        /// Computes atan(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 AtanFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Atan((float)value);

        /// <summary>
        /// Computes exp(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 ExpFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Exp((float)value);

        /// <summary>
        /// Computes 2^x.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 Exp2FP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Exp2((float)value);

        /// <summary>
        /// Computes floor(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 FloorFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Floor((float)value);

        /// <summary>
        /// Computes ceil(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 CeilingFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Ceiling((float)value);

        /// <summary>
        /// Computes log(x) to base e.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 LogFP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Log((float)value);

        /// <summary>
        /// Computes log(x) to base 2.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 Log2FP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Log2((float)value);

        /// <summary>
        /// Computes log(x) to base 10.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 Log10FP32(Mini43AIFloat8 value) =>
            (Mini43AIFloat8)IntrinsicMath.CPUOnly.Log10((float)value);



        /// <summary>
        /// The % operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 RemFP32(Mini43AIFloat8 left,
            BFloat16 right)
            =>(Mini43AIFloat8)IntrinsicMath.CPUOnly.Rem((float)left, (float)right);

        /// <summary>
        /// The min operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 MinFP32(Mini43AIFloat8 left,
            BFloat16 right)
            =>(Mini43AIFloat8)IntrinsicMath.CPUOnly.Min((float)left, (float)right);

        /// <summary>
        /// The max operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 MaxFP32(Mini43AIFloat8 left,
            BFloat16 right)
            =>(Mini43AIFloat8)IntrinsicMath.CPUOnly.Max((float)left, (float)right);

        /// <summary>
        /// The atan2 operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 Atan2FP32(Mini43AIFloat8 left,
            BFloat16 right)
            =>(Mini43AIFloat8)IntrinsicMath.CPUOnly.Atan2((float)left, (float)right);

        /// <summary>
        /// The pow operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 PowFP32(Mini43AIFloat8 left,
            BFloat16 right)
            =>(Mini43AIFloat8)IntrinsicMath.CPUOnly.Pow((float)left, (float)right);

        /// <summary>
        /// The binary log operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Mini43AIFloat8 LogFP32(Mini43AIFloat8 left,
            BFloat16 right)
            =>(Mini43AIFloat8)IntrinsicMath.CPUOnly.Log((float)left, (float)right);


    }
}