// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: BinaryIRReader.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace ILGPU.IR.Serialization
{
    /// <summary>
    /// Wrapper class around <see cref="BinaryReader"/> for deserializing IR types and values. 
    /// </summary>
    public sealed class BinaryIRReader : IIRReader
    {
        private readonly BinaryReader reader;

        /// <inheritdoc/>
        public IIRMappingContext Context { get; }

        /// <summary>
        /// Wraps an instance of <see cref="BinaryIRReader"/>
        /// around a given <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="Stream"/> instance to wrap.
        /// </param>
        /// <param name="encoding">
        /// The <see cref="Encoding"/> to use for
        /// deserializing <see cref="string"/> values.
        /// </param>
        /// <param name="context">
        /// A mapping context instance to use for deserialization. 
        /// </param>
        public BinaryIRReader(Stream stream, Encoding encoding, IIRMappingContext context)
        {
            reader = new BinaryReader(stream, encoding);
            Context = context;
        }


        /// <inheritdoc/>
        public bool Read(out int value)
        {
            try
            {
                value = reader.ReadInt32();
                return true;
            }
            catch (EndOfStreamException)
            {
                value = default;
                return false;
            }
        }

        /// <inheritdoc/>
        public bool Read(out long value)
        {
            try
            {
                value = reader.ReadInt64();
                return true;
            }
            catch (EndOfStreamException)
            {
                value = default;
                return false;
            }
        }

        /// <inheritdoc/>
        public bool Read([NotNullWhen(true)] out string? value)
        {
            try
            {
                value = reader.ReadString();
                return true;
            }
            catch (EndOfStreamException)
            {
                value = default;
                return false;
            }
        }

        /// <inheritdoc/>
        public bool Read<T>(out T value)
            where T : unmanaged, Enum
        {
            try
            {
                value = (T)Enum.ToObject(typeof(T), reader.ReadInt32());
                return true;
            }
            catch (EndOfStreamException)
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Disposes of the wrapped <see cref="BinaryReader"/> instance.
        /// </summary>
        public void Dispose() => ((IDisposable)reader).Dispose();
    }
}
