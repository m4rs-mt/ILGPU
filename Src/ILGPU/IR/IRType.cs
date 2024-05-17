// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRType.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Collections.Immutable;

namespace ILGPU.IR
{
    /// <summary>
    /// A uniform value type representing an exported
    /// <see cref="Types.TypeNode"/> from an <see cref="IRContext"/>.
    /// </summary>
    /// <param name="Id">Corresponds to <see cref="Node.Id"/></param>
    /// <param name="Class">
    /// A <see cref="Classifier"/> value denoting which kind of
    /// <see cref="Types.TypeNode"/> this instance represents
    /// </param>
    /// <param name="Nodes">
    /// A list of <see cref="NodeId"/> values corresponding to those
    /// of related instances; important for later reconstruction
    /// </param>
    /// <param name="BasicValueType">
    /// Corresponds to <see cref="Types.TypeNode.BasicValueType"/>
    /// </param>
    /// <param name="Data">Extra data specific to this type's kind or instance</param>
    public record struct IRType(
        NodeId Id,
        IRType.Classifier Class,
        ImmutableArray<NodeId> Nodes,
        BasicValueType BasicValueType,
        long Data)
    {
        /// <summary>
        /// Enumeration of the various special kinds of <see cref="Types.TypeNode"/>.
        /// </summary>
        public enum Classifier
        {
            /// <summary>
            /// Fallback value for when the classification is unknown or doesn't apply
            /// </summary>
            Unknown,

            /// <summary>
            /// See <see cref="Types.VoidType"/>
            /// </summary>
            Void,

            /// <summary>
            /// See <see cref="Types.StringType"/>
            /// </summary>
            String,

            /// <summary>
            /// See <see cref="Types.PrimitiveType"/>
            /// </summary>
            Primitive,

            /// <summary>
            /// See <see cref="Types.PointerType"/>
            /// </summary>
            Pointer,

            /// <summary>
            /// See <see cref="Types.ViewType"/>
            /// </summary>
            View,

            /// <summary>
            /// See <see cref="Types.ArrayType"/>
            /// </summary>
            Array,

            /// <summary>
            /// See <see cref="Types.StructureType"/>
            /// </summary>
            Structure,
        }
    }
}
