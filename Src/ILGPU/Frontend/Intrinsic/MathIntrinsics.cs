// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: MathIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Resources;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    enum MathIntrinsicKind
    {
        Neg = UnaryArithmeticKind.Neg,
        Not = UnaryArithmeticKind.Not,
        Abs = UnaryArithmeticKind.Abs,
        RcpF = UnaryArithmeticKind.RcpF,
        IsNaNF = UnaryArithmeticKind.IsNaNF,
        IsInfF = UnaryArithmeticKind.IsInfF,
        SqrtF = UnaryArithmeticKind.SqrtF,
        RsqrtF = UnaryArithmeticKind.RsqrtF,
        AsinF = UnaryArithmeticKind.AsinF,
        SinF = UnaryArithmeticKind.SinF,
        SinHF = UnaryArithmeticKind.SinHF,
        AcosF = UnaryArithmeticKind.AcosF,
        CosF = UnaryArithmeticKind.CosF,
        CosHF = UnaryArithmeticKind.CosHF,
        TanF = UnaryArithmeticKind.TanF,
        TanHF = UnaryArithmeticKind.TanHF,
        AtanF = UnaryArithmeticKind.AtanF,
        ExpF = UnaryArithmeticKind.ExpF,
        Exp2F = UnaryArithmeticKind.Exp2F,
        FloorF = UnaryArithmeticKind.FloorF,
        CeilingF = UnaryArithmeticKind.CeilingF,
        LogF = UnaryArithmeticKind.LogF,
        Log2F = UnaryArithmeticKind.Log2F,
        Log10F = UnaryArithmeticKind.Log10F,

        _BinaryFunctions,

        Add,
        Sub,
        Mul,
        Div,
        Rem,
        And,
        Or,
        Xor,
        Shl,
        Shr,
        Min,
        Max,
        Atan2F,
        PowF,
    }

    /// <summary>
    /// Marks math methods that are built in.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class MathIntrinsicAttribute : IntrinsicAttribute
    {
        public MathIntrinsicAttribute(MathIntrinsicKind kind)
            : this(kind, ArithmeticFlags.None)
        { }

        public MathIntrinsicAttribute(
            MathIntrinsicKind kind,
            ArithmeticFlags flags)
        {
            IntrinsicKind = kind;
            IntrinsicFlags = flags;
        }

        public override IntrinsicType Type => IntrinsicType.Math;

        /// <summary>
        /// Returns the associated intrinsic kind.
        /// </summary>
        public MathIntrinsicKind IntrinsicKind { get; }

        /// <summary>
        /// Returns the associated intrinsic flags.
        /// </summary>
        public ArithmeticFlags IntrinsicFlags { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles math operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleMathOperation(
            in InvocationContext context,
            MathIntrinsicAttribute attribute)
        {
            switch (context.NumArguments)
            {
                case 1:
                    return context.Builder.CreateArithmetic(
                        context[0],
                        (UnaryArithmeticKind)attribute.IntrinsicKind,
                        attribute.IntrinsicFlags);
                case 2:
                    var kindIndex = attribute.IntrinsicKind -
                        MathIntrinsicKind._BinaryFunctions - 1;
                    return context.Builder.CreateArithmetic(
                        context[0],
                        context[1],
                        (BinaryArithmeticKind)kindIndex,
                        attribute.IntrinsicFlags);
                default:
                    throw context.GetNotSupportedException(
                        ErrorMessages.NotSupportedMathIntrinsic,
                        context.NumArguments.ToString());
            }
        }

    }
}
