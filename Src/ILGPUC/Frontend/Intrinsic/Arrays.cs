// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Arrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.Values;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Creates a new nD array instance.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Arrays_CreateNew(ref InvocationContext context)
    {
        var location = context.Location;
        var builder = context.Builder;

        int dimension = context.NumArguments - 1;
        if (dimension < 1)
        {
            throw context.Location.GetNotSupportedException(
                ErrorMessages.NotSupportedArrayDimension,
                dimension.ToString());
        }

        // Create an array view type of the appropriate dimension
        var managedElementType =
            context.Method.DeclaringType.AsNotNull().GetElementType().AsNotNull();
        var elementType = builder.CreateType(managedElementType);
        var arrayType = builder.CreateArrayType(elementType, dimension);

        // Create array instance
        var arrayBuilder = builder.CreateNewArray(location, arrayType);
        for (int i = 0; i < dimension; ++i)
            arrayBuilder.Add(context.Arguments[i + 1]);
        var newArray = arrayBuilder.Seal();

        // Store instance
        builder.CreateStore(location, context[0], newArray);
        return default;
    }

    /// <summary>
    /// Creates a new empty 1D array instance.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Arrays_Empty(ref InvocationContext context)
    {
        var location = context.Location;
        var builder = context.Builder;

        var elementType = builder.CreateType(context.GetMethodGenericArguments()[0]);
        var arrayType = builder.CreateArrayType(elementType, 1);
        return builder.CreateEmptyArray(location, arrayType);
    }

    /// <summary>
    /// Gets an array element.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Arrays_GetElement(ref InvocationContext context)
    {
        var location = context.Location;
        var builder = context.Builder;

        var laeaBuilder = builder.CreateLoadArrayElementAddress(
            location,
            context.Arguments[0]);
        for (int i = 1, e = context.NumArguments; i < e; ++i)
            laeaBuilder.Add(context.Arguments[i]);
        return builder.CreateLoad(location, laeaBuilder.Seal());
    }

    /// <summary>
    /// Sets an array element.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Arrays_SetElement(ref InvocationContext context)
    {
        var location = context.Location;
        var builder = context.Builder;

        var laeaBuilder = builder.CreateLoadArrayElementAddress(
            location,
            context.Arguments[0]);
        for (int i = 1, e = context.NumArguments - 1; i < e; ++i)
            laeaBuilder.Add(context.Arguments[i]);
        return builder.CreateStore(
            location,
            laeaBuilder.Seal(),
            context[context.NumArguments - 1]);
    }

    /// <summary>
    /// Gets an array lower bound.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Arrays_GetLowerBound(
        ref InvocationContext context) =>
        context.Builder.CreatePrimitiveValue(
            context.Location,
            0);

    /// <summary>
    /// Gets an array upper bound.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Arrays_GetUpperBound(ref InvocationContext context) =>
        context.Builder.CreateArithmetic(
            context.Location,
            Arrays_Length(ref context),
            context.Builder.CreatePrimitiveValue(
                context.Location,
                1),
            BinaryArithmeticKind.Sub);

    /// <summary>
    /// Gets an array length.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Arrays_LengthGet(ref InvocationContext context) =>
        context.Builder.CreateGetArrayLength(
            context.Location,
            context.Pull());


    /// <summary>
    /// Gets a long array length.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Arrays_LongLengthGet(ref InvocationContext context) =>
        context.Builder.CreateConvertToInt64(
            context.Location,
            Arrays_LengthGet(ref context));

    /// <summary>
    /// Gets an array length.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Arrays_Length(ref InvocationContext context) =>
        context.Builder.CreateGetArrayLength(
            context.Location,
            context.Pull(),
            context.Pull());

    /// <summary>
    /// Gets an array long length.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Arrays_LongLength(ref InvocationContext context) =>
        context.Builder.CreateConvertToInt64(
            context.Location,
            Arrays_Length(ref context));
}
