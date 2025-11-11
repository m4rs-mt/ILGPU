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
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.PureValues;
using System.Linq;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles group barrier operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_Barrier(ref InvocationContext context) =>
        context.Builder.CreateBarrier(context.Location, BarrierKind.GroupLevel);

    /// <summary>
    /// Handles group barrier pop-count operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_BarrierPopCount(ref InvocationContext context) =>
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
    private static Value? Group_BarrierAnd(ref InvocationContext context) =>
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
    private static Value? Group_BarrierOr(ref InvocationContext context) =>
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
    private static Value? Group_Broadcast(ref InvocationContext context)
    {
        var builder = context.Builder;
        var location = context.Location;

        var parameters = context.Method.GetParameters();
        var sourceLane = parameters[0].ParameterType == typeof(FirstLaneValue<>)
            ? builder.CreatePrimitiveValue(location, 0)
            : builder.CreateArithmetic(
                location,
                builder.CreateGroupDimensionValue(location),
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
    private static Value? Group_BroadcastFromThread(ref InvocationContext context) =>
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
    private static Value? Group_Dimension(ref InvocationContext context) =>
        context.Builder.CreateGroupDimensionValue(context.Location);

    /// <summary>
    /// Handles group index operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_Index(ref InvocationContext context) =>
        context.Builder.CreateGroupIndexValue(context.Location);

    /// <summary>
    /// Handles group memory fence operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_MemoryFence(ref InvocationContext context) =>
        context.Builder.CreateMemoryBarrier(
            context.Location,
            MemoryBarrierKind.GroupLevel);

    /// <summary>
    /// Handles group local memory buffer operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_GetLocalMemoryBuffer(ref InvocationContext context)
    {
        var builder = context.ModuleBuilder;

        // Create local allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var length = context.Pull();
        var alloca = builder.CreateGlobal(
            context.Location,
            builder.CreateType(allocaType),
            MemoryAddressSpace.Local,
            length);

        // Build view using the same length
        return context.Builder.CreateNewView(
            context.Location,
            alloca,
            length);
    }

    /// <summary>
    /// Handles group shared memory element operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_GetSharedMemoryElement(ref InvocationContext context)
    {
        var builder = context.ModuleBuilder;

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        return builder.CreateGlobal(
            context.Location,
            context.ModuleBuilder.CreateType(allocaType),
            MemoryAddressSpace.Shared);
    }

    /// <summary>
    /// Handles group shared memory buffer operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_GetSharedMemoryBuffer(ref InvocationContext context)
    {
        var builder = context.ModuleBuilder;

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var length = context.Pull();
        var alloca = builder.CreateGlobal(
            context.Location,
            builder.CreateType(allocaType),
            MemoryAddressSpace.Shared,
            length);

        // Build view using the same length
        return context.Builder.CreateNewView(
            context.Location,
            alloca,
            length);
    }

    /// <summary>
    /// Handles group shared element buffer-per-thread operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Group_GetSharedMemoryElementPerThread(
        ref InvocationContext context)
    {
        var builder = context.Builder;

        // Create group dimension
        var groupDimension = builder.CreateGroupDimensionValue(context.Location);

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var alloca = context.ModuleBuilder.CreateGlobal(
            context.Location,
            context.ModuleBuilder.CreateType(allocaType),
            MemoryAddressSpace.Shared,
            groupDimension);

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
    private static Value? Group_GetSharedMemoryElementPerThreadMultiplier(
        ref InvocationContext context)
    {
        var builder = context.Builder;

        // Create group dimension
        var groupDimension = builder.CreateGroupDimensionValue(context.Location);

        // Get multiplier and apply it
        var multiplier = context.Pull();
        var dimension = builder.CreateArithmetic(
            context.Location,
            groupDimension,
            multiplier,
            BinaryArithmeticKind.Mul);

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var alloca = context.ModuleBuilder.CreateGlobal(
            context.Location,
            context.ModuleBuilder.CreateType(allocaType),
            MemoryAddressSpace.Shared,
            dimension);

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
    private static Value? Group_GetSharedMemoryElementPerWarp(
        ref InvocationContext context)
    {
        var builder = context.Builder;

        // Create group dimension
        var warpDimension = builder.CreateSubGroupDimensionValue(context.Location);

        // Create shared allocation
        var allocaType = context.GetMethodGenericArguments().First();
        var alloca = context.ModuleBuilder.CreateGlobal(
            context.Location,
            context.ModuleBuilder.CreateType(allocaType),
            MemoryAddressSpace.Shared,
            warpDimension);

        // Build view
        return builder.CreateNewView(
            context.Location,
            alloca,
            warpDimension);
    }

    /// <summary>
    /// Handles group reduce/all-reduce operations with lambda binary operations.
    /// Always emits a custom <see cref="GroupReduce"/>; operation recognition
    /// happens later in <see cref="IR.Transformations.RecognizeCollectiveOps"/>.
    /// </summary>
    private static Value? Group_Reduce(
        ref InvocationContext context,
        GroupReduceKind kind = GroupReduceKind.Reduce)
    {
        var data = context.Pull();
        var identity = context.Pull();
        var methodBase = context.PullDelegateMethodBase();
        var method = methodBase is not null
            ? context.CodeGenerator.GetMethod(methodBase)
            : null;
        return context.Builder.CreateGroupReduce(
            context.Location, data, (Value?)method, kind, identity);
    }

    /// <summary>
    /// Handles group scan operations with lambda binary operations.
    /// Always emits a custom <see cref="GroupScan"/>; operation recognition
    /// happens later in <see cref="IR.Transformations.RecognizeCollectiveOps"/>.
    /// </summary>
    private static Value? Group_Scan(
        ref InvocationContext context,
        GroupScanKind kind = GroupScanKind.Inclusive)
    {
        var data = context.Pull();
        var identity = context.Pull();
        var methodBase = context.PullDelegateMethodBase();
        var method = methodBase is not null
            ? context.CodeGenerator.GetMethod(methodBase)
            : null;
        return context.Builder.CreateGroupScan(
            context.Location,
            data,
            (Value?)method,
            kind,
            identity);
    }

    /// <summary>
    /// Handles group radix sort operations.
    /// Resolves IRadixSortOperation static abstract members via reflection
    /// at frontend time, since the concrete operation type is fully known.
    /// </summary>
    private static Value? Group_RadixSort(ref InvocationContext context)
    {
        var data = context.Pull();

        // Get generic type arguments: [T, TRadixSortOperation]
        var operationType = context.GetMethodGenericArguments()[1];

        // Resolve NumBits (static abstract int property)
        var numBits = (int)operationType
            .GetProperty(
                "NumBits",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;

        // Resolve DefaultValue (static abstract T property)
        var defaultValueObj = operationType
            .GetProperty(
                "DefaultValue",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        var defaultValue = context.Builder.CreatePrimitiveValue(
            context.Location,
            defaultValueObj);

        // Resolve ExtractRadixBits method
        var extractMethodInfo = operationType.GetMethod(
            "ExtractRadixBits",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Static)!;
        var extractMethod = context.CodeGenerator.GetMethod(extractMethodInfo);

        return context.Builder.CreateGroupRadixSort(
            context.Location,
            data,
            numBits,
            defaultValue,
            extractMethod);
    }
}
