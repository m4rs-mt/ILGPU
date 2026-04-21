// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Atomic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;

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
    private static Value? Atomic_Operation(
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
    private static Value? Atomic_CompareExchange(ref InvocationContext context)
    {
        var flags = DetermineAtomicFlags(ref context);
        return context.Builder.CreateAtomicCAS(
            context.Location,
            context.Pull(),
            context.Pull(),
            context.Pull(),
            flags);
    }

    /// <summary>
    /// Handles <see cref="ILGPU.Atomic.MakeAtomic{T}"/>. Resolves the
    /// user's binary operation delegate to an IR method and constructs a
    /// <see cref="IR.BasicBlockValues.CustomAtomic"/> node. The user's
    /// <c>CompareExchangeOperation&lt;T&gt;</c> delegate is consumed but
    /// unused — the lowering pass always emits <see cref="IR.BasicBlockValues.AtomicCAS"/>
    /// which every backend handles natively.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Atomic_MakeAtomic(ref InvocationContext context)
    {
        var flags = DetermineAtomicFlags(ref context);
        var target = context.Pull();                  // ref T target
        var value = context.Pull();                   // T value
        var operation = context.PullDelegateMethod(); // MakeAtomicOperation<T>
        _ = context.Pull();                           // CompareExchangeOperation<T> — unused
        return context.Builder.CreateCustomAtomic(
            context.Location, target, value, (Value?)operation, flags);
    }
}
