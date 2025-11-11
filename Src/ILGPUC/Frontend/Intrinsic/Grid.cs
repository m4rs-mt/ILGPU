// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Grid.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles grid dimension operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Grid_CurrentDimension(
        ref InvocationContext context) =>
        context.Builder.CreateGridDimensionValue(
            context.Location);

    /// <summary>
    /// Handles grid dimension operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Grid_CurrentIndex(ref InvocationContext context) =>
        context.Builder.CreateGridIndexValue(
            context.Location);

    /// <summary>
    /// Handles grid dimension operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Grid_MemoryFence(ref InvocationContext context) =>
        context.Builder.CreateMemoryBarrier(
            context.Location,
            MemoryBarrierKind.DeviceLevel);

    /// <summary>
    /// Handles grid dimension operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Grid_SystemMemoryFence(ref InvocationContext context) =>
        context.Builder.CreateMemoryBarrier(
            context.Location,
            MemoryBarrierKind.SystemLevel);
}
