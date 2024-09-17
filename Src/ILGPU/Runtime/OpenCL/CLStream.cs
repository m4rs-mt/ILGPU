// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics.CodeAnalysis;
using static ILGPU.Runtime.OpenCL.CLAPI;

namespace ILGPU.Runtime.OpenCL
{
    /// <summary>
    /// Represents an OpenCL stream.
    /// </summary>
    [SuppressMessage(
        "Microsoft.Naming",
        "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
    public sealed class CLStream : AcceleratorStream
    {
        #region Instance

        private IntPtr queuePtr;
        private readonly bool responsibleForHandle;

        internal CLStream(
            Accelerator accelerator,
            IntPtr ptr,
            bool responsible)
            : base(accelerator)
        {
            queuePtr = ptr;
            responsibleForHandle = responsible;
        }

        internal CLStream(CLAccelerator accelerator)
            : base(accelerator)
        {
            CLCommandQueueProperties properties =
                Accelerator.Context.Properties.EnableProfiling
                ? CLCommandQueueProperties.CL_QUEUE_PROFILING_ENABLE
                : default;
            CLException.ThrowIfFailed(
                CurrentAPI.CreateCommandQueue(
                    accelerator.PlatformVersion,
                    accelerator.DeviceId,
                    accelerator.NativePtr,
                    properties,
                    out queuePtr));
            responsibleForHandle = true;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the native OpenCL command queue.
        /// </summary>
        public IntPtr CommandQueue => queuePtr;

        #endregion

        #region Methods

        /// <summary cref="AcceleratorStream.Synchronize"/>
        public override void Synchronize() =>
            CLException.ThrowIfFailed(
                CurrentAPI.FinishCommandQueue(queuePtr));

        /// <inheritdoc/>
        protected unsafe override ProfilingMarker AddProfilingMarkerInternal()
        {
            using var binding = Accelerator.BindScoped();

            IntPtr* profilingEvent = stackalloc IntPtr[1];
            CLException.ThrowIfFailed(
                CurrentAPI.EnqueueBarrierWithWaitList(
                    queuePtr,
                    Array.Empty<IntPtr>(),
                    profilingEvent));

            // WORKAROUND: The OpenCL event needs to be awaited now, otherwise
            // it does not contain the correct timing - it appears to have the timing
            // of whenever it gets awaited.
            var marker = new CLProfilingMarker(Accelerator, *profilingEvent);
            marker.Synchronize();
            return marker;
        }

        /// <inheritdoc/>
        protected unsafe override void WaitForStreamMarkerInternal(
            StreamMarker streamMarker)
        {
            if (streamMarker is not CLStreamMarker clStreamMarker ||
                clStreamMarker.EventPtr == IntPtr.Zero)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedAcceleratorStreamMarker);
            }

            using var binding = BindScoped();
            var streamEvents = new IntPtr[] { clStreamMarker.EventPtr };
            CLException.ThrowIfFailed(
                CurrentAPI.EnqueueBarrierWithWaitList(
                    CommandQueue,
                    streamEvents,
                    null));
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes this OpenCL stream.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (!responsibleForHandle || queuePtr == IntPtr.Zero)
                return;

            CLException.VerifyDisposed(
                disposing,
                CurrentAPI.ReleaseCommandQueue(queuePtr));
            queuePtr = IntPtr.Zero;
        }

        #endregion
    }
}
