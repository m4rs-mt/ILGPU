// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2020 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: CuBlasException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents an CuBlas exception that can be thrown by the CuBlas library.
    /// </summary>
    [Serializable]
    public sealed class CuBlasException : Exception
    {
        #region Instance

        /// <summary>
        /// Constructs a new CuBlas exception.
        /// </summary>
        public CuBlasException()
            : this(CuBlasStatus.CUBLAS_STATUS_NOT_SUPPORTED)
        { }

        /// <summary>
        /// Constructs a new CuBlas exception.
        /// </summary>
        /// <param name="errorCode">The CuBlas runtime error.</param>
        public CuBlasException(CuBlasStatus errorCode)
            : base()
        {
            Error = errorCode;
        }

        /// <summary cref="Exception(string)"/>
        public CuBlasException(string message)
            : base(message)
        {
            Error = CuBlasStatus.CUBLAS_STATUS_NOT_SUPPORTED;
        }

        /// <summary cref="Exception(string, Exception)"/>
        public CuBlasException(string message, Exception innerException)
            : base(message, innerException)
        {
            Error = CuBlasStatus.CUBLAS_STATUS_NOT_SUPPORTED;
        }

        /// <summary cref="Exception(SerializationInfo, StreamingContext)"/>
        private CuBlasException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Error = (CuBlasStatus)info.GetInt32("Error");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the error.
        /// </summary>
        public CuBlasStatus Error { get; }

        #endregion

        #region Methods

        /// <summary cref="Exception.GetObjectData(SerializationInfo, StreamingContext)"/>
#if !NET5_0_OR_GREATER
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
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
        /// <param name="errorCode">The CuBlas error code to check.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfFailed(CuBlasStatus errorCode)
        {
            if (errorCode != CuBlasStatus.CUBLAS_STATUS_SUCCESS)
                throw new CuBlasException(errorCode);
        }

        #endregion
    }
}
