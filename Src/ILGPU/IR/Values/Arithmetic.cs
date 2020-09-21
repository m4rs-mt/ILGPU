// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Arithmetic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
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
    /// Represents an abstract arithmetic value.
    /// </summary>
    public abstract class ArithmeticValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new arithmetic value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="flags">The operation flags.</param>
        internal ArithmeticValue(in ValueInitializer initializer, ArithmeticFlags flags)
            : base(initializer)
        {
            Flags = flags;
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
        /// Returns true if the operation has enabled overflow semantics.
        /// </summary>
        public bool CanOverflow => (Flags & ArithmeticFlags.Overflow) ==
            ArithmeticFlags.Overflow;

        /// <summary>
        /// Returns true if the operation has enabled unsigned semantics.
        /// </summary>
        public bool IsUnsigned => (Flags & ArithmeticFlags.Unsigned) ==
            ArithmeticFlags.Unsigned;

        /// <summary>
        /// Returns true if the operation works on integers.
        /// </summary>
        public bool IsIntOperation => BasicValueType.IsInt();

        /// <summary>
        /// Returns true if the operation works on floats.
        /// </summary>
        public bool IsFloatOperation => BasicValueType.IsFloat();

        #endregion
    }

    /// <summary>
    /// Represents a unary arithmetic operation.
    /// </summary>
    [ValueKind(ValueKind.UnaryArithmetic)]
    public sealed class UnaryArithmeticValue : ArithmeticValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new unary arithmetic operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="value">The operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        internal UnaryArithmeticValue(
            in ValueInitializer initializer,
            ValueReference value,
            UnaryArithmeticKind kind,
            ArithmeticFlags flags)
            : base(initializer, flags)
        {
            Kind = kind;

            Seal(value);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.UnaryArithmetic;

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

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer)
        {
            var type = Value.Type;
            switch (Kind)
            {
                case UnaryArithmeticKind.IsInfF:
                case UnaryArithmeticKind.IsNaNF:
                    type = initializer.Context.GetPrimitiveType(
                        BasicValueType.Int1);
                    break;
            }
            return type;
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateArithmetic(
                Location,
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
    [ValueKind(ValueKind.BinaryArithmetic)]
    public sealed class BinaryArithmeticValue : ArithmeticValue
    {
        #region Static

        /// <summary>
        /// Inverts the given binary arithmetic kind.
        /// </summary>
        /// <param name="kind">The kind to invert.</param>
        /// <returns>The inverted operation (if inverted).</returns>
        public static BinaryArithmeticKind InvertLogical(BinaryArithmeticKind kind) =>
            kind switch
            {
                BinaryArithmeticKind.And => BinaryArithmeticKind.Or,
                BinaryArithmeticKind.Or => BinaryArithmeticKind.And,
                _ => kind
            };

        /// <summary>
        /// Tries to invert the given binary arithmetic kind.
        /// </summary>
        /// <param name="kind">The kind to invert.</param>
        /// <param name="inverted">The inverted operation (if any).</param>
        /// <returns>True, if the given kind could be inverted.</returns>
        public static bool TryInvertLogical(
            BinaryArithmeticKind kind,
            out BinaryArithmeticKind inverted)
        {
            inverted = InvertLogical(kind);
            return kind != inverted;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new binary arithmetic value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        internal BinaryArithmeticValue(
            in ValueInitializer initializer,
            ValueReference left,
            ValueReference right,
            BinaryArithmeticKind kind,
            ArithmeticFlags flags)
            : base(initializer, flags)
        {
            bool isLeftPointer = left.Type.IsPointerType;
            bool isRightPointer = right.Type.IsPointerType;
            initializer.Assert(
                // Check whether the types are the same
                left.Type == right.Type ||

                // Check whether this is a raw pointer operation
                isLeftPointer && isRightPointer ||
                isLeftPointer && right.BasicValueType.IsInt() ||
                left.BasicValueType.IsInt() && isRightPointer ||

                // Check for shift operations
                (kind == BinaryArithmeticKind.Shl ||
                    kind == BinaryArithmeticKind.Shr) &&
                right.BasicValueType == BasicValueType.Int32);

            Kind = kind;
            Seal(left, right);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.BinaryArithmetic;

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

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            Left.Type;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateArithmetic(
                Location,
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
    [ValueKind(ValueKind.TernaryArithmetic)]
    public sealed class TernaryArithmeticValue : ArithmeticValue
    {
        #region Static

        /// <summary>
        /// Returns the left hand binary operation of a fused ternary operation.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <returns>The resolved binary operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BinaryArithmeticKind GetLeftBinaryKind(
            TernaryArithmeticKind kind) =>
            kind switch
            {
                TernaryArithmeticKind.MultiplyAdd => BinaryArithmeticKind.Mul,
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

        /// <summary>
        /// Returns the right hand binary operation of a fused ternary operation.
        /// </summary>
        /// <param name="kind">The arithmetic kind.</param>
        /// <returns>The resolved binary operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BinaryArithmeticKind GetRightBinaryKind(
            TernaryArithmeticKind kind) =>
            kind switch
            {
                TernaryArithmeticKind.MultiplyAdd => BinaryArithmeticKind.Add,
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new ternary arithmetic value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="first">The first operand.</param>
        /// <param name="second">The second operand.</param>
        /// <param name="third">The third operand.</param>
        /// <param name="kind">The operation kind.</param>
        /// <param name="flags">The operation flags.</param>
        internal TernaryArithmeticValue(
            in ValueInitializer initializer,
            ValueReference first,
            ValueReference second,
            ValueReference third,
            TernaryArithmeticKind kind,
            ArithmeticFlags flags)
            : base(initializer, flags)
        {
            Debug.Assert(
                first.Type == second.Type &&
                second.Type == third.Type, "Invalid types");

            Kind = kind;
            Seal(first, second, third);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.TernaryArithmetic;

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

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            First.Type;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateArithmetic(
                Location,
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
        protected override string ToArgString() =>
            $"{First}, {Second}, {Third} [{Flags}]";

        #endregion
    }
}
