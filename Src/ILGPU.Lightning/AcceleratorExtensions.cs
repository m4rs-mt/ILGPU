// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: AcceleratorExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents extension methods for ILGPU accelerators.
    /// </summary>
    public static class AcceleratorExtensions
    {
        /// <summary>
        /// Creates a new lighting context for the given accelerator.
        /// </summary>
        /// <param name="accelerator">The target accelerator to create the lighting context for.</param>
        /// <returns>The created lighting context.</returns>
        public static LightningContext CreateLightningContext(this Accelerator accelerator)
        {
            return new LightningContext(accelerator);
        }
    }
}
