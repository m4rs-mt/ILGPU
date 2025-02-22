// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Interop.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.Values;
using System.Linq;

namespace ILGPUC.Frontend.Intrinsic;

partial class Intrinsics
{
    /// <summary>
    /// Handles interop sizeof operations.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    /// <returns>The resulting value.</returns>
    private static ValueReference Interop_SizeOf(ref InvocationContext context) =>
        context.Builder.CreateSizeOf(
            context.Location,
            context.Builder.CreateType(context.GetMethodGenericArguments().First()));

    /// <summary>
    /// Creates a new offset-of computation.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    private static ValueReference Interop_OffsetOf(ref InvocationContext context)
    {
        var builder = context.Builder;
        var typeInfo = builder.TypeContext.GetTypeInfo(
            context.GetMethodGenericArguments().First());
        var fieldName = context.Pull().ResolveAs<StringValue>().AsNotNull();
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
    /// Creates a new float-as-int cast.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    private static ValueReference Interop_FloatAsInt(ref InvocationContext context) =>
        context.Builder.CreateFloatAsIntCast(context.Location, context.Pull());

    /// <summary>
    /// Creates a new int-as-float cast.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    private static ValueReference Interop_IntAsFloat(ref InvocationContext context) =>
        context.Builder.CreateIntAsFloatCast(context.Location, context.Pull());

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
    private static ValueReference Interop_Write(ref InvocationContext context) =>
        CreateWrite(
            GetFormatExpression(ref context),
            ref context);

    /// <summary>
    /// Creates a new write-line instruction to the standard output stream.
    /// </summary>
    /// <param name="context">The current invocation context.</param>
    private static ValueReference Interop_WriteLine(ref InvocationContext context)
    {
        var format = GetFormatExpression(ref context);
        format = Interop.GetWriteLineFormat(format);
        return CreateWrite(format, ref context);
    }
}
