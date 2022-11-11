// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvJpegLibrary.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda.API;
using ILGPU.Util;
using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Represents an NvJpeg library.
    /// </summary>
    [CLSCompliant(false)]
    public sealed partial class NvJpegLibrary : DisposeBase
    {
        /// <summary>
        /// Constructs a new instance to wrap an NvJpeg library.
        /// </summary>
        public NvJpegLibrary(NvJpegAPI api, IntPtr libHandle)
        {
            API = api;
            LibHandle = libHandle;
        }

        /// <summary>
        /// The underlying API wrapper.
        /// </summary>
        public NvJpegAPI API { get; }

        /// <summary>
        /// The native handle.
        /// </summary>
        public IntPtr LibHandle { get; private set; }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                NvJpegException.ThrowIfFailed(
                    API.Destroy(LibHandle));
                LibHandle = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Creates a new NvJpeg state instance.
        /// </summary>
        public NvJpegState CreateState()
        {
            NvJpegException.ThrowIfFailed(
                API.JpegStateCreate(LibHandle, out IntPtr stateHandle));
            return new NvJpegState(API, stateHandle);
        }

        /// <inheritdoc cref="NvJpegAPI.GetImageInfo(
        ///     IntPtr,
        ///     ReadOnlySpan{byte},
        ///     out int,
        ///     out NvJpegChromaSubsampling,
        ///     out int[],
        ///     out int[])"/>
        public unsafe NvJpegStatus GetImageInfo(
            ReadOnlySpan<byte> imageBytes,
            out int numComponents,
            out NvJpegChromaSubsampling subsampling,
            out int[] widths,
            out int[] heights) =>
            API.GetImageInfo(
                LibHandle,
                imageBytes,
                out numComponents,
                out subsampling,
                out widths,
                out heights);

        /// <inheritdoc cref="NvJpegAPI.Decode(
        ///     IntPtr,
        ///     IntPtr,
        ///     ReadOnlySpan{byte},
        ///     NvJpegOutputFormat,
        ///     in NvJpegImage, CudaStream)"/>
        public unsafe NvJpegStatus Decode(
            NvJpegState state,
            ReadOnlySpan<byte> imageBytes,
            NvJpegOutputFormat outputFormat,
            in NvJpegImage destination,
            CudaStream stream) =>
            API.Decode(
                LibHandle,
                state.StateHandle,
                imageBytes,
                outputFormat,
                destination,
                stream);

        /// <inheritdoc cref="NvJpegAPI.Decode(
        ///     IntPtr,
        ///     IntPtr,
        ///     ReadOnlySpan{byte},
        ///     NvJpegOutputFormat,
        ///     in NvJpegImage)"/>
        public NvJpegStatus Decode(
            NvJpegState state,
            ReadOnlySpan<byte> imageBytes,
            NvJpegOutputFormat outputFormat,
            in NvJpegImage destination) =>
            API.Decode(
                LibHandle,
                state.StateHandle,
                imageBytes,
                outputFormat,
                destination);
    }
}
