// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IOValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using FormatArray = System.Collections.Immutable.ImmutableArray<
    ILGPU.IR.Values.WriteToOutput.FormatExpression>;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an abstract Input/Output (IO) value with side effects.
    /// </summary>
    public abstract class IOValue : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new debug operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="staticType">The static type.</param>
        internal IOValue(in ValueInitializer initializer, TypeNode staticType)
            : base(initializer, staticType)
        { }

        #endregion
    }

    /// <summary>
    /// Represents a console output.
    /// </summary>
    [ValueKind(ValueKind.WriteToOutput)]
    public sealed class WriteToOutput : IOValue
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

        /// <summary>
        /// Represents an write argument collection.
        /// </summary>
        public readonly ref struct ArgumentCollection
        {
            #region Nested Types

            /// <summary>
            /// Returns an enumerator to enumerate all values in argument collection.
            /// </summary>
            public ref struct Enumerator
            {
                private FormatArray.Enumerator enumerator;

                /// <summary>
                /// Constructs a new use enumerator.
                /// </summary>
                /// <param name="writeToOutput">The parent write node.</param>
                internal Enumerator(WriteToOutput writeToOutput)
                {
                    WriteToOutput = writeToOutput;
                    enumerator = writeToOutput.Expressions.GetEnumerator();
                    Current = default;
                }

                /// <summary>
                /// Returns the associated node.
                /// </summary>
                public WriteToOutput WriteToOutput { get; }

                /// <summary>
                /// Returns the current use.
                /// </summary>
                public Value Current { get; private set; }

                /// <summary cref="IEnumerator.MoveNext"/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext()
                {
                    while (enumerator.MoveNext())
                    {
                        if (!enumerator.Current.HasArgument)
                            continue;
                        Current = WriteToOutput[enumerator.Current.Argument];
                        return true;
                    }
                    return false;
                }
            }

            #endregion

            /// <summary>
            /// Constructs a new argument collection.
            /// </summary>
            /// <param name="writeToOutput">The parent write node.</param>
            internal ArgumentCollection(WriteToOutput writeToOutput)
            {
                WriteToOutput = writeToOutput;
            }

            /// <summary>
            /// Returns the associated node.
            /// </summary>
            public WriteToOutput WriteToOutput { get; }

            /// <summary>
            /// Returns an enumerator to enumerate all uses in the context
            /// of the parent scope.
            /// </summary>
            /// <returns>The enumerator.</returns>
            public readonly Enumerator GetEnumerator() => new Enumerator(WriteToOutput);
        }

        #endregion

        #region Constants

        /// <summary>
        /// All native PrintF formats for all arithmetic basic value types.
        /// </summary>
        private static readonly ImmutableArray<string> PrintFFormats =
            ImmutableArray.Create(
                "%n",
                "%u",

                "%d",
                "%d",
                "%d",
                "%ld",

                "%n",
                "%f",
                "%lf",

                "%i",
                "%i",
                "%i",
                "%lu");

        /// <summary>
        /// The native PrintF pointer format.
        /// </summary>
        private const string PrintFPointerFormat = "%p";

        /// <summary>
        /// The native PrintF percent format.
        /// </summary>
        private const string PrintFPercentFormat = "%%";

        #endregion

        #region Static

        /// <summary>
        /// Returns the native PrintF format for the given basic value type.
        /// </summary>
        /// <param name="valueType">The basic value type.</param>
        /// <returns>The resolved PrintF format.</returns>
        public static string GetPrintFFormat(ArithmeticBasicValueType valueType) =>
            PrintFFormats[(int)valueType];

        /// <summary>
        /// Converts the given value into a printf compatible argument.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted value.</returns>
        public static Value ConvertToPrintFArgument(
            IRBuilder builder,
            Location location,
            Value value)
        {
            switch (value.BasicValueType)
            {
                case BasicValueType.Int1:
                case BasicValueType.Int8:
                case BasicValueType.Int16:
                    return builder.CreateConvertToInt32(location, value);
                case BasicValueType.Float16:
                case BasicValueType.Float32:
                    return builder.CreateConvert(
                        location,
                        value,
                        BasicValueType.Float64);
                default:
                    return value;
            }
        }

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
            while (formatExpression.Length > 0)
            {
                // Search for next {
                int startIndex = formatExpression.IndexOf('{', 0);
                if (startIndex < 0)
                {
                    result.Add(new FormatExpression(formatExpression));
                    break;
                }
                else if (startIndex > 0)
                {
                    result.Add(new FormatExpression(
                        formatExpression.Substring(0, startIndex)));
                }

                // Search for next }
                int endIndex = formatExpression.IndexOf('}', startIndex);
                if (endIndex < 0)
                {
                    result.Add(new FormatExpression(formatExpression));
                    break;
                }

                // Check sub expression
                var subExpr = formatExpression.Substring(
                    startIndex + 1,
                    endIndex - startIndex - 1);

                // Check whether the argument can be resolved to an integer
                if (!int.TryParse(subExpr, out int argument) || argument < 0)
                    return false;

                // Append current argument
                result.Add(new FormatExpression(argument));
                formatExpression = formatExpression.Substring(endIndex + 1);
            }

            expressions = result.ToImmutable();
            return true;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new debug operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="expressions">The list of all format expressions.</param>
        /// <param name="arguments">The arguments to format.</param>
        /// <param name="voidType">The void type.</param>
        internal WriteToOutput(
            in ValueInitializer initializer,
            FormatArray expressions,
            ref ValueList arguments,
            VoidType voidType)
            : base(initializer, voidType)
        {
            foreach (var argument in arguments)
                this.Assert(argument.BasicValueType != BasicValueType.None);
            foreach (var expression in expressions)
            {
                this.Assert(
                    !expression.HasArgument ||
                    expression.Argument >= 0 && expression.Argument < arguments.Count);
            }

            Expressions = expressions;
            Seal(ref arguments);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.WriteToOutput;

        /// <summary>
        /// Returns the underlying native format expressions.
        /// </summary>
        public FormatArray Expressions { get; }

        /// <summary>
        /// Returns all direct argument references for further processing.
        /// </summary>
        public ArgumentCollection Arguments => new ArgumentCollection(this);

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder)
        {
            var arguments = ValueList.Create(Count);
            foreach (Value argument in this)
                arguments.Add(rebuilder[argument]);
            return builder.CreateWriteToOutput(
                Location,
                Expressions,
                ref arguments);
        }

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        /// <summary>
        /// Converts the internal format expressions into a printf string.
        /// </summary>
        /// <returns>The converted printf string.</returns>
        public string ToPrintFExpression()
        {
            var result = new StringBuilder();
            foreach (var expression in Expressions)
            {
                if (expression.HasArgument)
                {
                    // TODO: extend this functionality in the future to support
                    // typed signed/unsigned outputs
                    var argument = this[expression.Argument];
                    string argumentFormat = argument.Type.IsPointerType
                        ? PrintFPointerFormat
                        : GetPrintFFormat(
                            argument.BasicValueType.GetArithmeticBasicValueType(false));
                    result.Append(argumentFormat);
                }
                else
                {
                    // Append the underlying expression string and escape % characters
                    result.Append(
                        expression.String.Replace(
                            "%",
                            PrintFPercentFormat));
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Converts the internal format expressions into an escaped sequence.
        /// </summary>
        public string ToEscapedPrintFExpression() =>
            ToPrintFExpression()
            .Replace("\t", @"\t")
            .Replace("\r", @"\r")
            .Replace("\n", @"\n")
            .Replace("\"", "\\\"")
            .Replace("\\", @"\\");

        #endregion

        #region Methods

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "write";


        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            ToPrintFExpression().Replace(
                Environment.NewLine,
                string.Empty) +
            " " + base.ToArgString();

        #endregion
    }
}
