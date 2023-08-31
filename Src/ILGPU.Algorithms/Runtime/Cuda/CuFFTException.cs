// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuFFTException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents an exception that can be thrown by the cuFFT library.
    /// </summary>
    [Serializable]
    public sealed class CuFFTException : Exception
    {
        #region Instance

        /// <summary>
        /// Constructs a new cuFFT exception.
        /// </summary>
        public CuFFTException()
            : this(CuFFTResult.CUFFT_NOT_SUPPORTED)
        { }

        /// <summary>
        /// Constructs a new cuFFT exception.
        /// </summary>
        /// <param name="errorCode">The runtime error.</param>
        public CuFFTException(CuFFTResult errorCode)
            : base()
        {
            Error = errorCode;
        }

        /// <summary cref="Exception(string)"/>
        public CuFFTException(string message)
            : base(message)
        {
            Error = CuFFTResult.CUFFT_NOT_SUPPORTED;
        }

        /// <summary cref="Exception(string, Exception)"/>
        public CuFFTException(string message, Exception innerException)
            : base(message, innerException)
        {
            Error = CuFFTResult.CUFFT_NOT_SUPPORTED;
        }

        /// <summary cref="Exception(SerializationInfo, StreamingContext)"/>
        private CuFFTException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Error = (CuFFTResult)info.GetInt32("Error");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the error.
        /// </summary>
        public CuFFTResult Error { get; }

        #endregion

        #region Methods

        /// <summary cref="Exception.GetObjectData(SerializationInfo, StreamingContext)"/>
        public override void GetObjectData(
            SerializationInfo info,
            StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("Error", (int)Error);
        }

        /// <summary>
        /// Checks the given status and throws an exception in case of an error.
        /// </summary>
        /// <param name="errorCode">The error code to check.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfFailed(CuFFTResult errorCode)
        {
            if (errorCode != CuFFTResult.CUFFT_SUCCESS)
                throw new CuFFTException(errorCode);
        }

        #endregion
    }
}
