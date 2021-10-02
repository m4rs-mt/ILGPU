// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaAcceleratorFlags.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents the accelerator flags for a Cuda accelerator.
    /// </summary>
    [Flags]
    public enum CudaAcceleratorFlags
    {
        /// <summary>
        /// Automatic scheduling (default).
        /// </summary>
        ScheduleAuto = 0,

        /// <summary>
        /// Spin scheduling.
        /// </summary>
        ScheduleSpin = 1,

        /// <summary>
        /// Yield scheduling
        /// </summary>
        ScheduleYield = 2,

        /// <summary>
        /// Blocking synchronization as default scheduling.
        /// </summary>
        ScheduleBlockingSync = 4,
    }
}
