// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueHeader.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;

namespace ILGPU.IR.Serialization
{
    /// <summary>
    /// A structure containing the basic information necessary
    /// to deserialize an IR value from a stream.
    /// </summary>
    public readonly ref struct ValueHeader
    {
        /// <summary>
        /// Returns the <see cref="ValueKind"/> associated with this value.
        /// </summary>
        public ValueKind Kind { get; }

        /// <summary>
        /// Returns the parent <see cref="IR.Method"/> of this value.
        /// </summary>
        public Method? Method { get; }

        /// <summary>
        /// Returns the parent <see cref="IR.BasicBlock"/> of this value.
        /// </summary>
        public BasicBlock? Block { get; }


        /// <summary>
        /// Returns all <see cref="IR.Value"/> instances associated with this value.
        /// </summary>
        public ReadOnlySpan<ValueReference> Nodes { get; }

        /// <summary>
        /// Initializes an instance of this type.
        /// </summary>
        public ValueHeader(ValueKind kind, Method? method, BasicBlock? block, ReadOnlySpan<ValueReference> nodes)
        {
            Kind = kind;
            Method = method;
            Block = block;
            Nodes = nodes;
        }
    }
}
