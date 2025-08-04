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

using ILGPUC.IR.Values;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles grid dimension operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Grid_CurrentDimension(
        ref InvocationContext context) =>
        context.Builder.CreateGridDimensionValue(
            context.Location,
            DeviceConstantDimension3D.X);

    /// <summary>
    /// Handles grid dimension operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Grid_CurrentIndex(ref InvocationContext context) =>
        context.Builder.CreateGridIndexValue(
            context.Location,
            DeviceConstantDimension3D.X);

    /// <summary>
    /// Handles grid dimension operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Grid_MemoryFence(ref InvocationContext context) =>
        context.Builder.CreateMemoryBarrier(
            context.Location,
            MemoryBarrierKind.DeviceLevel);

    /// <summary>
    /// Handles grid dimension operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Grid_SystemMemoryFence(ref InvocationContext context) =>
        context.Builder.CreateMemoryBarrier(
            context.Location,
            MemoryBarrierKind.SystemLevel);
}
