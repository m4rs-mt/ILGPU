// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvJpegEnums.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime.Cuda
{
    public enum NvJpegChromaSubsampling : int
    {
        NVJPEG_CSS_444 = 0,
        NVJPEG_CSS_422 = 1,
        NVJPEG_CSS_420 = 2,
        NVJPEG_CSS_440 = 3,
        NVJPEG_CSS_411 = 4,
        NVJPEG_CSS_410 = 5,
        NVJPEG_CSS_GRAY = 6,
        NVJPEG_CSS_UNKNOWN = -1
    }

    [SuppressMessage(
        "Design",
        "CA1027:Mark enums with FlagsAttribute",
        Justification = "This is not a flag enumeration")]
    public enum NvJpegOutputFormat : int
    {
        NVJPEG_OUTPUT_UNCHANGED = 0,
        NVJPEG_OUTPUT_YUV = 1,
        NVJPEG_OUTPUT_Y = 2,
        NVJPEG_OUTPUT_RGB = 3,
        NVJPEG_OUTPUT_BGR = 4,
        NVJPEG_OUTPUT_RGBI = 5,
        NVJPEG_OUTPUT_BGRI = 6,
        NVJPEG_OUTPUT_FORMAT_MAX = NVJPEG_OUTPUT_BGRI
    }

    public enum NvJpegStatus : int
    {
        NVJPEG_STATUS_SUCCESS = 0,
        NVJPEG_STATUS_NOT_INITIALIZED = 1,
        NVJPEG_STATUS_INVALID_PARAMETER = 2,
        NVJPEG_STATUS_BAD_JPEG = 3,
        NVJPEG_STATUS_JPEG_NOT_SUPPORTED = 4,
        NVJPEG_STATUS_ALLOCATOR_FAILURE = 5,
        NVJPEG_STATUS_EXECUTION_FAILED = 6,
        NVJPEG_STATUS_ARCH_MISMATCH = 7,
        NVJPEG_STATUS_INTERNAL_ERROR = 8,
        NVJPEG_STATUS_IMPLEMENTATION_NOT_SUPPORTED = 9,
    }
}

#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
