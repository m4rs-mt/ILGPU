// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// Represents a velocity stream.
    /// </summary>
    sealed class VelocityStream : AcceleratorStream
    {
        #region Instance

        /// <summary>
        /// Constructs a new Velocity stream.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        internal VelocityStream(Accelerator accelerator)
            : base(accelerator)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Does not perform any operation.
        /// </summary>
        public override void Synchronize() { }

        /// <inheritdoc/>
        protected override ProfilingMarker AddProfilingMarkerInternal()
        {
            using var binding = Accelerator.BindScoped();
            return new VelocityProfilingMarker(Accelerator);
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


