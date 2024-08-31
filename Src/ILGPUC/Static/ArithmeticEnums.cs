// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArithmeticEnums.tt/ArithmeticEnums.cs
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

using ILGPU.IR.Values;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents the kind of an unary operation.
    /// </summary>
    public enum UnaryArithmeticKind
    {
        /// <summary>
        /// The neg operation.
        /// </summary>
        Neg,

        /// <summary>
        /// The logical not operation.
        /// </summary>
        Not,

        /// <summary>
        /// The abs operation.
        /// </summary>
        Abs,

        /// <summary>
        /// The popcount operation.
        /// </summary>
        PopC,

        /// <summary>
        /// The CLZ operation.
        /// </summary>
        CLZ,

        /// <summary>
        /// The CTZ operation.
        /// </summary>
        CTZ,

        /// <summary>
        /// The reciprocal operation.
        /// </summary>
        RcpF,

        /// <summary>
        /// The is-not-a-number operation.
        /// </summary>
        IsNaNF,

        /// <summary>
        /// The is-infinity operation.
        /// </summary>
        IsInfF,

        /// <summary>
        /// The is-finite operation.
        /// </summary>
        IsFinF,

        /// <summary>
        /// Computes sqrt(value).
        /// </summary>
        SqrtF,

        /// <summary>
        /// Computes 1/sqrt(value).
        /// </summary>
        RsqrtF,

        /// <summary>
        /// Computes asin(x).
        /// </summary>
        AsinF,

        /// <summary>
        /// Computes sin(x).
        /// </summary>
        SinF,

        /// <summary>
        /// Computes sinh(x).
        /// </summary>
        SinhF,

        /// <summary>
        /// Computes acos(x).
        /// </summary>
        AcosF,

        /// <summary>
        /// Computes cos(x).
        /// </summary>
        CosF,

        /// <summary>
        /// Computes cosh(x).
        /// </summary>
        CoshF,

        /// <summary>
        /// Computes tan(x).
        /// </summary>
        TanF,

        /// <summary>
        /// Computes tanh(x).
        /// </summary>
        TanhF,

        /// <summary>
        /// Computes atan(x).
        /// </summary>
        AtanF,

        /// <summary>
        /// Computes exp(x).
        /// </summary>
        ExpF,

        /// <summary>
        /// Computes 2^x.
        /// </summary>
        Exp2F,

        /// <summary>
        /// Computes floor(x).
        /// </summary>
        FloorF,

        /// <summary>
        /// Computes ceil(x).
        /// </summary>
        CeilingF,

        /// <summary>
        /// Computes log(x) to base e.
        /// </summary>
        LogF,

        /// <summary>
        /// Computes log(x) to base 2.
        /// </summary>
        Log2F,

        /// <summary>
        /// Computes log(x) to base 10.
        /// </summary>
        Log10F,

    }

    /// <summary>
    /// Represents the kind of a binary operation.
    /// </summary>
    public enum BinaryArithmeticKind
    {
        /// <summary>
        /// The + operation.
        /// </summary>
        Add,

        /// <summary>
        /// The - operation.
        /// </summary>
        Sub,

        /// <summary>
        /// The * operation.
        /// </summary>
        Mul,

        /// <summary>
        /// The / operation.
        /// </summary>
        Div,

        /// <summary>
        /// The % operation.
        /// </summary>
        Rem,

        /// <summary>
        /// The logical and operation.
        /// </summary>
        And,

        /// <summary>
        /// The logical or operation.
        /// </summary>
        Or,

        /// <summary>
        /// The logical xor operation.
        /// </summary>
        Xor,

        /// <summary>
        /// The shift left operation.
        /// </summary>
        Shl,

        /// <summary>
        /// The shift right operation.
        /// </summary>
        Shr,

        /// <summary>
        /// The min operation.
        /// </summary>
        Min,

        /// <summary>
        /// The max operation.
        /// </summary>
        Max,

        /// <summary>
        /// The atan2 operation.
        /// </summary>
        Atan2F,

        /// <summary>
        /// The pow operation.
        /// </summary>
        PowF,

        /// <summary>
        /// The binary log operation.
        /// </summary>
        BinaryLogF,

        /// <summary>
        /// The copy sign operation.
        /// </summary>
        CopySignF,

    }

    /// <summary>
    /// Represents the kind of a ternary operation.
    /// </summary>
    public enum TernaryArithmeticKind
    {
        /// <summary>
        /// The FMA operation.
        /// </summary>
        MultiplyAdd,

    }

    /// <summary>
    /// Contains several extensions for arithmetic kinds.
    /// </summary>
    public static class ArithmeticKindExtensions
    {
        /// <summary>
        /// Returns true if the given kind is commutative.
        /// </summary>
        /// <param name="kind">The kind to test.</param>
        /// <returns>True, if the given kind is commutative.</returns>
        public static bool IsCommutative(this BinaryArithmeticKind kind) =>
            kind switch
            {
                BinaryArithmeticKind.Add => true,
                BinaryArithmeticKind.Mul => true,
                BinaryArithmeticKind.And => true,
                BinaryArithmeticKind.Or => true,
                BinaryArithmeticKind.Xor => true,
                BinaryArithmeticKind.Min => true,
                BinaryArithmeticKind.Max => true,
                _ => false
            };

        /// <summary>
        /// Returns true if the given kind is commutative.
        /// </summary>
        /// <param name="kind">The kind to test.</param>
        /// <returns>True, if the given kind is commutative.</returns>
        public static bool IsCommutative(this TernaryArithmeticKind kind) =>
            false;
    }
}

namespace ILGPU.Frontend.Intrinsic
{
    enum MathIntrinsicKind
    {
        Neg = UnaryArithmeticKind.Neg,
        Not = UnaryArithmeticKind.Not,
        Abs = UnaryArithmeticKind.Abs,
        PopC = UnaryArithmeticKind.PopC,
        CLZ = UnaryArithmeticKind.CLZ,
        CTZ = UnaryArithmeticKind.CTZ,
        RcpF = UnaryArithmeticKind.RcpF,
        IsNaNF = UnaryArithmeticKind.IsNaNF,
        IsInfF = UnaryArithmeticKind.IsInfF,
        IsFinF = UnaryArithmeticKind.IsFinF,
        SqrtF = UnaryArithmeticKind.SqrtF,
        RsqrtF = UnaryArithmeticKind.RsqrtF,
        AsinF = UnaryArithmeticKind.AsinF,
        SinF = UnaryArithmeticKind.SinF,
        SinhF = UnaryArithmeticKind.SinhF,
        AcosF = UnaryArithmeticKind.AcosF,
        CosF = UnaryArithmeticKind.CosF,
        CoshF = UnaryArithmeticKind.CoshF,
        TanF = UnaryArithmeticKind.TanF,
        TanhF = UnaryArithmeticKind.TanhF,
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
        BinaryLogF,
        CopySignF,

        _TernaryFunctions,

        MultiplyAdd,
    }
}