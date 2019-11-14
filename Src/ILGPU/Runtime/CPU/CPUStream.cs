﻿// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CPUStream.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a CPU stream.
    /// </summary>
    sealed class CPUStream : AcceleratorStream
    {
        #region Instance

        /// <summary>
        /// Constructs a new CPU stream.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        internal CPUStream(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion

        #region Methods

        /// <summary cref="AcceleratorStream.Synchronize"/>
        public override void Synchronize()
        { }

        #endregion
    }
}
