// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: BasicValueType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU
{
    /// <summary>
    /// Represents a basic value type.
    /// </summary>
    public enum BasicValueType
    {
        /// <summary>
        /// Represent a non-basic value type.
        /// </summary>
        None,

        /// <summary>
        /// Represents an 1-bit integer.
        /// </summary>
        Int1,

        /// <summary>
        /// Represents an 8-bit integer.
        /// </summary>
        Int8,

        /// <summary>
        /// Represents a 16-bit integer.
        /// </summary>
        Int16,

        /// <summary>
        /// Represents a 32-bit integer.
        /// </summary>
        Int32,

        /// <summary>
        /// Represents a 64-bit integer.
        /// </summary>
        Int64,

        /// <summary>
        /// Represents a 32-bit float.
        /// </summary>
        Float32,

        /// <summary>
        /// Represents a 64-bit float.
        /// </summary>
        Float64,
    }

    /// <summary>
    /// Represents an arithmetic basic value type.
    /// </summary>
    public enum ArithmeticBasicValueType
    {
        /// <summary>
        /// Represent a non-arithemtic value type.
        /// </summary>
        None,

        /// <summary>
        /// Represents an 1-bit integer.
        /// </summary>
        UInt1,

        /// <summary>
        /// Represents an 8-bit integer.
        /// </summary>
        Int8,

        /// <summary>
        /// Represents a 16-bit integer.
        /// </summary>
        Int16,

        /// <summary>
        /// Represents a 32-bit integer.
        /// </summary>
        Int32,

        /// <summary>
        /// Represents a 64-bit integer.
        /// </summary>
        Int64,

        /// <summary>
        /// Represents a 32-bit float.
        /// </summary>
        Float32,

        /// <summary>
        /// Represents a 64-bit float.
        /// </summary>
        Float64,

        /// <summary>
        /// Represents an 8-bit unsigned integer.
        /// </summary>
        UInt8,

        /// <summary>
        /// Represents a 16-bit unsigned integer.
        /// </summary>
        UInt16,

        /// <summary>
        /// Represents a 32-bit unsigned integer.
        /// </summary>
        UInt32,

        /// <summary>
        /// Represents a 64-bit unsigned integer.
        /// </summary>
        UInt64,
    }
}
