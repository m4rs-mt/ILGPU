// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: IMemoryBuffer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents the base interface for all memory buffers.
    /// </summary>
    public interface IMemoryBuffer
    {
        /// <summary>
        /// Returns the associated accelerator.
        /// </summary>
        Accelerator Accelerator { get; }

        /// <summary>
        /// Returns the native pointer.
        /// </summary>
        IntPtr Pointer { get; }

        /// <summary>
        /// Returns the length of this buffer.
        /// </summary>
        int Length { get; }
    }
}
