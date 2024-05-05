// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: BF16Conversion.tt/BF16Conversion.cs
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
   public static partial class BF16Extensions
   {



        /// <summary>
        /// The reciprocal operation.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 RcpFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Rcp((float)value);

        /// <summary>
        /// Computes sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 SqrtFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Sqrt((float)value);

        /// <summary>
        /// Computes 1/sqrt(value).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 RsqrtFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Rsqrt((float)value);

        /// <summary>
        /// Computes asin(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 AsinFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Asin((float)value);

        /// <summary>
        /// Computes sin(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 SinFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Sin((float)value);

        /// <summary>
        /// Computes sinh(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 SinhFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Sinh((float)value);

        /// <summary>
        /// Computes acos(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 AcosFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Acos((float)value);

        /// <summary>
        /// Computes cos(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 CosFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Cos((float)value);

        /// <summary>
        /// Computes cosh(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 CoshFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Cosh((float)value);

        /// <summary>
        /// Computes tan(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 TanFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Tan((float)value);

        /// <summary>
        /// Computes tanh(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 TanhFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Tanh((float)value);

        /// <summary>
        /// Computes atan(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 AtanFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Atan((float)value);

        /// <summary>
        /// Computes exp(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 ExpFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Exp((float)value);

        /// <summary>
        /// Computes 2^x.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 Exp2FP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Exp2((float)value);

        /// <summary>
        /// Computes floor(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 FloorFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Floor((float)value);

        /// <summary>
        /// Computes ceil(x).
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 CeilingFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Ceiling((float)value);

        /// <summary>
        /// Computes log(x) to base e.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 LogFP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Log((float)value);

        /// <summary>
        /// Computes log(x) to base 2.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 Log2FP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Log2((float)value);

        /// <summary>
        /// Computes log(x) to base 10.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 Log10FP32(BF16 value) =>
            (BF16)IntrinsicMath.CPUOnly.Log10((float)value);



        /// <summary>
        /// The % operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 RemFP32(BF16 left, BF16 right) =>
          (BF16)IntrinsicMath.CPUOnly.Rem((float)left, (float)right);

        /// <summary>
        /// The min operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 MinFP32(BF16 left, BF16 right) =>
          (BF16)IntrinsicMath.CPUOnly.Min((float)left, (float)right);

        /// <summary>
        /// The max operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 MaxFP32(BF16 left, BF16 right) =>
          (BF16)IntrinsicMath.CPUOnly.Max((float)left, (float)right);

        /// <summary>
        /// The atan2 operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 Atan2FP32(BF16 left, BF16 right) =>
          (BF16)IntrinsicMath.CPUOnly.Atan2((float)left, (float)right);

        /// <summary>
        /// The pow operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 PowFP32(BF16 left, BF16 right) =>
          (BF16)IntrinsicMath.CPUOnly.Pow((float)left, (float)right);

        /// <summary>
        /// The binary log operation.
        /// </summary>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BF16 LogFP32(BF16 left, BF16 right) =>
          (BF16)IntrinsicMath.CPUOnly.Log((float)left, (float)right);


    }
}