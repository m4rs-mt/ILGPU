// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: BFloat16Conversion.tt/BFloat16Conversion.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2023 ILGPU Project
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
   public static partial class BFloat16Extensions
   {


        /// <summary>
        /// The reciprocal operation.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 RcpFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Rcp((float)value);

        /// <summary>
        /// Computes sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 SqrtFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Sqrt((float)value);

        /// <summary>
        /// Computes 1/sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 RsqrtFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Rsqrt((float)value);

        /// <summary>
        /// Computes asin(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 AsinFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Asin((float)value);

        /// <summary>
        /// Computes sin(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 SinFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Sin((float)value);

        /// <summary>
        /// Computes sinh(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 SinhFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Sinh((float)value);

        /// <summary>
        /// Computes acos(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 AcosFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Acos((float)value);

        /// <summary>
        /// Computes cos(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 CosFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Cos((float)value);

        /// <summary>
        /// Computes cosh(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 CoshFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Cosh((float)value);

        /// <summary>
        /// Computes tan(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 TanFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Tan((float)value);

        /// <summary>
        /// Computes tanh(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 TanhFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Tanh((float)value);

        /// <summary>
        /// Computes atan(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 AtanFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Atan((float)value);

        /// <summary>
        /// Computes exp(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 ExpFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Exp((float)value);

        /// <summary>
        /// Computes 2^x.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 Exp2FP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Exp2((float)value);

        /// <summary>
        /// Computes floor(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 FloorFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Floor((float)value);

        /// <summary>
        /// Computes ceil(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 CeilingFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Ceiling((float)value);

        /// <summary>
        /// Computes log(x) to base e.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 LogFP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Log((float)value);

        /// <summary>
        /// Computes log(x) to base 2.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 Log2FP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Log2((float)value);

        /// <summary>
        /// Computes log(x) to base 10.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 Log10FP32(BFloat16 value) =>
            (BFloat16)IntrinsicMath.CPUOnly.Log10((float)value);



        /// <summary>
        /// The % operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 RemFP32(BFloat16 left, BFloat16 right) =>
          (BFloat16)IntrinsicMath.CPUOnly.Rem((float)left, (float)right);

        /// <summary>
        /// The min operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 MinFP32(BFloat16 left, BFloat16 right) =>
          (BFloat16)IntrinsicMath.CPUOnly.Min((float)left, (float)right);

        /// <summary>
        /// The max operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 MaxFP32(BFloat16 left, BFloat16 right) =>
          (BFloat16)IntrinsicMath.CPUOnly.Max((float)left, (float)right);

        /// <summary>
        /// The atan2 operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 Atan2FP32(BFloat16 left, BFloat16 right) =>
          (BFloat16)IntrinsicMath.CPUOnly.Atan2((float)left, (float)right);

        /// <summary>
        /// The pow operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 PowFP32(BFloat16 left, BFloat16 right) =>
          (BFloat16)IntrinsicMath.CPUOnly.Pow((float)left, (float)right);

        /// <summary>
        /// The binary log operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 LogFP32(BFloat16 left, BFloat16 right) =>
          (BFloat16)IntrinsicMath.CPUOnly.Log((float)left, (float)right);


    }
}
