// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaCacheConfiguration.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a cache configuration of a device.
    /// </summary>
    public enum CudaCacheConfiguration
    {
        /// <summary>
        /// The default cache configuration.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Prefer shared cache.
        /// </summary>
        PreferShared = 1,

        /// <summary>
        /// Prefer L1 cache.
        /// </summary>
        PreferL1 = 2,

        /// <summary>
        /// Prefer shared or L1 cache.
        /// </summary>
        PreferEqual = 3
    }
}
