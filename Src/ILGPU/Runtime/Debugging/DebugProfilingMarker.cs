// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUProfilingMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Represents a point-in-time marker used in CPU profiling.
    /// </summary>
    internal sealed class CPUProfilingMarker : ProfilingMarker
    {
        #region Instance

        internal CPUProfilingMarker(Accelerator accelerator)
            : base(accelerator)
        {
            Timestamp = DateTime.UtcNow;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The timestamp this profiling marker was created.
        /// </summary>
        public DateTime Timestamp { get; private set; }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override void Synchronize() { }

        /// <inheritdoc/>
        public override TimeSpan MeasureFrom(ProfilingMarker marker)
        {
            using var binding = Accelerator.BindScoped();

            return (marker is CPUProfilingMarker startMarker)
                ? Timestamp - startMarker.Timestamp
                : throw new ArgumentException(
                    string.Format(
                        RuntimeErrorMessages.InvalidProfilingMarker,
                        GetType().Name,
                        marker.GetType().Name),
                    nameof(marker));
        }

        /// <inheritdoc/>
        protected override void DisposeAcceleratorObject(bool disposing) { }

        #endregion
    }
}
