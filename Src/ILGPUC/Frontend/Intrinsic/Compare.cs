// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Compare.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Values;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Determines compare flags for internal operations.
    /// </summary>
    private static CompareFlags DetermineCompareFlags(
        ref InvocationContext context) =>
        context.HasUnsignedArguments
        ? CompareFlags.UnsignedOrUnordered
        : CompareFlags.None;

    /// <summary>
    /// Handles compare operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="kind">Compare kind to be used.</param>
    /// <param name="flags">Compare flags to be used.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Compare_Operation(
        ref InvocationContext context,
        CompareKind kind,
        CompareFlags flags) =>
        context.Builder.CreateCompare(
            context.Location,
            context.Pull(),
            context.Pull(),
            kind,
            flags);

    /// <summary>
    /// Handles compare operations using a dynamic compare flags resolver.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="kind">Compare kind to be used.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Compare_OperationDynamic(
        ref InvocationContext context,
        CompareKind kind) =>
        Compare_Operation(
            ref context,
            kind,
            DetermineCompareFlags(ref context));
}
