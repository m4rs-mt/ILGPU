// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents an OpenCL exception that can be thrown by the OpenCL runtime.
    /// </summary>
    [Serializable]
    public sealed class CLException : AcceleratorException
    {
        #region Instance

        /// <summary>
        /// Constructs a new OpenCL exception.
        /// </summary>
        public CLException()
            : this(CLError.CL_INVALID_OPERATION)
        { }

        /// <summary>
        /// Constructs a new OpenCL exception.
        /// </summary>
        /// <param name="errorCode">The OpenCL runtime error.</param>
        public CLException(CLError errorCode)
            : base()
        {
            Error = errorCode;
        }

        /// <summary>
        /// Constructs a new OpenCL exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public CLException(string message)
            : base(message)
        { }

        /// <summary>
        /// Constructs a new OpenCL exception.
        /// </summary>
        /// <param name="message">
        /// The error message that explains the reason for the exception.
        /// </param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception, or a null reference
        /// if no inner exception is specified.
        /// </param>
        public CLException(string message, Exception innerException)
            : base(message, innerException)
        { }

        /// <summary cref="Exception(SerializationInfo, StreamingContext)"/>
        private CLException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Error = (CLError)info.GetInt32("Error");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the error.
        /// </summary>
        public CLError Error { get; }

        /// <summary>
        /// Returns <see cref="AcceleratorType.OpenCL"/>.
        /// </summary>
        public override AcceleratorType AcceleratorType => AcceleratorType.OpenCL;

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
        /// Checks the given status and throws an exception in case of an error if
        /// <paramref name="disposing"/> is set to true. If it is set to false, the
        /// exception will be suppressed in all cases.
        /// </summary>
        /// <param name="disposing">
        /// True, if this function has been called by the dispose method, false otherwise.
        /// </param>
        /// <param name="clStatus">The OpenCL error code to check.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void VerifyDisposed(bool disposing, CLError clStatus)
        {
            if (disposing)
                ThrowIfFailed(clStatus);
        }

        /// <summary>
        /// Checks the given status and throws an exception in case of an error.
        /// </summary>
        /// <param name="clStatus">The OpenCL error code to check.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfFailed(CLError clStatus)
        {
            if (clStatus != CLError.CL_SUCCESS)
                throw new CLException(clStatus);
        }

        #endregion
    }
}
