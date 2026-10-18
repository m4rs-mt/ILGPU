// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Math.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;
using System;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Determines arithmetic flags for internal operations.
    /// </summary>
    private static ArithmeticFlags DetermineArithmeticFlags(
        ref InvocationContext context) =>
        context.HasUnsignedArguments
        ? ArithmeticFlags.Unsigned
        : ArithmeticFlags.None;

    /// <summary>
    /// Handles unary math operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="kind">Operation kind to be used.</param>
    /// <returns>The resulting value.</returns>
    public static Value? Math_UnaryOperation(
        ref InvocationContext context,
        UnaryArithmeticKind kind) =>
        context.Builder.CreateArithmetic(
            context.Location,
            context.Pull(),
            kind,
            DetermineArithmeticFlags(ref context)).AsNotNull();

    /// <summary>
    /// Handles unary plus operations, which return their operand unchanged.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The operand.</returns>
    public static Value? Math_Identity(ref InvocationContext context) =>
        context.Pull();

    /// <summary>
    /// Handles <see cref="Half"/> increment operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The incremented value.</returns>
    public static Value? Math_Increment(ref InvocationContext context) =>
        context.Builder.CreateArithmetic(
            context.Location,
            context.Pull(),
            context.Builder.CreatePrimitiveValue(context.Location, Half.One),
            BinaryArithmeticKind.Add,
            ArithmeticFlags.None).AsNotNull();

    /// <summary>
    /// Handles <see cref="Half"/> decrement operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The decremented value.</returns>
    public static Value? Math_Decrement(ref InvocationContext context) =>
        context.Builder.CreateArithmetic(
            context.Location,
            context.Pull(),
            context.Builder.CreatePrimitiveValue(context.Location, Half.One),
            BinaryArithmeticKind.Sub,
            ArithmeticFlags.None).AsNotNull();

    /// <summary>
    /// Handles binary math operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="kind">Operation kind to be used.</param>
    /// <returns>The resulting value.</returns>
    public static Value? Math_BinaryOperation(
        ref InvocationContext context,
        BinaryArithmeticKind kind) =>
        context.Builder.CreateArithmetic(
            context.Location,
            context.Pull(),
            context.Pull(),
            kind,
            DetermineArithmeticFlags(ref context)).AsNotNull();

    /// <summary>
    /// Handles copysign operations. Integers are negated if the signs of both
    /// operands differ, which keeps the minimum value as its negation wraps around.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    public static Value? Math_CopySign(ref InvocationContext context)
    {
        if (context[0].BasicValueType.IsFloat())
            return Math_BinaryOperation(ref context, BinaryArithmeticKind.CopySign);

        // The sign bit of value ^ sign is set if the signs of both operands differ
        var location = context.Location;
        var builder = context.Builder;
        var value = context.Pull();
        var signs = builder.CreateArithmetic(
            location,
            value,
            context.Pull(),
            BinaryArithmeticKind.Xor);
        var differ = builder.CreateCompare(
            location,
            signs,
            builder.CreatePrimitiveValue(location, value.BasicValueType, 0),
            CompareKind.LessThan);
        return builder.CreatePredicate(
            location,
            differ,
            builder.CreateArithmetic(location, value, UnaryArithmeticKind.Neg),
            value);
    }

    /// <summary>
    /// Handles ternary math operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="kind">Operation kind to be used.</param>
    /// <returns>The resulting value.</returns>
    public static Value? Math_TernaryOperation(
        ref InvocationContext context,
        TernaryArithmeticKind kind) =>
        context.Builder.CreateArithmetic(
            context.Location,
            context.Pull(),
            context.Pull(),
            context.Pull(),
            kind,
            DetermineArithmeticFlags(ref context));
}
