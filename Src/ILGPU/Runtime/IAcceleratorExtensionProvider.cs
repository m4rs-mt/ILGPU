// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: IAcceleratorExtensionProvider.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;

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
        /// Creates an extension for a Cuda accelerator.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        /// <returns>The created extension.</returns>
        TExtension CreateCudaExtension(CudaAccelerator accelerator);
    }
}
