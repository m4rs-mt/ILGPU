// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2026 ILGPU Project
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
using ILGPUC.IR.PureValues;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Creates a new nD array instance.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ArrayValue Arrays_CreateNew(ref InvocationContext context)
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
        var elementType = context.ModuleBuilder.CreateType(managedElementType);
        var arrayType = context.ModuleBuilder.CreateArrayType(elementType, dimension);

        // Create array instance
        var arrayBuilder = builder.CreateNewArray(location, arrayType);
        for (int i = 0; i < dimension; ++i)
            arrayBuilder.Add(context[i + 1]);
        var newArray = arrayBuilder.Seal();
        context.CodeGenerator.ReinitializeArrayInLoop(newArray);

        // Store instance
        builder.CreateStore(location, context[0], newArray);
        return newArray;
    }

    /// <summary>
    /// Returns the empty 1D array of the element type, whose back buffer all
    /// calls with the same element type share, as <c>Array.Empty</c> returns the
    /// same array in .NET.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Arrays_Empty(ref InvocationContext context)
    {
        var location = context.Location;
        var builder = context.Builder;

        var managedElementType = context.GetMethodGenericArguments()[0];
        var elementType = context.ModuleBuilder.CreateType(managedElementType);
        var arrayType = context.ModuleBuilder.CreateArrayType(elementType, 1);
        return builder.CreateEmptyArray(
            location,
            arrayType,
            context.ModuleBuilder.GetEmptyArrayBuffer(
                location,
                managedElementType,
                elementType));
    }

    /// <summary>
    /// Gets an array element.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Arrays_GetElement(ref InvocationContext context)
    {
        var location = context.Location;
        var builder = context.Builder;

        var laeaBuilder = builder.CreateLoadArrayElementAddress(
            location,
            context[0]);
        for (int i = 1, e = context.NumArguments; i < e; ++i)
            laeaBuilder.Add(context[i]);
        return builder.CreateLoad(location, laeaBuilder.Seal());
    }

    /// <summary>
    /// Sets an array element.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Arrays_SetElement(ref InvocationContext context)
    {
        var location = context.Location;
        var builder = context.Builder;

        var laeaBuilder = builder.CreateLoadArrayElementAddress(
            location,
            context[0]);
        for (int i = 1, e = context.NumArguments - 1; i < e; ++i)
            laeaBuilder.Add(context[i]);
        return builder.CreateStore(
            location,
            laeaBuilder.Seal(),
            context[context.NumArguments - 1]);
    }

    /// <summary>
    /// Gets the address of an array element without loading.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Arrays_GetAddress(ref InvocationContext context)
    {
        var location = context.Location;
        var builder = context.Builder;

        var laeaBuilder = builder.CreateLoadArrayElementAddress(
            location,
            context[0]);
        for (int i = 1, e = context.NumArguments; i < e; ++i)
            laeaBuilder.Add(context[i]);
        return laeaBuilder.Seal();
    }

    /// <summary>
    /// Gets an array lower bound.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Arrays_GetLowerBound(
        ref InvocationContext context) =>
        context.Builder.CreatePrimitiveValue(
            context.Location,
            0);

    /// <summary>
    /// Gets an array upper bound.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Arrays_GetUpperBound(ref InvocationContext context) =>
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
    private static Value? Arrays_LengthGet(ref InvocationContext context) =>
        context.Builder.CreateGetArrayLength(
            context.Location,
            context.Pull());

    /// <summary>
    /// Gets a long array length.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Arrays_LongLengthGet(ref InvocationContext context) =>
        context.Builder.CreateConvertToInt64(
            context.Location,
            Arrays_LengthGet(ref context));

    /// <summary>
    /// Gets an array length.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Arrays_Length(ref InvocationContext context) =>
        context.Builder.CreateGetArrayLength(
            context.Location,
            context.Pull(),
            context.Pull());

    /// <summary>
    /// Gets an array long length.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static Value? Arrays_LongLength(ref InvocationContext context) =>
        context.Builder.CreateConvertToInt64(
            context.Location,
            Arrays_Length(ref context));
}
