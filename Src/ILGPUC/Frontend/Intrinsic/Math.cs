// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Math.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Values;

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
    public static ValueReference Math_UnaryOperation(
        ref InvocationContext context,
        UnaryArithmeticKind kind) =>
        context.Builder.CreateArithmetic(
            context.Location,
            context.Pull(),
            kind,
            DetermineArithmeticFlags(ref context));

    /// <summary>
    /// Handles binary math operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="kind">Operation kind to be used.</param>
    /// <returns>The resulting value.</returns>
    public static ValueReference Math_BinaryOperation(
        ref InvocationContext context,
        BinaryArithmeticKind kind) =>
        context.Builder.CreateArithmetic(
            context.Location,
            context.Pull(),
            context.Pull(),
            kind,
            DetermineArithmeticFlags(ref context));

    /// <summary>
    /// Handles ternary math operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="kind">Operation kind to be used.</param>
    /// <returns>The resulting value.</returns>
    public static ValueReference Math_TernaryOperation(
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
