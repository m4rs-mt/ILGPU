// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InvalidCodeGenerationException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
#if !NET8_0_OR_GREATER
using System.Runtime.Serialization;
#endif

namespace ILGPU.Backends
{
    /// <summary>
    /// An exception that is thrown in case of a fatal error in a backend.
    /// </summary>
#if !NET8_0_OR_GREATER
    [Serializable]
#endif
    public sealed class InvalidCodeGenerationException : Exception
    {
        /// <summary>
        /// Constructs a new code generation exception.
        /// </summary>
        public InvalidCodeGenerationException()
            : base(RuntimeErrorMessages.InvalidCodeGenerationOperation0)
        { }

        /// <summary>
        /// Constructs a new code generation exception.
        /// </summary>
        /// <param name="message">The detailed error message.</param>
        public InvalidCodeGenerationException(string message)
            : base(
                  string.Format(
                      RuntimeErrorMessages.InvalidCodeGenerationOperation1,
                      message))
        { }

        /// <summary>
        /// Constructs a new code generation exception.
        /// </summary>
        /// <param name="message">The detailed error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public InvalidCodeGenerationException(string message, Exception innerException)
            : base(message, innerException)
        { }

#if !NET8_0_OR_GREATER
        /// <summary>
        /// Constructs a new code generation exception.
        /// </summary>
        private InvalidCodeGenerationException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        { }
#endif
    }
}
