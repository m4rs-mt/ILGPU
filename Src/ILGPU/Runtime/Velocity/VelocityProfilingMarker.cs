// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityProfilingMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// Represents a point-in-time marker used in Velocity profiling.
    /// </summary>
    internal sealed class VelocityProfilingMarker : ProfilingMarker
    {
        #region Instance

        internal VelocityProfilingMarker(Accelerator accelerator)
            : base(accelerator)
        {
#if NET7_0_OR_GREATER
            Timestamp = Stopwatch.GetTimestamp();
#else
            Timestamp = DateTime.UtcNow.ToBinary();
#endif
        }

        #endregion

        #region Properties

        /// <summary>
        /// The timestamp this profiling marker was created.
        /// </summary>
        public long Timestamp { get; private set; }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override void Synchronize() { }

        /// <inheritdoc/>
        public override TimeSpan MeasureFrom(ProfilingMarker marker)
        {
            using var binding = Accelerator.BindScoped();

            return (marker is VelocityProfilingMarker startMarker)
#if NET7_0_OR_GREATER
                ? Stopwatch.GetElapsedTime(startMarker.Timestamp, Timestamp)
#else
                ? DateTime.FromBinary(Timestamp) -
                    DateTime.FromBinary(startMarker.Timestamp)
#endif
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
