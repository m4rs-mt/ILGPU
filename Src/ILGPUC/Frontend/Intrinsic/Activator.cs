// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Activator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPUC.IR;
using ILGPUC.IR.Values;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles activator operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Activator_CreateInstance(ref InvocationContext context)
    {
        var location = context.Location;

        var args = context.GetMethodGenericArguments();
        if (context.NumArguments != 0 || args.Length != 1 || !args[0].IsValueType)
        {
            throw context.Location.GetNotSupportedException(
                ErrorMessages.NotSupportedActivatorOperation,
                context.Method.Name);
        }

        return context.Builder.CreateNull(
            location,
            context.Builder.CreateType(args[0]));
    }
}
