// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Convert.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Values;
using ILGPUC.Util;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Determines atomic flags for internal operations.
    /// </summary>
    private static ConvertFlags DetermineConvertFlags(ref InvocationContext context)
    {
        var flags = ConvertFlags.None;
        if (context.HasUnsignedArguments)
            flags |= ConvertFlags.SourceUnsigned;
        if (context.HasUnsignedReturnType)
            flags |= ConvertFlags.TargetUnsigned;
        return flags;
    }

    /// <summary>
    /// Handles implicit convert operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Convert_ImplicitOperation(
        ref InvocationContext context) =>
        Convert_Operation(ref context);

    /// <summary>
    /// Handles explicit convert operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Convert_ExplicitOperation(
        ref InvocationContext context) =>
        Convert_Operation(ref context);

    /// <summary>
    /// Handles general convert operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Convert_Operation(
        ref InvocationContext context)
    {
        var flags = DetermineConvertFlags(ref context);
        var returnType = context.Method.GetReturnType();
        var typeNode = context.Builder.CreateType(returnType);
        return context.Builder.CreateConvert(
            context.Location,
            context.Pull(),
            typeNode,
            flags);
    }
}
