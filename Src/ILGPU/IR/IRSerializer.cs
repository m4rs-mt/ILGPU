// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRSerializer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;

namespace ILGPU.IR
{
    /// <summary>
    /// Wrapper class around <see cref="BinaryWriter"/> for serializing IR types and values. 
    /// </summary>
    public sealed partial class IRSerializer : IDisposable
    {
        private readonly BinaryWriter writer;

        /// <summary>
        /// Wraps an instance of <see cref="IRSerializer"/>
        /// around a given <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> instance to wrap.
        /// </param>
        /// <param name="encoding">
        /// The <see cref="Encoding"/> to use for
        /// serializing <see cref="string"/> values.
        /// </param>
        public IRSerializer(Stream stream, Encoding encoding)
        {
            writer = new BinaryWriter(stream, encoding);
        }

        /// <summary>
        /// Serializes a 32-bit integer value to the stream.
        /// </summary>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        public void Serialize(int value) =>
            writer.Write(value);

        /// <summary>
        /// Serializes a 64-bit integer value to the stream.
        /// </summary>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        public void Serialize(long value) =>
            writer.Write(value);

        /// <summary>
        /// Serializes a string value to the stream.
        /// </summary>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        public void Serialize(string value) =>
            writer.Write(value);

        /// <summary>
        /// Serializes an arbitrary <see cref="Enum"/> value as a 32-bit integer, to the stream.
        /// </summary>
        /// <param name="value">
        /// The value to serialize.
        /// </param>
        public void Serialize<T>(T value) where T : Enum =>
            writer.Write(Convert.ToInt32(value));

        /// <summary>
        /// Disposes of the wrapped <see cref="BinaryWriter"/> instance.
        /// </summary>
        public void Dispose() => ((IDisposable)writer).Dispose();
    }
}
