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

using ILGPUC.IR.PureValues;
using DirectionList =
    System.Collections.Immutable.ImmutableArray<
        ILGPUC.IR.BasicBlockValues.EmitParameterDirection>;
using FormatArray = System.Collections.Immutable.ImmutableArray<
    ILGPUC.Util.FormatString.FormatExpression>;

namespace ILGPUC.IR.BasicBlockValues.Construction;

partial class BasicBlockBuilder
{
    /// <summary>
    /// Creates a new failed debug assertion.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="condition">The debug assert condition.</param>
    /// <param name="message">The assertion message.</param>
    /// <returns>A node that represents the debug assertion.</returns>
    public Value? CreateDebugAssert(
        Location location,
        Value? condition,
        Value? message)
    {
        if (condition is null || message is null) return null;

        // Try to simplify debug assertions
        location.Assert(message is StringValue);
        return condition is PrimitiveValue primitiveValue &&
            primitiveValue.RawValue != 0L
            ? null
            : Append(new DebugAssertOperation(
                GetInitializer(location),
                condition,
                message));
    }

    /// <summary>
    /// Creates a <see cref="System.Console.Write(string, object[])"/>-like output
    /// operation using typed expression formats.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="expressions">The list of all format expressions.</param>
    /// <param name="arguments">The arguments to format.</param>
    /// <returns>A node that represents the output operation.</returns>
    public Value CreateWriteToOutput(
        Location location,
        FormatArray expressions,
        ref ValueBuilderList arguments) =>
        Append(new WriteToOutput(GetInitializer(location), expressions, ref arguments));

    /// <summary>
    /// Creates an inline language output operation using typed expression formats.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="emitKind">The kind of language to emit.</param>
    /// <param name="usingRefParams">True, if passing parameters by reference.</param>
    /// <param name="expressions">The list of all format expressions.</param>
    /// <param name="directions">Indicates the direction of the arguments.</param>
    /// <param name="arguments">The arguments to format.</param>
    /// <returns>A node that represents the language emit operation.</returns>
    public LanguageEmitValue CreateLanguageEmit(
        Location location,
        LanguageEmitKind emitKind,
        bool usingRefParams,
        FormatArray expressions,
        DirectionList directions,
        ref ValueBuilderList arguments) =>
        Append(new LanguageEmitValue(
            GetInitializer(location),
            emitKind,
            usingRefParams,
            expressions,
            directions,
            ref arguments));
}
