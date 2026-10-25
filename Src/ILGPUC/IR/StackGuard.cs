// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: StackGuard.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Roslyn.Analysis;
using ILGPUC.Util;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR;

/// <summary>
/// Guards the walks that recurse into the operands of a value, and therefore once
/// per nesting level of an expression: an expression nested too deeply for the
/// stack of the compiling thread is reported as
/// <see cref="KernelDiagnostic.UnsupportedConstructId"/> at its location instead
/// of overflowing the stack, which would terminate the process.
/// </summary>
static class StackGuard
{
    /// <summary>
    /// Ensures that the stack of the current thread can hold the next nesting
    /// level of the given value. Values without operands end an expression and
    /// always pass, so that the error points at an operation of the expression.
    /// </summary>
    /// <param name="value">The value whose operands are visited next.</param>
    /// <param name="method">The method containing the value.</param>
    /// <exception cref="KernelCompilationException">
    /// The expression is nested too deeply.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureSufficientStack(Value value, Method method)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack() &&
            value.Values.Length > 0)
        {
            throw CreateNestingException(value, method);
        }
    }

    /// <summary>
    /// Creates the error for an expression nested too deeply.
    /// </summary>
    /// <param name="value">The value nested too deeply.</param>
    /// <param name="method">The method containing the value.</param>
    /// <returns>The error to throw.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static KernelCompilationException CreateNestingException(
        Value value,
        Method method) =>
        new(
            KernelDiagnostic.UnsupportedConstructId,
            string.Format(
                ErrorMessages.KernelDiagnosticExpressionNesting,
                method.Source?.GetDisplayName() ?? method.Name),
            value.Location);
}
