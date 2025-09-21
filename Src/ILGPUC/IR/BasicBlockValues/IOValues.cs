// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: IOValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Util;
using System;
using System.Collections;
using System.Collections.Immutable;
using System.Text;
using FormatArray = System.Collections.Immutable.ImmutableArray<
    ILGPUC.Util.FormatString.FormatExpression>;

namespace ILGPUC.IR.BasicBlockValues;

/// <summary>
/// Represents an abstract Input/Output (IO) value with side effects.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class IOValue(in BasicBlockValueInitializer initializer, TypeValue type) :
    MemoryValue(initializer, type);

/// <summary>
/// Represents a console output.
/// </summary>
sealed partial class WriteToOutput : IOValue
{
    #region Nested Types and Constants

    /// <summary>
    /// Represents an write argument collection.
    /// </summary>
    /// <param name="writeToOutput">The parent write node.</param>
    internal readonly ref struct ArgumentCollection(WriteToOutput writeToOutput)
    {
        /// <summary>
        /// Returns an enumerator to enumerate all values in argument collection.
        /// </summary>
        /// <param name="writeToOutput">The parent write node.</param>
        internal ref struct Enumerator(WriteToOutput writeToOutput)
        {
            private FormatArray.Enumerator _enumerator =
                writeToOutput.Expressions.GetEnumerator();

            /// <summary>
            /// Returns the current use.
            /// </summary>
            public Value Current { get; private set; } =
                Utilities.InitNotNullable<Value>();

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext()
            {
                while (_enumerator.MoveNext())
                {
                    if (!_enumerator.Current.HasArgument)
                        continue;
                    Current = writeToOutput.GetValue<Value>(_enumerator.Current.Argument);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Returns an enumerator to enumerate all uses in the context
        /// of the parent scope.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator() => new(writeToOutput);
    }

    /// <summary>
    /// All native PrintF formats for all arithmetic basic value types.
    /// </summary>
    private static readonly ImmutableArray<string> PrintFFormats =
        ImmutableArray.Create(
            "%n", "%u",
            "%d", "%d", "%d", "%lld",
            "%n", "%f", "%lf",
            "%u", "%u", "%u", "%llu");

    /// <summary>
    /// The native PrintF pointer format.
    /// </summary>
    private const string PrintFPointerFormat = "%p";

    /// <summary>
    /// The native PrintF percent format.
    /// </summary>
    private const string PrintFPercentFormat = "%%";

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
        BasicBlockBuilder builder,
        Location location,
        Value value) =>
        value.BasicValueType switch
        {
            BasicValueType.Int1 or
            BasicValueType.Int8 or
            BasicValueType.Int16 => builder.CreateConvertToInt32(location, value),
            BasicValueType.Float16 or
            BasicValueType.Float32 =>
                builder.CreateConvert(location, value, BasicValueType.Float64),
            _ => value,
        };

    #endregion

    #region WriteToOutput Instance

    /// <summary>
    /// Constructs a new debug operation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="expressions">The list of all format expressions.</param>
    /// <param name="arguments">The arguments to format.</param>
    public WriteToOutput(
        in BasicBlockValueInitializer initializer,
        FormatArray expressions,
        ref ValueBuilderList arguments)
        : base(initializer, initializer.ModuleBuilder.VoidType)
    {
#if DEBUG
        foreach (var argument in arguments)
        {
            this.Assert(
                argument.BasicValueType != BasicValueType.None,
                "WriteToOutput argument has BasicValueType.None");
        }
        foreach (var expression in expressions)
        {
            this.Assert(
                !expression.HasArgument ||
                expression.Argument >= 0 && expression.Argument < arguments.Count,
                $"WriteToOutput expression argument index {expression.Argument} out of"
                + $" range [0, {arguments.Count})");
        }
#endif

        Expressions = expressions;
        Seal(ref arguments);
    }

    /// <summary>
    /// Returns the underlying native format expressions.
    /// </summary>
    public FormatArray Expressions { get; }

    /// <summary>
    /// Returns all direct argument references for further processing.
    /// </summary>
    public ArgumentCollection Arguments => new(this);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var arguments = ValueBuilderList.Create(Generation, Count);
        foreach (Value argument in Values)
            arguments.Add(rewriter.Rewrite(argument));
        return rewriter.Builder.CreateWriteToOutput(
            Location,
            Expressions,
            ref arguments);
    }

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
                var argument = GetValue<Value>(expression.Argument);
                string argumentFormat = argument.Type is PointerType
                    ? PrintFPointerFormat
                    : GetPrintFFormat(
                        argument.BasicValueType.GetArithmeticBasicValueType(false));
                result.Append(argumentFormat);
            }
            else
            {
                // Append the underlying expression string and escape % characters
                result.Append(
                    expression.String.AsNotNull().Replace(
                        "%",
                        PrintFPercentFormat,
                        StringComparison.Ordinal));
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// Converts the internal format expressions into an escaped sequence.
    /// </summary>
    public string ToEscapedPrintFExpression() =>
        ToPrintFExpression()
        // Replace backslash before others, so that we do not double-escape.
        .Replace("\\", @"\\", StringComparison.Ordinal)
        // On Unix, replaces NewLine of \n with an escaped \n.
        // On Windows, replaces NewLine of \r\n with an escaped \n.
        // The printf call expects \n, and will translate to the appropriate newline.
        .Replace(Environment.NewLine, @"\n", StringComparison.Ordinal)
        .Replace("\t", @"\t", StringComparison.Ordinal)
        .Replace("\r", @"\r", StringComparison.Ordinal)
        .Replace("\n", @"\n", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "write";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        ToPrintFExpression().Replace(
            Environment.NewLine,
            string.Empty,
            StringComparison.Ordinal) +
        " " + base.ToArgString();

    #endregion
}
