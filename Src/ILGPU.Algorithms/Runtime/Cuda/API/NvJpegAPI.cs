// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2021 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: NvJpegAPI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.Cuda.API
{
    /// <summary>
    /// An implementation of the nvJpeg API.
    /// </summary>
    [CLSCompliant(false)]
    public abstract partial class NvJpegAPI
    {
        #region Static

        /// <summary>
        /// Creates a new API wrapper.
        /// </summary>
        /// <param name="version">The nvJPEG version to use.</param>
        /// <returns>The created API wrapper.</returns>
        public static NvJpegAPI Create(NvJpegAPIVersion? version) =>
            version.HasValue
            ? CreateInternal(version.Value)
            : CreateLatest();

        /// <summary>
        /// Creates a new API wrapper using the latest installed version.
        /// </summary>
        /// <returns>The created API wrapper.</returns>
        private static NvJpegAPI CreateLatest()
        {
            Exception firstException = null;
            var versions = Enum.GetValues(typeof(CuFFTAPIVersion));

            for (var i = versions.Length - 1; i >= 0; i--)
            {
                var version = (NvJpegAPIVersion)versions.GetValue(i);
                var api = CreateInternal(version);
                if (api is null)
                    continue;

                try
                {
                    var status = api.GetProperty(
                        LibraryPropertyType.MAJOR_VERSION,
                        out _);
                    if (status == NvJpegStatus.NVJPEG_STATUS_SUCCESS)
                        return api;
                }
                catch (Exception ex) when (
                    ex is DllNotFoundException ||
                    ex is EntryPointNotFoundException)
                {
                    firstException ??= ex;
                }
            }

            throw firstException ?? new DllNotFoundException(nameof(NvJpegAPI));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Retrieves information about the supplied JPEG.
        /// </summary>
        /// <param name="libHandle">The NvJPEG library handle.</param>
        /// <param name="imageBytes">The JPEG image bytes.</param>
        /// <param name="numComponents">Filled in with the number of components.</param>
        /// <param name="subsampling">Filled in with the subsampling.</param>
        /// <param name="widths">Filled in with the widths.</param>
        /// <param name="heights">Filled in with the heights.</param>
        /// <returns>The error code.</returns>
        public unsafe NvJpegStatus GetImageInfo(
            IntPtr libHandle,
            ReadOnlySpan<byte> imageBytes,
            out int numComponents,
            out NvJpegChromaSubsampling subsampling,
            out int[] widths,
            out int[] heights)
        {
            widths = new int[NvJpegConstants.NVJPEG_MAX_COMPONENT];
            heights = new int[NvJpegConstants.NVJPEG_MAX_COMPONENT];

            fixed (byte* imageBytesPtr = imageBytes)
            fixed (int* widthsPtr = widths)
            fixed (int* heightsPtr = heights)
            {
                return GetImageInfo(
                    libHandle,
                    imageBytesPtr,
                    (ulong)imageBytes.Length,
                    out numComponents,
                    out subsampling,
                    widthsPtr,
                    heightsPtr);
            }
        }

        /// <summary>
        /// Performs single image decode.
        /// </summary>
        /// <param name="libHandle">The NvJPEG library handle</param>
        /// <param name="stateHandle">The NvJPEG state handle.</param>
        /// <param name="imageBytes">The JPEG image bytes.</param>
        /// <param name="outputFormat">The desired output format.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <returns>The error code.</returns>
        public unsafe NvJpegStatus Decode(
            IntPtr libHandle,
            IntPtr stateHandle,
            ReadOnlySpan<byte> imageBytes,
            NvJpegOutputFormat outputFormat,
            in NvJpegImage destination,
            CudaStream stream)
        {
            var imageInterop = destination.ToInterop();

            fixed (byte* imageBytesPtr = imageBytes)
            {
                return Decode(
                    libHandle,
                    stateHandle,
                    imageBytesPtr,
                    (ulong)imageBytes.Length,
                    outputFormat,
                    &imageInterop,
                    stream?.StreamPtr ?? IntPtr.Zero);
            }
        }

        /// <inheritdoc cref="Decode(
        ///     IntPtr,
        ///     IntPtr,
        ///     ReadOnlySpan{byte},
        ///     NvJpegOutputFormat,
        ///     in NvJpegImage,
        ///     CudaStream)"/>
        public NvJpegStatus Decode(
            IntPtr libHandle,
            IntPtr stateHandle,
            ReadOnlySpan<byte> imageBytes,
            NvJpegOutputFormat outputFormat,
            in NvJpegImage destination) =>
            Decode(
                libHandle,
                stateHandle,
                imageBytes,
                outputFormat,
                destination,
                null);

        #endregion
    }
}
