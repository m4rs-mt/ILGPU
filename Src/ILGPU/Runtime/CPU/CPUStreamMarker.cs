// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUStreamMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a marker used in CPU streams.
    /// </summary>
    internal sealed class CPUStreamMarker : StreamMarker
    {
        #region Instance

        internal CPUStreamMarker(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override void Synchronize() { }

        /// <inheritdoc/>
        protected override void DisposeAcceleratorObject(bool disposing) { }

        /// <inheritdoc/>
        public unsafe override void Record(AcceleratorStream stream)
        {
            if (stream is not CPUStream)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedAcceleratorStream);
            }
        }

        #endregion
    }
}
