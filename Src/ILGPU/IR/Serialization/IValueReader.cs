// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IValueReader.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.IR.Serialization
{

    /// <summary>
    /// A generic delegate for constructing IR values from a stream.
    /// </summary>
    /// <param name="header">
    /// The header information associated with the
    /// target value instance.
    /// </param>
    /// <param name="reader">
    /// The wrapped stream instance to read from. 
    /// </param>
    /// <returns>
    /// The reconstructed, deserialized value. 
    /// </returns>
    public delegate Value? GenericValueReader(ValueHeader header, IIRReader reader);

    /// <summary>
    /// Common interface for IR value deserialization.
    /// </summary>
    public interface IValueReader
    {
        /// <summary>
        /// Performs the correct construction pattern
        /// for deserializing this specific type.
        /// </summary>
        /// <param name="header">
        /// The header information associated with the
        /// target value instance.
        /// </param>
        /// <param name="reader">
        /// The wrapped stream instance to read from. 
        /// </param>
        /// <returns>
        /// The reconstructed, deserialized value. 
        /// </returns>
        static abstract Value? Read(ValueHeader header, IIRReader reader);
    }
}
