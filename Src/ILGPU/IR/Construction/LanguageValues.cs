// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: LanguageValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
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
        /// Creates an inline language output operation using typed expression formats.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="expressions">The list of all format expressions.</param>
        /// <param name="hasOutput">Indicates if the first argument is output.</param>
        /// <param name="arguments">The arguments to format.</param>
        /// <returns>A node that represents the language emit operation.</returns>
        public ValueReference CreateLanguageEmitPTX(
            Location location,
            FormatArray expressions,
            bool hasOutput,
            ref ValueList arguments) =>
            Append(new LanguageEmitValue(
                GetInitializer(location),
                LanguageKind.PTX,
                expressions,
                hasOutput,
                ref arguments,
                VoidType));
    }
}
