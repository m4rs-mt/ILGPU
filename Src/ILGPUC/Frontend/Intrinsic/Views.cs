// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Views.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPUC.IR.Values;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles view alignment operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Views_AlignTo(ref InvocationContext context) =>
        context.Builder.CreateAlignTo(
            context.Location,
            context.PullInstance(),
            context.Pull());

    /// <summary>
    /// Handles view alignment reinterpret operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Views_AsAlignedInternal(
        ref InvocationContext context) =>
        context.Builder.CreateAsAligned(
            context.Location,
            context.PullInstance(),
            context.Pull());

    /// <summary>
    /// Handles view cast operations
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Views_Cast(ref InvocationContext context)
    {
        var targetElementType = context.TypeContext.CreateType(
            context.GetMethodGenericArguments()[0]);
        return context.Builder.CreateViewCast(
            context.Location,
            context.PullInstance(),
            targetElementType);
    }

    /// <summary>
    /// Handles view extent operations
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Views_Extent(ref InvocationContext context)
    {
        var structureBuilder = context.Builder.CreateDynamicStructure(
            context.Location,
            1);
        structureBuilder.Add(Views_Length(ref context));
        return structureBuilder.Seal();
    }

    /// <summary>
    /// Handles view int extent operations
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Views_IntExtent(ref InvocationContext context)
    {
        var structureBuilder = context.Builder.CreateDynamicStructure(
            context.Location,
            1);
        structureBuilder.Add(Views_IntLength(ref context));
        return structureBuilder.Seal();
    }

    /// <summary>
    /// Handles view length operations
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Views_Length(ref InvocationContext context) =>
        context.Builder.CreateGetViewLongLength(
            context.Location,
            context.PullInstance());

    /// <summary>
    /// Handles view length in bytes operations
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Views_LengthInBytes(ref InvocationContext context)
    {
        var builder = context.Builder;
        var viewElementType = context.TypeContext.CreateType(
            context.GetTypeGenericArguments()[0]);
        return builder.CreateArithmetic(
            context.Location,
            Views_Length(ref context),
            builder.CreateSizeOf(context.Location, viewElementType),
            BinaryArithmeticKind.Mul,
            ArithmeticFlags.Unsigned);
    }

    /// <summary>
    /// Handles view int length operations
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Views_IntLength(ref InvocationContext context) =>
        context.Builder.CreateGetViewLength(
            context.Location,
            context.PullInstance());

    /// <summary>
    /// Handles view is valid operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Views_IsValid(ref InvocationContext context)
    {
        var builder = context.Builder;
        return builder.CreateCompare(
            context.Location,
            Views_Length(ref context),
            builder.CreatePrimitiveValue(context.Location, 0),
            CompareKind.GreaterThan);
    }

    /// <summary>
    /// Handles view stride operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Views_Stride(ref InvocationContext context) =>
        context.Builder.CreateDynamicStructure(context.Location, 0).Seal();

    /// <summary>
    /// Handles view sub-view operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Views_SubView(ref InvocationContext context) =>
        context.Builder.CreateSubViewValue(
            context.Location,
            context.PullInstance(),
            context.Pull(),
            context.Pull());

    /// <summary>
    /// Handles view element operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Views_Item(ref InvocationContext context)
    {
        var builder = context.Builder;
        var instance = context.PullInstance();
        var indexValue = context.Pull();

        var paramType = context.Method.GetParameters()[0].ParameterType;
        if (paramType == typeof(Index1D) || paramType == typeof(LongIndex1D))
        {
            indexValue = builder.CreateGetField(
                context.Location,
                indexValue,
                new FieldAccess(0));
        }

        return builder.CreateLoadElementAddress(
            context.Location,
            instance,
            indexValue);
    }
}
