// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: LanguageValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using DirectionList =
    System.Collections.Immutable.ImmutableArray<
        ILGPU.IR.Values.CudaEmitParameterDirection>;
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
        /// <param name="usingRefParams">True, if passing parameters by reference.</param>
        /// <param name="expressions">The list of all format expressions.</param>
        /// <param name="directions">Indicates the direction of the arguments.</param>
        /// <param name="arguments">The arguments to format.</param>
        /// <returns>A node that represents the language emit operation.</returns>
        public ValueReference CreateLanguageEmitPTX(
            Location location,
            bool usingRefParams,
            FormatArray expressions,
            DirectionList directions,
            ref ValueList arguments) =>
            Append(new LanguageEmitValue(
                GetInitializer(location),
                LanguageKind.PTX,
                usingRefParams,
                expressions,
                directions,
                ref arguments,
                VoidType));
    }
}
