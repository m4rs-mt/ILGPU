// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: IOValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using FormatArray = System.Collections.Immutable.ImmutableArray<
    ILGPU.Util.FormatString.FormatExpression>;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a <see cref="System.Console.Write(string, object[])"/>-like output
        /// operation using typed expression formats.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="expressions">The list of all format expressions.</param>
        /// <param name="arguments">The arguments to format.</param>
        /// <returns>A node that represents the output operation.</returns>
        public ValueReference CreateWriteToOutput(
            Location location,
            FormatArray expressions,
            ref ValueList arguments) =>
            Append(new WriteToOutput(
                GetInitializer(location),
                expressions,
                ref arguments,
                VoidType));
    }
}
