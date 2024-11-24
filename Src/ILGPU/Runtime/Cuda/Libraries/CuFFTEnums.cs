// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuFFTEnums.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

#pragma warning disable CA1008 // Enums should have zero value
#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace ILGPU.Runtime.Cuda
{
    public enum CuFFTDirection : int
    {
        FORWARD = -1,
        INVERSE = 1,
    }

    public enum CuFFTResult : int
    {
        CUFFT_SUCCESS = 0x0,
        CUFFT_INVALID_PLAN = 0x1,
        CUFFT_ALLOC_FAILED = 0x2,
        CUFFT_INVALID_TYPE = 0x3,
        CUFFT_INVALID_VALUE = 0x4,
        CUFFT_INTERNAL_ERROR = 0x5,
        CUFFT_EXEC_FAILED = 0x6,
        CUFFT_SETUP_FAILED = 0x7,
        CUFFT_INVALID_SIZE = 0x8,
        CUFFT_UNALIGNED_DATA = 0x9,
        CUFFT_INCOMPLETE_PARAMETER_LIST = 0xA,
        CUFFT_INVALID_DEVICE = 0xB,
        CUFFT_PARSE_ERROR = 0xC,
        CUFFT_NO_WORKSPACE = 0xD,
        CUFFT_NOT_IMPLEMENTED = 0xE,
        CUFFT_LICENSE_ERROR = 0x0F,
        CUFFT_NOT_SUPPORTED = 0x10,
    }

    public enum CuFFTType : int
    {
        /// <summary>
        /// Complex to complex (interleaved).
        /// </summary>
        CUFFT_C2C = 0x29,

        /// <summary>
        /// Real to complex (interleaved).
        /// </summary>
        CUFFT_R2C = 0x2a,

        /// <summary>
        /// Complex (interleaved) to real.
        /// </summary>
        CUFFT_C2R = 0x2c,

        /// <summary>
        /// Double-complex to double-complex (interleaved).
        /// </summary>
        CUFFT_Z2Z = 0x69,

        /// <summary>
        /// Double to double-complex (interleaved).
        /// </summary>
        CUFFT_D2Z = 0x6a,

        /// <summary>
        /// Double-complex (interleaved) to double.
        /// </summary>
        CUFFT_Z2D = 0x6c,
    }
}

#pragma warning restore CA1008 // Enums should have zero value
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
