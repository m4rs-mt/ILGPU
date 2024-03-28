// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityStreamMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// Represents a marker used in velocity streams.
    /// </summary>
    internal sealed class VelocityStreamMarker : StreamMarker
    {
        #region Instance

        internal VelocityStreamMarker(Accelerator accelerator)
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
            if (stream is not VelocityStream)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedAcceleratorStream);
            }
        }

        #endregion
    }
}
