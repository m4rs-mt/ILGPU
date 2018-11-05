// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
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
    /// Represents a shared-memory configuration of a device.
    /// </summary>
    public enum CudaSharedMemoryConfiguration
    {
        /// <summary>
        /// The default shared-memory configuration.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Setup a bank size of 4 byte.
        /// </summary>
        FourByteBankSize = 1,

        /// <summary>
        /// Setup a bank size of 8 byte.
        /// </summary>
        EightByteBankSize = 2
    }
}
