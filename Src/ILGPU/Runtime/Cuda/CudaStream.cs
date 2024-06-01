// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics.CodeAnalysis;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents a Cuda stream.
    /// </summary>
    [SuppressMessage(
        "Microsoft.Naming",
        "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
    public sealed class CudaStream : AcceleratorStream
    {
        #region Instance

        private IntPtr streamPtr;
        private readonly bool responsibleForHandle;

        /// <summary>
        /// Constructs a new Cuda stream from the given native pointer.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="ptr">The native stream pointer.</param>
        /// <param name="responsible">
        /// Whether ILGPU is responsible of disposing this stream.
        /// </param>
        internal CudaStream(Accelerator accelerator, IntPtr ptr, bool responsible)
            : base(accelerator)
        {
            streamPtr = ptr;
            responsibleForHandle = responsible;
        }

        /// <summary>
        /// Constructs a new Cuda stream with given <see cref="StreamFlags"/>.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="flag">
        /// Stream flag to use. Allows blocking and non-blocking streams.
        /// </param>
        internal CudaStream(Accelerator accelerator, StreamFlags flag)
            : base(accelerator)
        {
            CudaException.ThrowIfFailed(
                CurrentAPI.CreateStream(
                    out streamPtr,
                    flag));
            responsibleForHandle = true;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying native Cuda stream.
        /// </summary>
        public IntPtr StreamPtr => streamPtr;

        #endregion

        #region Methods

        /// <summary cref="AcceleratorStream.Synchronize"/>
        public override void Synchronize()
        {
            var binding = Accelerator.BindScoped();

            CudaException.ThrowIfFailed(
                CurrentAPI.SynchronizeStream(streamPtr));

            binding.Recover();
        }

        /// <inheritdoc/>
        protected override ProfilingMarker AddProfilingMarkerInternal()
        {
            using var binding = Accelerator.BindScoped();
            var profilingMarker = new CudaProfilingMarker(Accelerator);

            CudaException.ThrowIfFailed(
                CurrentAPI.RecordEvent(profilingMarker.EventPtr, StreamPtr));
            return profilingMarker;
        }

        /// <inheritdoc/>
        protected unsafe override void WaitForStreamMarkerInternal(
            StreamMarker streamMarker)
        {
            if (streamMarker is not CudaStreamMarker cudaStreamMarker)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedAcceleratorStreamMarker);
            }

            using var binding = BindScoped();
            CudaException.ThrowIfFailed(
                CurrentAPI.WaitForEvent(
                    StreamPtr,
                    cudaStreamMarker.EventPtr,
                    IntPtr.Zero));
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes this Cuda stream.
        /// </summary>
        protected override void DisposeAcceleratorObject(bool disposing)
        {
            if (!responsibleForHandle || streamPtr == IntPtr.Zero)
                return;

            CudaException.VerifyDisposed(
                disposing,
                CurrentAPI.DestroyStream(streamPtr));
            streamPtr = IntPtr.Zero;
        }

        #endregion
    }
}
