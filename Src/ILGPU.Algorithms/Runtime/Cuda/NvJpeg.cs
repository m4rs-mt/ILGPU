// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvJpeg.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.Cuda.API;
using System;

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Wrapper over NvJpeg to simplify integration with ILGPU.
    /// </summary>
    public sealed class NvJpeg
    {
        /// <summary>
        /// Constructs a new NvJpeg instance.
        /// </summary>
        public NvJpeg()
            : this(default)
        { }

        /// <summary>
        /// Constructs a new NvJpeg wrapper instance.
        /// </summary>
        public NvJpeg(NvJpegAPIVersion? version)
        {
            API = NvJpegAPI.Create(version);
        }

        /// <summary>
        /// The underlying API wrapper.
        /// </summary>
        public NvJpegAPI API { get; }

        /// <summary>
        /// Returns the major version of the NvJpeg library.
        /// </summary>
        public int MajorVersion => GetProperty(LibraryPropertyType.MAJOR_VERSION);

        /// <summary>
        /// Returns the minor version of the NvJpeg library.
        /// </summary>
        public int MinorVersion => GetProperty(LibraryPropertyType.MINOR_VERSION);

        /// <summary>
        /// Returns the patch version of the NvJpeg library.
        /// </summary>
        public int PatchVersion => GetProperty(LibraryPropertyType.PATCH_LEVEL);

        /// <inheritdoc cref="NvJpegAPI.CreateSimple(out IntPtr)"/>
        public NvJpegLibrary CreateSimple()
        {
            NvJpegException.ThrowIfFailed(
                API.CreateSimple(out IntPtr libHandle));
            return new NvJpegLibrary(API, libHandle);
        }

        /// <inheritdoc cref="NvJpegAPI.GetProperty(LibraryPropertyType, out int)"/>
        public int GetProperty(LibraryPropertyType libraryPropertyType)
        {
            NvJpegException.ThrowIfFailed(
                API.GetProperty(libraryPropertyType, out var value));
            return value;
        }

        /// <inheritdoc cref="NvJpegAPI.GetCudartProperty(LibraryPropertyType, out int)"/>
        public int GetCudartProperty(LibraryPropertyType libraryPropertyType)
        {
            NvJpegException.ThrowIfFailed(
                API.GetCudartProperty(libraryPropertyType, out var value));
            return value;
        }
    }
}
