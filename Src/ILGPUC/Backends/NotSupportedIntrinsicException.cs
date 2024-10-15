// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: NotSupportedIntrinsicException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.Resources;
using System;
using System.Runtime.Serialization;

namespace ILGPU.Backends
{
    /// <summary>
    /// An exception that is thrown in case of a not support intrinsic.
    /// </summary>
    [Serializable]
    public sealed class NotSupportedIntrinsicException : Exception
    {
        /// <summary>
        /// Constructs a new intrinsic exception.
        /// </summary>
        public NotSupportedIntrinsicException()
            : base(ErrorMessages.NotSupportedIntrinsicImplementation0)
        { }

        /// <summary>
        /// Constructs a new intrinsic exception.
        /// </summary>
        /// <param name="intrinsicMethod">
        /// The IR method that could not be implemented.
        /// </param>
        public NotSupportedIntrinsicException(Method intrinsicMethod)
            : this(
                  intrinsicMethod.HasSource
                  ? intrinsicMethod.Source.Name
                  : intrinsicMethod.Name)
        { }

        /// <summary>
        /// Constructs a new intrinsic exception.
        /// </summary>
        /// <param name="intrinsicName">The name of the not supported intrinsic.</param>
        public NotSupportedIntrinsicException(string intrinsicName)
            : base(
                  string.Format(
                      ErrorMessages.NotSupportedIntrinsicImplementation1,
                      intrinsicName))
        { }

        /// <summary>
        /// Constructs a new intrinsic exception.
        /// </summary>
        /// <param name="message">The detailed error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public NotSupportedIntrinsicException(string message, Exception innerException)
            : base(message, innerException)
        { }

        /// <summary>
        /// Constructs a new intrinsic exception.
        /// </summary>
#if NET8_0_OR_GREATER
        [Obsolete("SYSLIB0050: Formatter-based serialization is obsolete")]
#endif
        private NotSupportedIntrinsicException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        { }
    }
}
