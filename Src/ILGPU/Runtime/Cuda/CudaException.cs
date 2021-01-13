﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CudaException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Permissions;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a Cuda exception that can be thrown by the Cuda runtime.
    /// </summary>
    [Serializable]
    public sealed class CudaException : AcceleratorException
    {
        #region Instance

        /// <summary>
        /// Constructs a new Cuda exception.
        /// </summary>
        public CudaException()
        {
            Error = "N/A";
        }

        /// <summary>
        /// Constructs a new Cuda exception.
        /// </summary>
        /// <param name="error">The Cuda runtime error.</param>
        private CudaException(CudaError error)
            : base(CurrentAPI.GetErrorString(error))
        {
            Error = error.ToString();
        }

        /// <summary cref="Exception(SerializationInfo, StreamingContext)"/>
        private CudaException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Error = info.GetString("Error");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the error.
        /// </summary>
        public string Error { get; }

        /// <summary>
        /// Returns <see cref="AcceleratorType.Cuda"/>.
        /// </summary>
        public override AcceleratorType AcceleratorType => AcceleratorType.Cuda;

        #endregion

        #region Methods

        /// <summary cref="Exception.GetObjectData(
        /// SerializationInfo, StreamingContext)"/>
#if !NET5_0
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
#endif
        public override void GetObjectData(
            SerializationInfo info,
            StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("Error", Error);
        }

        /// <summary>
        /// Checks the given status and throws an exception in case of an error if
        /// <paramref name="disposing"/> is set to true. If it is set to false, the
        /// exception will be suppressed in all cases.
        /// </summary>
        /// <param name="disposing">
        /// True, if this function has been called by the dispose method, false otherwise.
        /// </param>
        /// <param name="cudaStatus">The Cuda status to check.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void VerifyDisposed(bool disposing, CudaError cudaStatus)
        {
            if (disposing)
                ThrowIfFailed(cudaStatus);
        }

        /// <summary>
        /// Checks the given status and throws an exception in case of an error.
        /// </summary>
        /// <param name="cudaStatus">The Cuda status to check.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfFailed(CudaError cudaStatus)
        {
            if (cudaStatus != CudaError.CUDA_SUCCESS)
                throw new CudaException(cudaStatus);
        }

        #endregion
    }
}
