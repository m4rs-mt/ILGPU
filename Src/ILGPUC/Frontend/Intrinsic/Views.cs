// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Views.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.PureValues;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles view alignment operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Views_AlignTo(ref InvocationContext context) =>
        context.Builder.CreateAlignTo(
            context.Location,
            context.PullInstance(),
            context.Pull());

    /// <summary>
    /// Handles view alignment reinterpret operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Views_AsAlignedInternal(
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
    private static Value? Views_Cast(ref InvocationContext context)
    {
        var targetElementType = context.ModuleBuilder.CreateType(
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
    private static Value? Views_Extent(ref InvocationContext context)
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
    private static Value? Views_IntExtent(ref InvocationContext context)
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
    private static Value? Views_Length(ref InvocationContext context) =>
        context.Builder.CreateGetViewLongLength(
            context.Location,
            context.PullInstance());

    /// <summary>
    /// Handles view length in bytes operations
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Views_LengthInBytes(ref InvocationContext context)
    {
        var builder = context.Builder;
        var viewElementType = context.ModuleBuilder.CreateType(
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
    private static Value? Views_IntLength(ref InvocationContext context) =>
        context.Builder.CreateGetViewLength(
            context.Location,
            context.PullInstance());

    /// <summary>
    /// Handles view is valid operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Views_IsValid(ref InvocationContext context)
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
    private static Value? Views_Stride(ref InvocationContext context) =>
        context.Builder.CreateDynamicStructure(context.Location, 0).Seal();

    /// <summary>
    /// Handles view sub-view operations.
    /// Supports both <c>SubView(offset, length)</c> (3 args including
    /// instance) and <c>SubView(offset)</c> (2 args — length is computed
    /// as <c>view.Length - offset</c>).
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Views_SubView(ref InvocationContext context)
    {
        var builder = context.Builder;
        var view = context.PullInstance();
        var offset = context.Pull();

        Value length;
        if (context.NumArguments >= 3)
        {
            // SubView(offset, length)
            length = context.Pull();
        }
        else
        {
            // SubView(offset) — length = view.Length - offset
            var viewLength = builder.CreateGetViewLongLength(
                context.Location, view);
            length = builder.CreateArithmetic(
                context.Location,
                viewLength,
                offset,
                BinaryArithmeticKind.Sub,
                ArithmeticFlags.Unsigned).AsNotNull();
        }

        return builder.CreateSubView(
            context.Location, view, offset, length);
    }

    /// <summary>
    /// Handles view element operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Views_Item(ref InvocationContext context)
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
