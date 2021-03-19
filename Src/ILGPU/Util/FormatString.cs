// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: FormatString.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using FormatArray = System.Collections.Immutable.ImmutableArray<
    ILGPU.Util.FormatString.FormatExpression>;

namespace ILGPU.Util
{
    /// <summary>
    /// Helper class to parse a string containing .NET style string formatting.
    /// e.g. "Hello {0}"
    /// </summary>
    public static class FormatString
    {
        #region Nested Types

        /// <summary>
        /// Represents a single format command.
        /// </summary>
        public readonly struct FormatExpression
        {
            /// <summary>
            /// Constructs a new format expression.
            /// </summary>
            /// <param name="string">The string expression.</param>
            public FormatExpression(ReadOnlySpan<char> @string)
                : this(@string.ToString())
            { }

            /// <summary>
            /// Constructs a new format expression.
            /// </summary>
            /// <param name="string">The string expression.</param>
            public FormatExpression(string @string)
            {
                String = @string ?? string.Empty;
                Argument = -1;
            }

            /// <summary>
            /// Constructs a new format expression.
            /// </summary>
            /// <param name="argument">The argument reference.</param>
            public FormatExpression(int argument)
            {
                String = null;
                Argument = argument;
            }

            /// <summary>
            /// Returns the string to output (if any).
            /// </summary>
            public string String { get; }

            /// <summary>
            /// Returns the argument reference to output.
            /// </summary>
            public int Argument { get; }

            /// <summary>
            /// Returns true if the current expression has an argument reference.
            /// </summary>
            public readonly bool HasArgument => String is null;
        }

        #endregion

        /// <summary>
        /// Parses the given format expression into an array of format expressions.
        /// </summary>
        /// <param name="formatExpression">The format expression.</param>
        /// <param name="expressions">The array of managed format expressions.</param>
        /// <returns>True, if all expressions could be parsed successfully.</returns>
        public static bool TryParse(string formatExpression, out FormatArray expressions)
        {
            expressions = FormatArray.Empty;

            // Search for '{xyz}' patterns
            var result = ImmutableArray.CreateBuilder<FormatExpression>(10);
            var expression = formatExpression.AsSpan();
            var inArgumentPart = false;

            while (expression.Length > 0)
            {
                // Search for next {
                int foundIndex = expression.IndexOfAny(new[] { '{', '}' });
                if (foundIndex < 0)
                {
                    result.Add(new FormatExpression(expression));
                    break;
                }

                var foundOpenBracket = expression[foundIndex] == '{';
                var escaped =
                    foundIndex + 1 < expression.Length &&
                    expression[foundIndex] == expression[foundIndex + 1];
                if (foundOpenBracket)
                {
                    // Cannot start another argument when one is already opened.
                    if (inArgumentPart)
                        return false;
                    if (escaped)
                    {
                        result.Add(new FormatExpression(
                            expression.Slice(0, foundIndex + 1)));
                        expression = expression.Slice(foundIndex + 2);
                    }
                    else
                    {
                        // Open new {
                        inArgumentPart = true;
                        if (foundIndex > 0)
                        {
                            result.Add(new FormatExpression(
                                expression.Slice(0, foundIndex)));
                        }
                        expression = expression.Slice(foundIndex + 1);
                    }
                    continue;
                }

                Debug.Assert(!foundOpenBracket);
                if (!inArgumentPart)
                {
                    // Handle escaping }
                    if (escaped)
                    {
                        result.Add(new FormatExpression(
                            expression.Slice(0, foundIndex + 1)));
                        expression = expression.Slice(foundIndex + 2);
                    }
                    else
                    {
                        // Cannot have singular } without opening {
                        return false;
                    }
                    continue;
                }

                // Check sub expression
                var endIndex = foundIndex;
                var subExpr = expression.Slice(0, endIndex);

                // Check whether the argument can be resolved to an integer
                if (!int.TryParse(subExpr.ToString(), out int argument) || argument < 0)
                    return false;

                // Append current argument
                result.Add(new FormatExpression(argument));
                expression = expression.Slice(endIndex + 1);
                inArgumentPart = false;
            }

            // Open { without matching }
            if (inArgumentPart)
                result.Add(new FormatExpression("{".AsSpan()));

            expressions = result.ToImmutable();
            return true;
        }
    }
}
