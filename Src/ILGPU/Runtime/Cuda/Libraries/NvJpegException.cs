// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvJpegException.cs
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
    /// Represents an exception that can be thrown by the nvJPEG library.
    /// </summary>
    [Serializable]
    public sealed class NvJpegException : Exception
    {
        #region Instance

        /// <summary>
        /// Constructs a new nvJPEG exception.
        /// </summary>
        public NvJpegException()
            : this(NvJpegStatus.NVJPEG_STATUS_NOT_INITIALIZED)
        { }

        /// <summary>
        /// Constructs a new nvJPEG exception.
        /// </summary>
        /// <param name="errorCode">The nvJPEG runtime error.</param>
        public NvJpegException(NvJpegStatus errorCode)
            : base()
        {
            Error = errorCode;
        }

        /// <summary cref="Exception(string)"/>
        public NvJpegException(string message)
            : base(message)
        {
            Error = NvJpegStatus.NVJPEG_STATUS_NOT_INITIALIZED;
        }

        /// <summary cref="Exception(string, Exception)"/>
        public NvJpegException(string message, Exception innerException)
            : base(message, innerException)
        {
            Error = NvJpegStatus.NVJPEG_STATUS_NOT_INITIALIZED;
        }

        /// <summary cref="Exception(SerializationInfo, StreamingContext)"/>
#if NET8_0_OR_GREATER
        [Obsolete("SYSLIB0050: Formatter-based serialization is obsolete")]
#endif
        private NvJpegException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Error = (NvJpegStatus)info.GetInt32("Error");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the error.
        /// </summary>
        public NvJpegStatus Error { get; }

        #endregion

        #region Methods

        /// <summary cref="Exception.GetObjectData(SerializationInfo, StreamingContext)"/>
#if NET8_0_OR_GREATER
        [Obsolete("SYSLIB0050: Formatter-based serialization is obsolete")]
#endif
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
        /// <param name="errorCode">The nvJPEG error code to check.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfFailed(NvJpegStatus errorCode)
        {
            if (errorCode != NvJpegStatus.NVJPEG_STATUS_SUCCESS)
                throw new NvJpegException(errorCode);
        }

        #endregion
    }
}
