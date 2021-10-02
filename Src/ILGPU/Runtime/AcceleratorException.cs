// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: AcceleratorException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.Serialization;

namespace ILGPU.Runtime
{
    /// <summary>
    /// The exception that is thrown when a specific operation did not succeed.
    /// </summary>
    [Serializable]
    public abstract class AcceleratorException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the AcceleratorException class.
        /// </summary>
        protected AcceleratorException()
        { }

        /// <summary>
        /// Initializes a new instance of the AcceleratorException class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        protected AcceleratorException(string message)
            : base(message)
        { }

        /// <summary>
        /// Initializes a new instance of the AcceleratorException class with a specified
        /// error message and a reference to the inner exception that is the cause of
        /// this exception.
        /// </summary>
        /// <param name="message">
        /// The error message that explains the reason for the exception.
        /// </param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or a null reference
        /// if no inner exception is specified.
        /// </param>
        protected AcceleratorException(string message, Exception innerException)
            : base(message, innerException)
        { }

        /// <summary>
        /// Initializes a new instance of the AcceleratorException class with serialized
        /// data.
        /// </summary>
        /// <param name="serializationInfo">
        /// The System.Runtime.Serialization.SerializationInfo that holds the serialized
        /// object data about the exception being thrown.
        /// </param>
        /// <param name="streamingContext">
        /// The System.Runtime.Serialization.StreamingContext that contains contextual
        /// information about the source or destination.
        /// </param>
        protected AcceleratorException(
            SerializationInfo serializationInfo,
            StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        { }

        /// <summary>
        /// Returns the associated accelerator type.
        /// </summary>
        public abstract AcceleratorType AcceleratorType { get; }
    }
}
