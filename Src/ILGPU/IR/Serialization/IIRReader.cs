// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IIRReader.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.IR.Serialization
{
    /// <summary>
    /// Describes a wrapper that deserializes IR values and
    /// types to an <see cref="IRContext"/>.
    /// </summary>
    public partial interface IIRReader : IDisposable
    {
        /// <summary>
        /// Returns this instance's associated mapping context.
        /// </summary>
        IIRMappingContext Context { get; }

        /// <summary>
        /// Deserializes a 32-bit integer from the stream.
        /// </summary>
        /// <param name="value">
        /// The deserialized value.
        /// </param>
        /// <returns>
        /// Whether or not the value was
        /// read from the stream successfully.
        /// </returns>
        bool Read(out int value);

        /// <summary>
        /// Deserializes a 64-bit integer from the stream.
        /// </summary>
        /// <param name="value">
        /// The deserialized value.
        /// </param>
        /// <returns>
        /// Whether or not the value was
        /// read from the stream successfully.
        /// </returns>
        bool Read(out long value);

        /// <summary>
        /// Deserializes a string from the stream.
        /// </summary>
        /// <param name="value">
        /// The deserialized value.
        /// </param>
        /// <returns>
        /// Whether or not the value was
        /// read from the stream successfully.
        /// </returns>
        bool Read([NotNullWhen(true)] out string? value);

        /// <summary>
        /// Deserializes a 32-bit integer from the stream
        /// as an enumerated type value. 
        /// </summary>
        /// <typeparam name="T">
        /// The underlying type enumeration.
        /// </typeparam>
        /// <param name="value">
        /// The deserialized value.
        /// </param>
        /// <returns>
        /// Whether or not the value was
        /// read from the stream successfully.
        /// </returns>
        bool Read<T>(out T value)
            where T : unmanaged, Enum;
    }
}
