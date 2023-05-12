// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaDeviceOverride.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents overridable settings of a Cuda device.
    /// </summary>
    public sealed class CudaDeviceOverride
    {
        /// <summary>
        /// The Cuda device to configure.
        /// </summary>
        public CudaDevice Device { get; }

        /// <summary>
        /// Forces the Cuda device to use the specified Instruction Set.
        /// </summary>
        public CudaInstructionSet? InstructionSet { get; set; }

        /// <summary>
        /// Constructs a new instance with the overridable settings.
        /// </summary>
        /// <param name="device">The Cuda device.</param>
        internal CudaDeviceOverride(CudaDevice device)
        {
            Device = device;
            InstructionSet = device.InstructionSet;
        }

        /// <summary>
        /// Applies all the overridden settings to the Cuda device.
        /// </summary>
        internal void ApplyOverrides()
        {
            if (InstructionSet.HasValue)
                Device.InstructionSet = InstructionSet;
        }
    }
}
