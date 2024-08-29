// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeKind.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Enumeration of tHhe various special kinds of <see cref="TypeNode"/>.
    /// </summary>
    public enum TypeKind
    {
        /// <summary>
        /// Fallback value for when the classification is unknown or doesn't apply
        /// </summary>
        Unknown,

        /// <summary>
        /// See <see cref="VoidType"/>
        /// </summary>
        Void,

        /// <summary>
        /// See <see cref="StringType"/>
        /// </summary>
        String,

        /// <summary>
        /// See <see cref="PrimitiveType"/>
        /// </summary>
        Primitive,

        /// <summary>
        /// See <see cref="PaddingType"/>
        /// </summary>
        Padding,

        /// <summary>
        /// See <see cref="PointerType"/>
        /// </summary>
        Pointer,

        /// <summary>
        /// See <see cref="ViewType"/>
        /// </summary>
        View,

        /// <summary>
        /// See <see cref="ArrayType"/>
        /// </summary>
        Array,

        /// <summary>
        /// See <see cref="StructureType"/>
        /// </summary>
        Structure,

        /// <summary>
        /// See <see cref="HandleType"/>
        /// </summary>
        Handle,
    }
}
