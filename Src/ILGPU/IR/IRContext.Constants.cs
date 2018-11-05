// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: IRContext.Constants.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System.Collections.Immutable;

namespace ILGPU.IR
{
    partial class IRContext
    {
        /// <summary>
        /// Contains all basic value types.
        /// </summary>
        internal static readonly ImmutableArray<BasicValueType> BasicValueTypes =
            ImmutableArray.Create(
                BasicValueType.Int1,
                BasicValueType.Int8,
                BasicValueType.Int16,
                BasicValueType.Int32,
                BasicValueType.Int64,
                BasicValueType.Float32,
                BasicValueType.Float64);

        /// <summary>
        /// Represents the index type of a view.
        /// </summary>
        public static readonly BasicValueType ViewIndexType = BasicValueType.Int32;

        /// <summary>
        /// Represents the default flags of a new context.
        /// </summary>
        public static readonly IRContextFlags DefaultFlags = IRContextFlags.None;

        /// <summary>
        /// Represents the default debug flags of a new context.
        /// </summary>
        public static readonly IRContextFlags DefaultDebug =
            DefaultFlags | IRContextFlags.EnableDebugInformation;

        /// <summary>
        /// Represents the default flags of a new context.
        /// </summary>
        public static readonly IRContextFlags FastMathFlags =
            DefaultFlags | IRContextFlags.FastMath;

        /// <summary>
        /// Represents the default flags of a new context.
        /// </summary>
        public static readonly IRContextFlags FastMath32BitFloatsFlags =
            FastMathFlags | IRContextFlags.Force32BitFloats;
    }
}
