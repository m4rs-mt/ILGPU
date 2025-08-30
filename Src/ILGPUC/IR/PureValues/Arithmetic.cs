// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Arithmetic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.Util;
using System;
using System.Diagnostics;

namespace ILGPUC.IR.PureValues;

/// <summary>
/// Represents flags of an arithmetic operation.
/// </summary>
[Flags]
enum ArithmeticFlags
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
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
/// <param name="flags">The operation flags.</param>
abstract class ArithmeticValue(
    in PureValueInitializer initializer,
    TypeValue type,
    ArithmeticFlags flags) : PureValue(initializer, type)
{
    /// <summary>
    /// Returns the associated type.
    /// </summary>
    public PrimitiveType PrimitiveType => GetTypeAs<PrimitiveType>();

    /// <summary>
    /// Returns the operation flags.
    /// </summary>
    public ArithmeticFlags Flags { get; } = flags;

    /// <summary>
    /// Returns the associated arithmetic basic value type.
    /// </summary>
    public ArithmeticBasicValueType ArithmeticBasicValueType =>
        GetValue<Value>(0).BasicValueType.GetArithmeticBasicValueType(IsUnsigned);

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
}

/// <summary>
/// Represents a unary arithmetic operation.
/// </summary>
sealed partial class UnaryArithmeticValue : ArithmeticValue
{
    /// <summary>
    /// Determines the type of a new arithmetic operation.
    /// </summary>
    private static TypeValue GetType(
        in PureValueInitializer initializer,
        Value value,
        UnaryArithmeticKind kind)
    {
        var type = kind switch
        {
            UnaryArithmeticKind.IsNaN or
            UnaryArithmeticKind.IsInf or
            UnaryArithmeticKind.IsFin =>
                initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int1),
            UnaryArithmeticKind.PopC or
            UnaryArithmeticKind.CLZ or
            UnaryArithmeticKind.CTZ =>
                initializer.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32),
            _ => value.Type,
        };
        return type;
    }

    /// <summary>
    /// Constructs a new unary arithmetic operation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="value">The operand.</param>
    /// <param name="kind">The operation kind.</param>
    /// <param name="flags">The operation flags.</param>
    public UnaryArithmeticValue(
        in PureValueInitializer initializer,
        Value value,
        UnaryArithmeticKind kind,
        ArithmeticFlags flags)
        : base(initializer, GetType(initializer, value, kind), flags)
    {
        Kind = kind;

        Seal(value);
    }

    /// <summary>
    /// Returns the operand.
    /// </summary>
    public Value Value => GetValue<Value>(0);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateArithmetic(Location, rewriter.Rewrite(Value), Kind, Flags);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "arith.un." + Kind.ToString();

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => Value.ToReferenceString();
}

/// <summary>
/// Represents a binary arithmetic operation.
/// </summary>
sealed partial class BinaryArithmeticValue : ArithmeticValue
{
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

    /// <summary>
    /// Constructs a new binary arithmetic value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="kind">The operation kind.</param>
    /// <param name="flags">The operation flags.</param>
    internal BinaryArithmeticValue(
        in PureValueInitializer initializer,
        Value left,
        Value right,
        BinaryArithmeticKind kind,
        ArithmeticFlags flags)
        : base(initializer, left.Type, flags)
    {
        bool isLeftPointer = left.Type is PointerType;
        bool isRightPointer = right.Type is PointerType;
        initializer.Assert(
            // Check whether the types are the same
            left.Type.Equals(right.Type) ||

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

    /// <summary>
    /// Returns the left operand.
    /// </summary>
    public Value Left => GetValue<Value>(0);

    /// <summary>
    /// Returns the right operand.
    /// </summary>
    public Value Right => GetValue<Value>(1);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateArithmetic(
            Location,
            rewriter.Rewrite(Left),
            rewriter.Rewrite(Right),
            Kind,
            Flags);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "arith.bin." + Kind.ToString();

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{Left.ToReferenceString()}, {Right.ToReferenceString()} [{Flags}]";
}

/// <summary>
/// Represents a binary arithmetic operation.
/// </summary>
sealed partial class TernaryArithmeticValue : ArithmeticValue
{
    /// <summary>
    /// Returns the left hand binary operation of a fused ternary operation.
    /// </summary>
    /// <param name="kind">The arithmetic kind.</param>
    /// <returns>The resolved binary operation.</returns>
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
    public static BinaryArithmeticKind GetRightBinaryKind(
        TernaryArithmeticKind kind) =>
        kind switch
        {
            TernaryArithmeticKind.MultiplyAdd => BinaryArithmeticKind.Add,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    /// <summary>
    /// Constructs a new ternary arithmetic value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="first">The first operand.</param>
    /// <param name="second">The second operand.</param>
    /// <param name="third">The third operand.</param>
    /// <param name="kind">The operation kind.</param>
    /// <param name="flags">The operation flags.</param>
    public TernaryArithmeticValue(
        in PureValueInitializer initializer,
        Value first,
        Value second,
        Value third,
        TernaryArithmeticKind kind,
        ArithmeticFlags flags)
        : base(initializer, first.Type, flags)
    {
        Debug.Assert(
            first.Type.Equals(second.Type) &&
            second.Type.Equals(third.Type), "Invalid types");

        Kind = kind;
        Seal(first, second, third);
    }

    /// <summary>
    /// Returns the first operand.
    /// </summary>
    public Value First => GetValue<Value>(0);

    /// <summary>
    /// Returns the second operand.
    /// </summary>
    public Value Second => GetValue<Value>(1);

    /// <summary>
    /// Returns the third operand.
    /// </summary>
    public Value Third => GetValue<Value>(2);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateArithmetic(
            Location,
            rewriter.Rewrite(First),
            rewriter.Rewrite(Second),
            rewriter.Rewrite(Third),
            Kind,
            Flags);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "arith.ter." + Kind.ToString();

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{First.ToReferenceString()}, {Second.ToReferenceString()}, " +
        $"{Third.ToReferenceString()} [{Flags}]";
}
