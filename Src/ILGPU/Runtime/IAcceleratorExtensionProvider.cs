// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: IAcceleratorExtensionProvider.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using ILGPU.Runtime.Velocity;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents a generic accelerator-extension provider.
    /// </summary>
    /// <typeparam name="TExtension">The type of the extension to be created.</typeparam>
    public interface IAcceleratorExtensionProvider<TExtension>
    {
        /// <summary>
        /// Creates an extension for a CPU accelerator.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        /// <returns>The created extension.</returns>
        TExtension CreateCPUExtension(CPUAccelerator accelerator);

        /// <summary>
        /// Creates an extension for a Velocity accelerator.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        /// <returns>The created extension.</returns>
        TExtension CreateVelocityExtension(VelocityAccelerator accelerator);

        /// <summary>
        /// Creates an extension for a Cuda accelerator.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        /// <returns>The created extension.</returns>
        TExtension CreateCudaExtension(CudaAccelerator accelerator);

        /// <summary>
        /// Creates an extension for an OpenCL accelerator.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        /// <returns>The created extension.</returns>
        TExtension CreateOpenCLExtension(CLAccelerator accelerator);
    }
}
