// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvJpegStructs.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;

#pragma warning disable CA1051 // Do not declare visible instance fields
#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace ILGPU.Runtime.Cuda.Libraries;

public unsafe struct NvJpegImage_Interop
{
    public fixed ulong Channel[NvJpegConstants.NVJPEG_MAX_COMPONENT];
    public fixed ulong Pitch[NvJpegConstants.NVJPEG_MAX_COMPONENT];
}

public struct NvJpegImage
{
    #region Properties

    public MemoryBuffer1D<byte, Stride1D.Dense>?[] Channel;
    public ulong[] Pitch;

    #endregion

    #region Static

#pragma warning disable CA2000 // Dispose objects before losing scope

    /// <summary>
    /// Creates image buffers to hold an image of the specified width, height and
    /// number of components/channels.
    /// </summary>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="width">The width (in bytes) per channel.</param>
    /// <param name="height">The height (in bytes) per channel.</param>
    /// <param name="numComponents">The number of components/channels.</param>
    /// <returns>The allocated buffers.</returns>
    public static NvJpegImage Create(
        AcceleratorStream stream,
        int width,
        int height,
        int numComponents)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (numComponents <= 0 ||
            numComponents > NvJpegConstants.NVJPEG_MAX_COMPONENT)
        {
            throw new ArgumentOutOfRangeException(nameof(numComponents));
        }

        var size = width * height;
        var outputImage = new NvJpegImage
        {
            Channel =
            [
                    numComponents >= 1 ? stream.Allocate1D<byte>(size) : null,
                    numComponents >= 2 ? stream.Allocate1D<byte>(size) : null,
                    numComponents >= 3 ? stream.Allocate1D<byte>(size) : null,
                    numComponents >= 4 ? stream.Allocate1D<byte>(size) : null
            ],
            Pitch =
            [
                    numComponents >= 1 ? (ulong)width : 0,
                    numComponents >= 2 ? (ulong)width : 0,
                    numComponents >= 3 ? (ulong)width : 0,
                    numComponents >= 4 ? (ulong)width : 0,
            ]
        };

        return outputImage;
    }

    #endregion

#pragma warning restore CA2000 // Dispose objects before losing scope

    #region Methods

    /// <summary>
    /// Gets a representation of the image buffer allocation that is suitable for
    /// the NvJpeg Interop API.
    /// </summary>
    /// <returns>The interop data structure.</returns>
    public unsafe NvJpegImage_Interop ToInterop()
    {
        static ulong MemoryBufferToUInt64(MemoryBuffer1D<byte, Stride1D.Dense> buffer) =>
            buffer != null
            ? (ulong)buffer.View.BaseView.LoadEffectiveAddressAsPtr().ToInt64()
            : 0L;

        var imageInterop = new NvJpegImage_Interop();
        for (var i = 0;
            i < Math.Min(Channel.Length, NvJpegConstants.NVJPEG_MAX_COMPONENT);
            i++)
        {
            imageInterop.Channel[i] = MemoryBufferToUInt64(Channel[i].AsNotNull());
            imageInterop.Pitch[i] = Pitch[i];
        }

        return imageInterop;
    }

    #endregion
}

#pragma warning restore CA1051 // Do not declare visible instance fields
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
