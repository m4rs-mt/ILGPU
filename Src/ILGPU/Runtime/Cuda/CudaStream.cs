// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaStream.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

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

        /// <summary>
        /// Begins capturing the work submitted to this stream into a
        /// <see cref="CudaGraph"/>. Submissions made between this call and
        /// <see cref="EndCapture"/> are recorded rather than executed. The default stream
        /// (the NULL stream) cannot be captured; create a dedicated stream via
        /// <see cref="Accelerator.CreateStream()"/> to capture on.
        /// </summary>
        /// <param name="mode">The capture mode (see
        /// <see cref="CudaStreamCaptureMode"/>).</param>
        public void BeginCapture(
            CudaStreamCaptureMode mode = CudaStreamCaptureMode.Global)
        {
            using var binding = Accelerator.BindScoped();

            CudaException.ThrowIfFailed(
                CurrentAPI.BeginStreamCapture(streamPtr, mode));
        }

        /// <summary>
        /// Ends the capture started by
        /// <see cref="BeginCapture(CudaStreamCaptureMode)"/> and returns the recorded
        /// graph. The caller owns the returned <see cref="CudaGraph"/> and is responsible
        /// for disposing it.
        /// </summary>
        /// <returns>The captured graph.</returns>
        public CudaGraph EndCapture()
        {
            using var binding = Accelerator.BindScoped();

            CudaException.ThrowIfFailed(
                CurrentAPI.EndStreamCapture(streamPtr, out var graphPtr));
            return new CudaGraph(Accelerator, graphPtr);
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
