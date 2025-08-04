// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Debug.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Values;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles debug assert operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Debug_Assert(ref InvocationContext context)
    {
        var builder = context.Builder;
        var location = context.Location;

        var message = context.NumArguments == 1
            ? builder.CreatePrimitiveValue(location, "Assert failed")
            : (ValueReference)context.Pull();

        return builder.CreateDebugAssert(
            location,
            context.Pull(),
            message);
    }

    /// <summary>
    /// Handles debug fail operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Debug_Fail(ref InvocationContext context)
    {
        var builder = context.Builder;
        var location = context.Location;

        return builder.CreateDebugAssert(
            location,
            builder.CreatePrimitiveValue(location, false),
            context.Pull());
    }
}

