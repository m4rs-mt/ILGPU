// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: InvalidKernelOperationException.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU
{
    /// <summary>
    /// An exception that is thrown when an ILGPU kernel method is called from the
    /// managed CPU side instead of a kernel.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors")]
    [Serializable]
    public sealed class InvalidKernelOperationException : InvalidOperationException
    {
        /// <summary>
        /// Constructs a new exception.
        /// </summary>
        public InvalidKernelOperationException()
            : base("This operation can only be called from an ILGPU kernel")
        { }
    }
}
