// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: LanguageIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using static ILGPU.Util.FormatString;
using FormatArray = System.Collections.Immutable.ImmutableArray<
    ILGPU.Util.FormatString.FormatExpression>;

namespace ILGPU.Frontend.Intrinsic
{
    enum LanguageIntrinsicKind
    {
        EmitPTX,
    }

    /// <summary>
    /// Marks inline language methods that are built in.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class LanguageIntrinsicAttribute : IntrinsicAttribute
    {
        public LanguageIntrinsicAttribute(LanguageIntrinsicKind intrinsicKind)
        {
            IntrinsicKind = intrinsicKind;
        }

        public override IntrinsicType Type => IntrinsicType.Language;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public LanguageIntrinsicKind IntrinsicKind { get; }
    }

    partial class Intrinsics
    {
        /// <summary>
        /// Regex for parsing PTX assembly instructions.
        /// </summary>
        private static readonly Regex PTXExpressionRegex =
            // Escape sequence, %n arguments, singular % detection.
            new Regex("(%%|%\\d+|%)");

        /// <summary>
        /// Handles language operations.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="attribute">The intrinsic attribute.</param>
        /// <returns>The resulting value.</returns>
        private static ValueReference HandleLanguageOperation(
            ref InvocationContext context,
            LanguageIntrinsicAttribute attribute)
        {
            var builder = context.Builder;
            return attribute.IntrinsicKind switch
            {
                LanguageIntrinsicKind.EmitPTX =>
                    CreateLanguageEmitPTX(ref context),
                _ => throw context.Location.GetNotSupportedException(
                    ErrorMessages.NotSupportedLanguageIntrinsic,
                    attribute.IntrinsicKind.ToString()),
            };
        }

        /// <summary>
        /// Creates a new inline PTX instruction.
        /// </summary>
        /// <param name="ptxExpression">The PTX expression string.</param>
        /// <param name="context">The current invocation context.</param>
        private static ValueReference CreateLanguageEmitPTX(
            string ptxExpression,
            ref InvocationContext context)
        {
            // Parse PTX expression and ensure valid argument references
            var location = context.Location;
            if (!TryParse(ptxExpression, out var expressions))
            {
                throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportedInlinePTXFormat,
                    ptxExpression);
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
                        ErrorMessages.NotSupportedInlinePTXFormatArgumentRef,
                        ptxExpression,
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
                if (arg.BasicValueType != BasicValueType.None)
                    continue;
                throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportedInlinePTXFormatArgumentType,
                    ptxExpression,
                    arg.Type.ToString());
            }

            // The method parameter at position 0 is the PTX string.
            // The method parameter at position 1 is the first argument to the PTX string.
            var methodParams = context.Method.GetParameters();
            var hasOutput = methodParams.Length >= 2 && methodParams[1].IsOut;

            // Create the language statement
            return context.Builder.CreateLanguageEmitPTX(
                location,
                expressions,
                hasOutput,
                ref arguments);
        }

        /// <summary>
        /// Creates a new inline PTX instruction to the standard output stream.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        private static ValueReference CreateLanguageEmitPTX(
            ref InvocationContext context) =>
            CreateLanguageEmitPTX(
                GetEmitPTXExpression(ref context),
                ref context);

        /// <summary>
        /// Resolves a PTX expression string.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The resolved PTX expression string.</returns>
        private static string GetEmitPTXExpression(ref InvocationContext context)
        {
            var ptxExpression = context[0].ResolveAs<StringValue>();
            return ptxExpression is null
                ? throw context.Location.GetNotSupportedException(
                    ErrorMessages.NotSupportedInlinePTXFormatConstant,
                    context[0].ToString())
                : ptxExpression.String ?? string.Empty;
        }

        /// <summary>
        /// Parses the given PTX expression into an array of format expressions.
        /// </summary>
        /// <param name="ptxExpression">The PTX format expression.</param>
        /// <param name="expressions">The array of managed format expressions.</param>
        /// <returns>True, if all expressions could be parsed successfully.</returns>
        public static bool TryParse(
            string ptxExpression,
            out FormatArray expressions)
        {
            // Search for '%n' format arguments
            var parts = PTXExpressionRegex.Split(ptxExpression);
            var result = ImmutableArray.CreateBuilder<FormatExpression>(parts.Length);

            foreach (var part in parts)
            {
                if (part.Equals("%%", StringComparison.Ordinal))
                {
                    result.Add(new FormatExpression("%"));
                }
                else if (part.StartsWith("%", StringComparison.Ordinal))
                {
                    // Check whether the argument can be resolved to an integer.
                    if (int.TryParse(part.Substring(1), out int argument))
                    {
                        result.Add(new FormatExpression(argument));
                    }
                    else
                    {
                        // Singular % or remaining text was not a number.
                        expressions = FormatArray.Empty;
                        return false;
                    }
                }
                else if (part.Length > 0)
                {
                    result.Add(new FormatExpression(part));
                }
            }

            expressions = result.ToImmutable();
            return true;
        }
    }
}
