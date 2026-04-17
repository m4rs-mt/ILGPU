// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: FP8E5M2Conversion.tt/FP8E5M2Conversion.cs
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
   public static partial class FP8E5M2Extensions
   {



        /// <summary>
        /// The reciprocal operation.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 RcpFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Rcp((float)value);

        /// <summary>
        /// Computes sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 SqrtFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Sqrt((float)value);

        /// <summary>
        /// Computes 1/sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 RsqrtFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Rsqrt((float)value);

        /// <summary>
        /// Computes asin(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 AsinFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Asin((float)value);

        /// <summary>
        /// Computes sin(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 SinFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Sin((float)value);

        /// <summary>
        /// Computes sinh(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 SinhFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Sinh((float)value);

        /// <summary>
        /// Computes acos(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 AcosFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Acos((float)value);

        /// <summary>
        /// Computes cos(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 CosFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Cos((float)value);

        /// <summary>
        /// Computes cosh(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 CoshFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Cosh((float)value);

        /// <summary>
        /// Computes tan(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 TanFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Tan((float)value);

        /// <summary>
        /// Computes tanh(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 TanhFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Tanh((float)value);

        /// <summary>
        /// Computes atan(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 AtanFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Atan((float)value);

        /// <summary>
        /// Computes exp(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 ExpFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Exp((float)value);

        /// <summary>
        /// Computes 2^x.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 Exp2FP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Exp2((float)value);

        /// <summary>
        /// Computes floor(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 FloorFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Floor((float)value);

        /// <summary>
        /// Computes ceil(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 CeilingFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Ceiling((float)value);

        /// <summary>
        /// Computes log(x) to base e.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 LogFP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Log((float)value);

        /// <summary>
        /// Computes log(x) to base 2.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 Log2FP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Log2((float)value);

        /// <summary>
        /// Computes log(x) to base 10.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 Log10FP32(FP8E5M2 value) =>
            (FP8E5M2)IntrinsicMath.CPUOnly.Log10((float)value);



        /// <summary>
        /// The % operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 RemFP32(FP8E5M2 left,
            BF16 right)
            => (FP8E5M2)IntrinsicMath.CPUOnly.Rem((float)left, (float)right);

        /// <summary>
        /// The min operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 MinFP32(FP8E5M2 left,
            BF16 right)
            => (FP8E5M2)IntrinsicMath.CPUOnly.Min((float)left, (float)right);

        /// <summary>
        /// The max operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 MaxFP32(FP8E5M2 left,
            BF16 right)
            => (FP8E5M2)IntrinsicMath.CPUOnly.Max((float)left, (float)right);

        /// <summary>
        /// The atan2 operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 Atan2FP32(FP8E5M2 left,
            BF16 right)
            => (FP8E5M2)IntrinsicMath.CPUOnly.Atan2((float)left, (float)right);

        /// <summary>
        /// The pow operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 PowFP32(FP8E5M2 left,
            BF16 right)
            => (FP8E5M2)IntrinsicMath.CPUOnly.Pow((float)left, (float)right);

        /// <summary>
        /// The binary log operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP8E5M2 LogFP32(FP8E5M2 left,
            BF16 right)
            => (FP8E5M2)IntrinsicMath.CPUOnly.Log((float)left, (float)right);


    }
}