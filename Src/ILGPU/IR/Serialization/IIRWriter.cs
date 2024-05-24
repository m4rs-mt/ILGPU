// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
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
    /// Describes a wrapper that serializes IR values and types to some implementation-specific instance
    /// </summary>
    public partial interface IIRWriter : IDisposable
    {
        /// <summary>
        /// Serializes a 32-bit integer value to the stream.
        /// </summary>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        void Write(int value);

        /// <summary>
        /// Serializes a 64-bit integer value to the stream.
        /// </summary>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        void Write(long value);

        /// <summary>
        /// Serializes a 32-bit integer value to the stream.
        /// </summary>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        void Write(string value);

        /// <summary>
        /// Serializes a string value to the stream.
        /// </summary>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        void Write<T>(T value) where T : Enum;
    }
}
