// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Arithmetic.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents flags of an arithmetic operation.
    /// </summary>
    [Flags]
    public enum ArithmeticFlags
    {
        /// <summary>
        /// No special flags (default).
        /// </summary>
        None = 0,

        /// <summary>
        /// The operation has overflow semantics.
        /// </summary>
        Overflow = 1,

        /// <summary>
        /// The operation has unsigned semantics.
        /// </summary>
        Unsigned = 2,

        /// <summary>
        /// The operation has overflow semantics and the
        /// overflow check is based on unsigned semantics.
        /// </summary>
        OverflowUnsigned = 3,
    }

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
        /// The mathematical <see cref="Math.Abs(int)"/> operation.
        /// </summary>
        Abs,

        /// <summary>
        /// The reciprocal operation.
        /// </summary>
        RcpF,

        /// <summary>
        /// The is-not-a-number operation.
        /// </summary>
        IsNaNF,

        /// <summary>
        /// The is-infitity operation.
        /// </summary>
        IsInfF,

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
        SinHF,

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
        CosHF,

        /// <summary>
        /// Computes tan(x).
        /// </summary>
        TanF,

        /// <summary>
        /// Computes tanh(x).
        /// </summary>
        TanHF,

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
        /// Computes sign(x);
        /// </summary>
        SignF,

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
        /// Computes log2(x) to base 2.
        /// </summary>
        Log2F,

        /// <summary>
        /// Computes log10(x) to base 10.
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
        /// The &amp; operation.
        /// </summary>
        And,

        /// <summary>
        /// The | operation.
        /// </summary>
        Or,

        /// <summary>
        /// The ^ operation.
        /// </summary>
        Xor,

        /// <summary>
        /// The &lt;&lt; operation.
        /// </summary>
        Shl,

        /// <summary>
        /// The &gt;&gt; operation.
        /// </summary>
        Shr,

        /// <summary>
        /// Computes min(a, b).
        /// </summary>
        Min,

        /// <summary>
        /// Computes max(a, b).
        /// </summary>
        Max,

        /// <summary>
        /// Computes atan2(x, y).
        /// </summary>
        Atan2F,

        /// <summary>
        /// Computes basis^exp.
        /// </summary>
        PowF,
    }

    /// <summary>
    /// Represents the kind of a ternary operation.
    /// </summary>
    public enum TernaryArithmeticKind
    {
        /// <summary>
        /// The * + operation.
        /// </summary>
        MultiplyAdd,
    }

    /// <summary>
    /// Represents an abstract arithmetic value.
    /// </summary>
    public abstract class ArithmeticValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new arithmetic value.
        /// </summary>
        /// <param name="kind">The value kind.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="operands">The operands.</param>
        /// <param name="flags">The operation flags.</param>
        /// <param name="initialType">The initial node type.</param>
        internal ArithmeticValue(
            ValueKind kind,
            BasicBlock basicBlock,
            ImmutableArray<ValueReference> operands,
            ArithmeticFlags flags,
            TypeNode initialType)
            : base(kind, basicBlock, initialType)
        {
            Flags = flags;

            Seal(operands);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated type.
        /// </summary>
        public PrimitiveType PrimitiveType => Type as PrimitiveType;

        /// <summary>
        /// Returns the operation flags.
        /// </summary>
        public ArithmeticFlags Flags { get; }

        /// <summary>
        /// Returns the associated arithmetic basic value type.
        /// </summary>
        public ArithmeticBasicValueType ArithmeticBasicValueType =>
            this[0].BasicValueType.GetArithmeticBasicValueType(IsUnsigned);

        /// <summary>
        /// Returns true iff the operation has enabled overflow semantics.
        /// </summary>
        public bool CanOverflow => (Flags & ArithmeticFlags.Overflow) ==
            ArithmeticFlags.Overflow;

        /// <summary>
        /// Returns true iff the operation has enabled unsigned semantics.
        /// </summary>
        public bool IsUnsigned => (Flags & ArithmeticFlags.Unsigned) ==
            ArithmeticFlags.Unsigned;

        /// <summary>
        /// Returns true iff the operation works on ints.
        /// </summary>
        public bool IsIntOperation => BasicValueType.IsInt();

        /// <summary>
        /// Returns true iff the operation works on floats.
        /// </summary>
        public bool IsFloatOperation => BasicValueType.IsFloat();

        #endregion
    }

    /// <summary>
    /// Reprensents a unary arithmetic operation.
    /// </summary>
    public sealed class UnaryArithmeticValue : ArithmeticValue
    {
        #region Static

        /// <summary>
        /// Computes an arithmetic node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="operand">The arithmetic operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(
            IRContext context,
            ValueReference operand,
            UnaryArithmeticKind kind)
        {
            var type = operand.Type;
            switch (kind)
            {
                case UnaryArithmeticKind.IsInfF:
                case UnaryArithmeticKind.IsNaNF:
                    type = context.GetPrimitiveType(BasicValueType.Int1);
                    break;
            }
            return type;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new unary arithmetic operation.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="value">The operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        internal UnaryArithmeticValue(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference value,
            UnaryArithmeticKind kind,
            ArithmeticFlags flags)
            : base(
                  ValueKind.UnaryArithmetic,
                  basicBlock,
                  ImmutableArray.Create(value),
                  flags,
                  ComputeType(context, value, kind))
        {
            Kind = kind;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the operation kind.
        /// </summary>
        public UnaryArithmeticKind Kind { get; }

        /// <summary>
        /// Returns the operand.
        /// </summary>
        public ValueReference Value => this[0];

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context, Value, Kind);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateArithmetic(
                rebuilder.Rebuild(Value),
                Kind,
                Flags);

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "arith.un." + Kind.ToString();

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => Value.ToString();

        #endregion
    }

    /// <summary>
    /// Represents a binary arithmetic operation.
    /// </summary>
    public sealed class BinaryArithmeticValue : ArithmeticValue
    {
        #region Static

        /// <summary>
        /// Computes an arithmetic node type.
        /// </summary>
        /// <param name="operand">The first arithmetic operand.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(ValueReference operand) =>
            operand.Type;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new binary arithmetic value.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        internal BinaryArithmeticValue(
            BasicBlock basicBlock,
            ValueReference left,
            ValueReference right,
            BinaryArithmeticKind kind,
            ArithmeticFlags flags)
            : base(
                  ValueKind.BinaryArithmetic,
                  basicBlock,
                  ImmutableArray.Create(left, right),
                  flags,
                  ComputeType(left))
        {
            Debug.Assert(
                left.Type == right.Type ||
                (kind == BinaryArithmeticKind.Shl || kind == BinaryArithmeticKind.Shr) &&
                right.BasicValueType == BasicValueType.Int32, "Invalid types");

            Kind = kind;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the operation kind.
        /// </summary>
        public BinaryArithmeticKind Kind { get; }

        /// <summary>
        /// Returns the left operand.
        /// </summary>
        public ValueReference Left => this[0];

        /// <summary>
        /// Returns the right operand.
        /// </summary>
        public ValueReference Right => this[1];

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(Left);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateArithmetic(
                rebuilder.Rebuild(Left),
                rebuilder.Rebuild(Right),
                Kind,
                Flags);

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "arith.bin." + Kind.ToString();

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Left}, {Right} [{Flags}]";

        #endregion
    }

    /// <summary>
    /// Represents a binary arithmetic operation.
    /// </summary>
    public sealed class TernaryArithmeticValue : ArithmeticValue
    {
        #region Static

        /// <summary>
        /// Computes an arithmetic node type.
        /// </summary>
        /// <param name="operand">The first arithmetic operand.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(ValueReference operand) =>
            operand.Type;

        /// <summary>
        /// Returns the left hand binary operation of a fused ternary operation.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <returns>The resolved binary operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BinaryArithmeticKind GetLeftBinaryKind(TernaryArithmeticKind kind)
        {
            switch (kind)
            {
                case TernaryArithmeticKind.MultiplyAdd:
                    return BinaryArithmeticKind.Mul;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>
        /// Returns the right hand binary operation of a fused ternary operation.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <returns>The resolved binary operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BinaryArithmeticKind GetRightBinaryKind(TernaryArithmeticKind kind)
        {
            switch (kind)
            {
                case TernaryArithmeticKind.MultiplyAdd:
                    return BinaryArithmeticKind.Add;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new ternary arithmetic value.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <param name="third">The third operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        internal TernaryArithmeticValue(
            BasicBlock basicBlock,
            ValueReference first,
            ValueReference second,
            ValueReference third,
            TernaryArithmeticKind kind,
            ArithmeticFlags flags)
            : base(
                  ValueKind.TernaryArithmetic,
                  basicBlock,
                  ImmutableArray.Create(first, second, third),
                  flags,
                  ComputeType(first))
        {
            Debug.Assert(
                first.Type == second.Type &&
                second.Type == third.Type, "Invalid types");

            Kind = kind;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the operation kind.
        /// </summary>
        public TernaryArithmeticKind Kind { get; }

        /// <summary>
        /// Returns the first operand.
        /// </summary>
        public ValueReference First => this[0];

        /// <summary>
        /// Returns the second operand.
        /// </summary>
        public ValueReference Second => this[1];

        /// <summary>
        /// Returns the third operand.
        /// </summary>
        public ValueReference Third => this[2];

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(First);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateArithmetic(
                rebuilder.Rebuild(First),
                rebuilder.Rebuild(Second),
                rebuilder.Rebuild(Third),
                Kind,
                Flags);

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "arith.ter." + Kind.ToString();

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{First}, {Second}, {Third} [{Flags}]";

        #endregion
    }
}
