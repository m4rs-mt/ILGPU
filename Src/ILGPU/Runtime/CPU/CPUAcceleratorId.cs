// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: AcceleratorId.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a single CPU accelerator reference.
    /// </summary>
    public sealed class CPUAcceleratorId : AcceleratorId
    {
        #region Static

        /// <summary>
        /// The main CPU accelerator id instance.
        /// </summary>
        public static CPUAcceleratorId Instance { get; } =
            new CPUAcceleratorId();

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new CPU accelerator instance.
        /// </summary>
        private CPUAcceleratorId()
            : base(AcceleratorType.CPU)
        { }

        #endregion
    }
}
