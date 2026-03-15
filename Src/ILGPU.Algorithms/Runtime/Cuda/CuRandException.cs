// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuRandException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
#if !NET8_0_OR_GREATER
using System.Runtime.Serialization;
using System.Security.Permissions;
#endif

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents an CuRand exception that can be thrown by the CuRand library.
    /// </summary>
#if !NET8_0_OR_GREATER
    [Serializable]
#endif
    public sealed class CuRandException : Exception
    {
        #region Instance

        /// <summary>
        /// Constructs a new CuRand exception.
        /// </summary>
        public CuRandException()
            : this(CuRandStatus.CURAND_STATUS_NOT_INITIALIZED)
        { }

        /// <summary>
        /// Constructs a new CuRand exception.
        /// </summary>
        /// <param name="errorCode">The CuRand runtime error.</param>
        public CuRandException(CuRandStatus errorCode)
            : base()
        {
            Error = errorCode;
        }

        /// <summary cref="Exception(string)"/>
        public CuRandException(string message)
            : base(message)
        {
            Error = CuRandStatus.CURAND_STATUS_NOT_INITIALIZED;
        }

        /// <summary cref="Exception(string, Exception)"/>
        public CuRandException(string message, Exception innerException)
            : base(message, innerException)
        {
            Error = CuRandStatus.CURAND_STATUS_NOT_INITIALIZED;
        }

#if !NET8_0_OR_GREATER
        /// <summary cref="Exception(SerializationInfo, StreamingContext)"/>
        private CuRandException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Error = (CuRandStatus)info.GetInt32("Error");
        }
#endif

        #endregion

        #region Properties

        /// <summary>
        /// Returns the error.
        /// </summary>
        public CuRandStatus Error { get; }

        #endregion

        #region Methods

#if !NET8_0_OR_GREATER
        /// <summary cref="Exception.GetObjectData(SerializationInfo, StreamingContext)"/>
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(
            SerializationInfo info,
            StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("Error", (int)Error);
        }
#endif

        /// <summary>
        /// Checks the given status and throws an exception in case of an error.
        /// </summary>
        /// <param name="errorCode">The CuRand error code to check.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfFailed(CuRandStatus errorCode)
        {
            if (errorCode != CuRandStatus.CURAND_STATUS_SUCCESS)
                throw new CuRandException(errorCode);
        }

        #endregion
    }
}
