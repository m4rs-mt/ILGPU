// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityArgumentMapper.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.PTX;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// Constructs mappings Velocity kernels.
    /// </summary>
    /// <remarks>The current velocity backend uses the PTX argument mapper.</remarks>
    sealed class VelocityArgumentMapper : PTXArgumentMapper
    {
        #region Instance

        /// <summary>
        /// Constructs a new IL argument mapper.
        /// </summary>
        /// <param name="context">The current context.</param>
        public VelocityArgumentMapper(Context context)
            : base(context)
        { }

        #endregion
    }
}
