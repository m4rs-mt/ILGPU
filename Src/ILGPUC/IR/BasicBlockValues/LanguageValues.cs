// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LanguageValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Text;
using DirectionList = System.Collections.Immutable.ImmutableArray<
    ILGPUC.IR.BasicBlockValues.EmitParameterDirection>;
using FormatArray = System.Collections.Immutable.ImmutableArray<
    ILGPUC.Util.FormatString.FormatExpression>;

namespace ILGPUC.IR.BasicBlockValues;

/// <summary>
/// Indicates the direction of the emit parameter.
/// </summary>
[Flags]
enum EmitParameterDirection
{
    /// <summary>
    /// The parameter is not used in either direction.
    /// </summary>
    None = 0,

    /// <summary>
    /// The parameter is used for passing input values.
    /// </summary>
    In = 0x1,

    /// <summary>
    /// The parameter is used for passing output values.
    /// </summary>
    Out = 0x2,

    /// <summary>
    /// The parameter is used for both input and output.
    /// </summary>
    Both = In | Out,
}

/// <summary>
/// Represents an inline language statement.
/// </summary>
sealed partial class LanguageEmitValue : MemoryValue
{
    /// <summary>
    /// Constructs a new inline language statement.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="languageKind">The language kind.</param>
    /// <param name="usingRefParams">True, if passing parameters by reference.</param>
    /// <param name="expressions">The list of all format expressions.</param>
    /// <param name="directions">Indicates the direction of the arguments.</param>
    /// <param name="arguments">The arguments to format.</param>
    public LanguageEmitValue(
        in BasicBlockValueInitializer initializer,
        LanguageEmitKind languageKind,
        bool usingRefParams,
        FormatArray expressions,
        DirectionList directions,
        ref ValueBuilderList arguments)
        : base(initializer, initializer.ModuleBuilder.VoidType)
    {
#if DEBUG
        foreach (var argument in arguments)
            this.Assert(
                argument.BasicValueType != BasicValueType.None,
                "LanguageOperation argument has BasicValueType.None");
        foreach (var expression in expressions)
        {
            this.Assert(
                !expression.HasArgument ||
                expression.Argument >= 0 && expression.Argument < arguments.Count,
                $"LanguageOperation expression argument index {expression.Argument} " +
                $"out of range [0, {arguments.Count})");
        }
#endif

        Kind = languageKind;
        UsingRefParams = usingRefParams;
        Directions = directions;
        Expressions = expressions;
        Seal(ref arguments);
    }

    /// <summary>
    /// Returns true if the first argument is an output argument.
    /// </summary>
    public DirectionList Directions { get; }

    /// <summary>
    /// Returns the underlying native format expressions.
    /// </summary>
    public FormatArray Expressions { get; }

    /// <summary>
    /// Returns true if passing parameters by reference
    /// </summary>
    public bool UsingRefParams { get; }

    /// <summary>
    /// Returns true if the argument is an input parameter.
    /// </summary>
    public bool IsInputArgument(int argumentIndex) =>
        Directions[argumentIndex].HasFlag(EmitParameterDirection.In);

    /// <summary>
    /// Returns true if the argument is an output parameter.
    /// </summary>
    public bool IsOutputArgument(int argumentIndex) =>
        Directions[argumentIndex].HasFlag(EmitParameterDirection.Out);

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var arguments = ValueBuilderList.Create(Generation, Count);
        foreach (Value argument in Values)
            arguments.Add(rewriter.Rewrite(argument));
        return rewriter.Builder.CreateLanguageEmit(
            Location,
            Kind,
            UsingRefParams,
            Expressions,
            Directions,
            ref arguments);
    }

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "emit";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        ToStringExpression().Replace(
            Environment.NewLine,
            string.Empty,
            StringComparison.Ordinal) +
        " " + base.ToArgString();

    /// <summary>
    /// Converts the internal format expressions into a string for debugging purposes.
    /// </summary>
    /// <returns>The converted string.</returns>
    private string ToStringExpression()
    {
        var result = new StringBuilder();
        foreach (var expression in Expressions)
        {
            if (expression.HasArgument)
            {
                var argument = GetValue<Value>(expression.Argument);
                string argumentFormat = argument.Type.ToString();
                result.Append('{');
                result.Append(expression.Argument);
                result.Append(':');
                result.Append(argumentFormat);
                result.Append('}');
            }
            else
            {
                result.Append(expression.String);
            }
        }
        return result.ToString();
    }
}
