// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IIRWriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.IR.Serialization
{
    /// <summary>
    /// Describes a wrapper that serializes IR values and types
    /// to some implementation-specific instance
    /// </summary>
    public partial interface IIRWriter
    {
        /// <summary>
        /// Serializes a 32-bit integer value to the stream.
        /// </summary>
        /// <param name="tag">
        /// A tag that describes the purpose of this value.
        /// </param>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        void Write(string tag, int value);

        /// <summary>
        /// Serializes a 64-bit integer value to the stream.
        /// </summary>
        /// <param name="tag">
        /// A tag that describes the purpose of this value.
        /// </param>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        void Write(string tag, long value);

        /// <summary>
        /// Serializes a 32-bit integer value to the stream.
        /// </summary>
        /// <param name="tag">
        /// A tag that describes the purpose of this value.
        /// </param>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        void Write(string tag, string value);

        /// <summary>
        /// Serializes a string value to the stream.
        /// </summary>
        /// <param name="tag">
        /// A tag that describes the purpose of this value.
        /// </param>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        void Write<T>(T value)
            where T : unmanaged, Enum;
    }
}
