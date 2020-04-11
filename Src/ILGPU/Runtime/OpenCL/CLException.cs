// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.OpenCL.API;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents an OpenCL exception that can be thrown by the OpenCL runtime.
    /// </summary>
    [SuppressMessage(
        "Microsoft.Design",
        "CA1032:ImplementStandardExceptionConstructors")]
    [Serializable]
    public sealed class CLException : Exception
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

        #endregion

        #region Methods

        /// <summary cref="Exception.GetObjectData(SerializationInfo, StreamingContext)"/>
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(
            SerializationInfo info,StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("Error", (int)Error);
        }

        /// <summary>
        /// Checks the given status and throws an exception in case of an error.
        /// </summary>
        /// <param name="errorCode">The OpenCL error code to check.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfFailed(CLError errorCode)
        {
            if (errorCode != CLError.CL_SUCCESS)
                throw new CLException(errorCode);
        }

        #endregion
    }
}
