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
    ILGPU.Util.FormatString.FormatExpression>;
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
                "%lld",

                "%n",
                "%f",
                "%lf",

                "%u",
                "%u",
                "%u",
                "%llu");

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
