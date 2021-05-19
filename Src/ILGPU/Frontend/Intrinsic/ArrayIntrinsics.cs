// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ArrayIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    partial class Intrinsics
    {
        #region Managed Arrays

        /// <summary>
        /// Determines whether the given type is an intrinsic array type.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>True, if the given type is an intrinsic array type.</returns>
        internal static bool IsIntrinsicArrayType(Type type) =>
            type == typeof(Array) || type.IsArray;

        /// <summary>
        /// Handles array operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleArrays(ref InvocationContext context)
        {
            var builder = context.Builder;
            var location = context.Location;
            return context.Method.Name switch
            {
                ".ctor" => CreateNewArray(ref context),
                "Get" => CreateGetArrayElement(ref context),
                "Set" => CreateSetArrayElement(ref context),
                "get_Length" => builder.CreateGetArrayLength(
                    location,
                    context[0]),
                "get_LongLength" => builder.CreateGetArrayLongLength(
                    location,
                    context[0]),
                nameof(Array.GetLowerBound) => CreateGetArrayLowerBound(ref context),
                nameof(Array.GetUpperBound) => CreateGetArrayUpperBound(ref context),
                nameof(Array.GetLength) => CreateGetArrayLength(ref context),
                nameof(Array.GetLongLength) => CreateGetArrayLongLength(ref context),
                nameof(Array.Empty) => CreateEmptyArray(ref context),
                _ => throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportedIntrinsic,
                    context.Method.Name),
            };
        }

        /// <summary>
        /// Creates a new nD array instance.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference CreateNewArray(ref InvocationContext context)
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
            var managedElementType = context.Method.DeclaringType.GetElementType();
            var elementType = builder.CreateType(
                managedElementType,
                MemoryAddressSpace.Generic);
            var dimensionLengths = context.Arguments.Slice(1, dimension);

            // Create and store array instance
            var newArray = builder.CreateNewArray(
                location,
                elementType,
                ref dimensionLengths);

            // Clear array data
            var callArguments = InlineList<ValueReference>.Create(
                builder.CreateGetViewFromArray(
                    location,
                    newArray));
            builder.CreateCall(
                location,
                context.DeclareMethod(
                    LocalMemory.GetClearMethod(managedElementType)),
                ref callArguments);

            // Store instance
            builder.CreateStore(location, context[0], newArray);
            return default;
        }

        /// <summary>
        /// Creates a new empty 1D array instance.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static Value CreateEmptyArray(ref InvocationContext context)
        {
            var location = context.Location;
            var builder = context.Builder;

            var elementType = builder.CreateType(context.GetMethodGenericArguments()[0]);
            return builder.CreateEmptyArray(location, elementType, 1);
        }

        /// <summary>
        /// Gets an array element.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static Value CreateGetArrayElement(ref InvocationContext context)
        {
            var location = context.Location;
            var builder = context.Builder;

            var arguments = context.Arguments.Slice(1, context.NumArguments - 1);
            return builder.CreateLoad(
                location,
                builder.CreateGetArrayElementAddress(
                    location,
                    context[0],
                    ref arguments));
        }

        /// <summary>
        /// Sets an array element.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static Value CreateSetArrayElement(ref InvocationContext context)
        {
            var location = context.Location;
            var builder = context.Builder;

            var arguments = context.Arguments.Slice(1, context.NumArguments - 2);
            return builder.CreateStore(
                location,
                builder.CreateGetArrayElementAddress(
                    location,
                    context[0],
                    ref arguments),
                context[context.NumArguments - 1]);
        }

        /// <summary>
        /// Gets an array lower bound.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static Value CreateGetArrayLowerBound(ref InvocationContext context) =>
            context.Builder.CreatePrimitiveValue(
                context.Location,
                0);

        /// <summary>
        /// Gets an array upper bound.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static Value CreateGetArrayUpperBound(ref InvocationContext context) =>
            context.Builder.CreateArithmetic(
                context.Location,
                CreateGetArrayLength(ref context),
                context.Builder.CreatePrimitiveValue(
                    context.Location,
                    1),
                BinaryArithmeticKind.Sub);

        /// <summary>
        /// Gets an array length.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static Value CreateGetArrayLength(ref InvocationContext context) =>
            context.Builder.CreateGetArrayLength(
                context.Location,
                context[0],
                context[1]);

        /// <summary>
        /// Gets an array long length.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resulting value.</returns>
        private static Value CreateGetArrayLongLength(ref InvocationContext context) =>
            context.Builder.CreateConvertToInt64(
                context.Location,
                CreateGetArrayLength(ref context));

        #endregion
    }
}
