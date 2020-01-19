// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2020 ILGPU Algorithms Project
//                                www.ilgpu.net
//
// File: CuBlasException.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents an CuBlas exception that can be thrown by the CuBlas library.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors")]
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
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
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
