// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Atomic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.Values;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Determines atomic flags for internal operations.
    /// </summary>
    private static AtomicFlags DetermineAtomicFlags(ref InvocationContext context) =>
        context.HasUnsignedArguments
        ? AtomicFlags.Unsigned
        : AtomicFlags.None;

    /// <summary>
    /// Handles atomic operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <param name="kind">Atomic kind to be used.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Atomic_Operation(
        ref InvocationContext context,
        GenericAtomicKind kind)
    {
        var flags = DetermineAtomicFlags(ref context);
        return context.Builder.CreateAtomic(
            context.Location,
            context.Pull(),
            context.Pull(),
            kind,
            flags);
    }

    /// <summary>
    /// Handles atomic ands.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Atomic_CompareExchange(ref InvocationContext context)
    {
        var flags = DetermineAtomicFlags(ref context);
        return context.Builder.CreateAtomicCAS(
            context.Location,
            context.Pull(),
            context.Pull(),
            context.Pull(),
            flags);
    }
}
