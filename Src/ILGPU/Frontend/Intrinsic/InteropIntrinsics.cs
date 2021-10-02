// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: InteropIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;

namespace ILGPU.Frontend.Intrinsic
{
    enum InteropIntrinsicKind
    {
        SizeOf,
        OffsetOf,

        FloatAsInt,
        IntAsFloat,

        Write,
        WriteLine
    }

    /// <summary>
    /// Marks intrinsic interop methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class InteropIntrinsicAttribute : IntrinsicAttribute
    {
        public InteropIntrinsicAttribute(InteropIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.Interop;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public InteropIntrinsicKind IntrinsicKind { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Handles interop operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleInterop(
            ref InvocationContext context,
            InteropIntrinsicAttribute attribute)
        {
            var builder = context.Builder;
            return attribute.IntrinsicKind switch
            {
                InteropIntrinsicKind.SizeOf => builder.CreateSizeOf(
                    context.Location,
                    builder.CreateType(context.GetMethodGenericArguments()[0])),
                InteropIntrinsicKind.OffsetOf => CreateOffsetOf(ref context),
                InteropIntrinsicKind.FloatAsInt => builder.CreateFloatAsIntCast(
                    context.Location,
                    context[0]),
                InteropIntrinsicKind.IntAsFloat => builder.CreateIntAsFloatCast(
                    context.Location,
                    context[0]),
                InteropIntrinsicKind.Write => CreateWrite(ref context),
                InteropIntrinsicKind.WriteLine => CreateWriteLine(ref context),
                _ => throw context.Location.GetNotSupportedException(
                    ErrorMessages.NotSupportedInteropIntrinsic,
                    attribute.IntrinsicKind.ToString()),
            };
        }

        /// <summary>
        /// Creates a new offset-of computation.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        private static ValueReference CreateOffsetOf(ref InvocationContext context)
        {
            var builder = context.Builder;
            var typeInfo = builder.TypeContext.GetTypeInfo(
                context.GetMethodGenericArguments()[0]);
            var fieldName = context[0].ResolveAs<StringValue>();
            int fieldIndex = 0;
            foreach (var field in typeInfo.Fields)
            {
                if (field.Name == fieldName.String)
                {
                    fieldIndex = typeInfo.GetAbsoluteIndex(field);
                    break;
                }
            }
            var irType = context.Builder.CreateType(typeInfo.ManagedType);
            return context.Builder.CreateOffsetOf(
                context.Location,
                irType,
                fieldIndex);
        }

        /// <summary>
        /// Resolves a format expression string.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resolved format expression string.</returns>
        private static string GetFormatExpression(ref InvocationContext context)
        {
            var formatExpression = context[0].ResolveAs<StringValue>();
            return formatExpression is null
                ? throw context.Location.GetNotSupportedException(
                    ErrorMessages.NotSupportedWriteFormatConstant,
                    context[0].ToString())
                : formatExpression.String ?? string.Empty;
        }

        /// <summary>
        /// Creates a new write instruction to the standard output stream.
        /// </summary>
        /// <param name="formatExpression">The format expression string.</param>
        /// <param name="context">The current invocation context.</param>
        private static ValueReference CreateWrite(
            string formatExpression,
            ref InvocationContext context)
        {
            // Parse format expression and ensure valid argument references
            var location = context.Location;
            if (!FormatString.TryParse(formatExpression, out var expressions))
            {
                throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportedWriteFormat,
                    formatExpression);
            }

            // Validate all expressions
            foreach (var expression in expressions)
            {
                if (!expression.HasArgument)
                    continue;
                if (expression.Argument < 0 ||
                    expression.Argument >= context.NumArguments - 1)
                {
                    throw location.GetNotSupportedException(
                        ErrorMessages.NotSupportedWriteFormatArgumentRef,
                        formatExpression,
                        expression.Argument);
                }
            }

            // Gather all arguments
            var arguments = InlineList<ValueReference>.Empty;
            context.Arguments.CopyTo(ref arguments);
            arguments.RemoveAt(0);

            // Valid all argument types
            foreach (var arg in arguments)
            {
                if (arg.Type.IsPointerType || arg.BasicValueType != BasicValueType.None)
                    continue;
                throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportedWriteFormatArgumentType,
                    formatExpression,
                    arg.Type.ToString());
            }

            // Create the output writer
            return context.Builder.CreateWriteToOutput(
                location,
                expressions,
                ref arguments);
        }

        /// <summary>
        /// Creates a new write instruction to the standard output stream.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        private static ValueReference CreateWrite(ref InvocationContext context) =>
            CreateWrite(
                GetFormatExpression(ref context),
                ref context);

        /// <summary>
        /// Creates a new write-line instruction to the standard output stream.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        private static ValueReference CreateWriteLine(ref InvocationContext context)
        {
            var format = GetFormatExpression(ref context);
            format = Interop.GetWriteLineFormat(format);
            return CreateWrite(format, ref context);
        }
    }
}
