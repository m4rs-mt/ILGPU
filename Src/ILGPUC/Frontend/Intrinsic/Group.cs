// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Group.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPUC.IR;
using ILGPUC.IR.Values;
using System.Linq;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles group barrier operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_Barrier(ref InvocationContext context) =>
        context.Builder.CreateBarrier(context.Location, BarrierKind.GroupLevel);

    /// <summary>
    /// Handles group barrier pop-count operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_BarrierPopCount(ref InvocationContext context) =>
        context.Builder.CreateBarrier(
            context.Location,
            PredicateBarrierKind.GroupLevel,
            context.Pull(),
            PredicateBarrierPredicateKind.PopCount);

    /// <summary>
    /// Handles group barrier and operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_BarrierAnd(ref InvocationContext context) =>
        context.Builder.CreateBarrier(
            context.Location,
            PredicateBarrierKind.GroupLevel,
            context.Pull(),
            PredicateBarrierPredicateKind.And);

    /// <summary>
    /// Handles group barrier or operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_BarrierOr(ref InvocationContext context) =>
        context.Builder.CreateBarrier(
            context.Location,
            PredicateBarrierKind.GroupLevel,
            context.Pull(),
            PredicateBarrierPredicateKind.Or);

    /// <summary>
    /// Handles group broadcast operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_Broadcast(ref InvocationContext context)
    {
        var builder = context.Builder;
        var location = context.Location;

        var parameters = context.Method.GetParameters();
        Value sourceLane = parameters[0].ParameterType == typeof(FirstLaneValue<>)
            ? builder.CreatePrimitiveValue(location, 0)
            : builder.CreateArithmetic(
                location,
                builder.CreateGroupDimensionValue(location, DeviceConstantDimension3D.X),
                builder.CreatePrimitiveValue(location, 1),
                BinaryArithmeticKind.Sub);

        return builder.CreateBroadcast(
            location,
            context.Pull(),
            sourceLane,
            BroadcastKind.GroupLevel);
    }

    /// <summary>
    /// Handles group specialized broadcast operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_BroadcastFromThread(
        ref InvocationContext context) =>
        context.Builder.CreateBroadcast(
            context.Location,
            context.Pull(),
            context.Pull(),
            BroadcastKind.GroupLevel);

    /// <summary>
    /// Handles group dimension operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_Dimension(ref InvocationContext context) =>
        context.Builder.CreateGroupDimensionValue(
            context.Location,
            DeviceConstantDimension3D.X);

    /// <summary>
    /// Handles group index operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_Index(ref InvocationContext context) =>
        context.Builder.CreateGroupIndexValue(
            context.Location,
            DeviceConstantDimension3D.X);

    /// <summary>
    /// Handles group memory fence operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_MemoryFence(ref InvocationContext context) =>
        context.Builder.CreateMemoryBarrier(
            context.Location,
            MemoryBarrierKind.GroupLevel);

    /// <summary>
    /// Handles group local memory buffer operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_GetLocalMemoryBuffer(
        ref InvocationContext context)
    {
        var builder = context.Builder;

        // Create local allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var alloca = builder.CreateStaticAllocaArray(
            context.Location,
            context.Pull(),
            builder.CreateType(allocaType),
            IR.MemoryAddressSpace.Local);

        // Build view
        return builder.CreateNewView(
            context.Location,
            alloca,
            context.Pull());
    }

    /// <summary>
    /// Handles group shared memory element operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_GetSharedMemoryElement(
        ref InvocationContext context)
    {
        var builder = context.Builder;

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var alloca = builder.CreateAlloca(
            context.Location,
            builder.CreateType(allocaType),
            IR.MemoryAddressSpace.Shared);
        return alloca;
    }

    /// <summary>
    /// Handles group shared memory buffer operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_GetSharedMemoryBuffer(
        ref InvocationContext context)
    {
        var builder = context.Builder;

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var alloca = builder.CreateStaticAllocaArray(
            context.Location,
            context.Pull(),
            builder.CreateType(allocaType),
            IR.MemoryAddressSpace.Shared);

        // Build view
        return builder.CreateNewView(
            context.Location,
            alloca,
            context.Pull());
    }

    /// <summary>
    /// Handles group shared element buffer-per-thread operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_GetSharedMemoryElementPerThread(
        ref InvocationContext context)
    {
        var builder = context.Builder;

        // Create group dimension
        var groupDimension = builder.CreateGroupDimensionValue(
            context.Location,
            DeviceConstantDimension3D.X);

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var alloca = builder.CreateStaticAllocaArray(
            context.Location,
            groupDimension,
            builder.CreateType(allocaType),
            IR.MemoryAddressSpace.Shared);

        // Build view
        return builder.CreateNewView(
            context.Location,
            alloca,
            groupDimension);
    }

    /// <summary>
    /// Handles group shared element buffer-per-thread operations receiving a multiplier.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_GetSharedMemoryElementPerThreadMultiplier(
        ref InvocationContext context)
    {
        var builder = context.Builder;

        // Create group dimension
        var groupDimension = builder.CreateGroupDimensionValue(
            context.Location,
            DeviceConstantDimension3D.X);

        // Get multiplier and apply it
        var multiplier = context.Pull();
        var dimension = builder.CreateArithmetic(
            context.Location,
            groupDimension,
            multiplier,
            BinaryArithmeticKind.Mul);

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var alloca = builder.CreateStaticAllocaArray(
            context.Location,
            dimension,
            builder.CreateType(allocaType),
            IR.MemoryAddressSpace.Shared);

        // Build view
        return builder.CreateNewView(
            context.Location,
            alloca,
            dimension);
    }

    /// <summary>
    /// Handles group shared element buffer-per-thread operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Group_GetSharedMemoryElementPerWarp(
        ref InvocationContext context)
    {
        var builder = context.Builder;

        // Create group dimension
        var warpDimension = builder.CreateWarpSizeValue(context.Location);

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var alloca = builder.CreateStaticAllocaArray(
            context.Location,
            warpDimension,
            builder.CreateType(allocaType),
            IR.MemoryAddressSpace.Shared);

        // Build view
        return builder.CreateNewView(
            context.Location,
            alloca,
            warpDimension);
    }
}
