// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CudaAccelerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------


namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents the type of a device pointer.
    /// </summary>
    public enum CudaMemoryType
    {
        /// <summary>
        /// Represents no known memory type.
        /// </summary>
        None = 0,
        /// <summary>
        /// Represents a host pointer.
        /// </summary>
        Host = 1,
        /// <summary>
        /// Represents a device pointer.
        /// </summary>
        Device = 2,
        /// <summary>
        /// Represents a pointer to a Cuda array.
        /// </summary>
        Array = 3,
        /// <summary>
        /// Represents a unified-memory pointer.
        /// </summary>
        Unified = 4,
    }
}
