// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;

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

        /// <summary>
        /// Does not perform any operation.
        /// </summary>
        public override void Synchronize() { }

        /// <inheritdoc/>
        protected unsafe override ProfilingMarker AddProfilingMarkerInternal()
        {
            using var binding = Accelerator.BindScoped();
            return new CPUProfilingMarker(Accelerator);
        }

        /// <inheritdoc/>
        protected unsafe override void WaitForStreamMarkerInternal(
            StreamMarker streamMarker)
        {
            if (streamMarker is not CPUStreamMarker)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedAcceleratorStreamMarker);
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Does not perform any operation.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing) { }

        #endregion
    }
}
