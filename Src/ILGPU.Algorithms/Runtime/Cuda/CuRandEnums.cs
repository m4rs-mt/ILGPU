// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuRandEnums.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

#pragma warning disable CA1008 // Enums should have zero value
#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace ILGPU.Runtime.Cuda
{
    public enum CuRandStatus : int
    {
        CURAND_STATUS_SUCCESS = 0,
        CURAND_STATUS_VERSION_MISMATCH = 100,
        CURAND_STATUS_NOT_INITIALIZED = 101,
        CURAND_STATUS_ALLOCATION_FAILED = 102,
        CURAND_STATUS_TYPE_ERROR = 103,
        CURAND_STATUS_OUT_OF_RANGE = 104,
        CURAND_STATUS_LENGTH_NOT_MULTIPLE = 105,
        CURAND_STATUS_DOUBLE_PRECISION_REQUIRED = 106,
        CURAND_STATUS_LAUNCH_FAILURE = 201,
        CURAND_STATUS_PREEXISTING_FAILURE = 202,
        CURAND_STATUS_INITIALIZATION_FAILED = 203,
        CURAND_STATUS_ARCH_MISMATCH = 204,
        CURAND_STATUS_INTERNAL_ERROR = 999,
    }

    public enum CuRandRngType : int
    {
        CURAND_RNG_TEST = 0,
        CURAND_RNG_PSEUDO_DEFAULT = 100,
        CURAND_RNG_PSEUDO_XORWOW = 101,
        CURAND_RNG_PSEUDO_MRG32K3A = 121,
        CURAND_RNG_PSEUDO_MTGP32 = 141,
        CURAND_RNG_PSEUDO_MT19937 = 142,
        CURAND_RNG_PSEUDO_PHILOX4_32_10 = 161,
        CURAND_RNG_QUASI_DEFAULT = 200,
        CURAND_RNG_QUASI_SOBOL32 = 201,
        CURAND_RNG_QUASI_SCRAMBLED_SOBOL32 = 202,
        CURAND_RNG_QUASI_SOBOL64 = 203,
        CURAND_RNG_QUASI_SCRAMBLED_SOBOL64 = 204,
    }
}

#pragma warning restore CA1008 // Enums should have zero value
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
