// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CuBlasEnums.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Runtime.Cuda
{
    public enum CuBlasStatus : int
    {
        CUBLAS_STATUS_SUCCESS = 0,
        CUBLAS_STATUS_NOT_INITIALIZED = 1,
        CUBLAS_STATUS_ALLOC_FAILED = 3,
        CUBLAS_STATUS_INVALID_VALUE = 7,
        CUBLAS_STATUS_ARCH_MISMATCH = 8,
        CUBLAS_STATUS_MAPPING_ERROR = 11,
        CUBLAS_STATUS_EXECUTION_FAILED = 13,
        CUBLAS_STATUS_INTERNAL_ERROR = 14,
        CUBLAS_STATUS_NOT_SUPPORTED = 15,
        CUBLAS_STATUS_LICENSE_ERROR = 16
    }

    public enum CuBlasFillMode
    {
        Lower = 0,
        Upper = 1,
        Full = 2
    }

    public enum CuBlasSideMode
    {
        Left = 0,
        Right = 1
    }

    public enum CuBlasDiagType : int
    {
        NonUnit = 0,
        Unit = 1
    }

    [SuppressMessage(
        "Design",
        "CA1027:Mark enums with FlagsAttribute",
        Justification = "This is not a flag enumeration")]
    public enum CuBlasOperation : int
    {
        NonTranspose = 0,
        Transpose = 1,
        ConjugateTranspose = 2,
        Hermitan = ConjugateTranspose,
        Conjugate = 3
    }

    public enum CuBlasPointerMode : int
    {
        Host = 0,
        Device = 1
    }

    public enum CuBlasAtomicsMode : int
    {
        NotAllowed = 0,
        Allowed = 1
    }

    public enum CuBlasMathMode : int
    {
        DefaultMath = 0,
        TensorOpMath = 1
    }

    /// <summary>
    /// Specifies a cuBlas API version.
    /// </summary>
    public enum CuBlasAPIVersion : int
    {
        /// <summary>
        /// Version 10 of the cuBlas library.
        /// </summary>
        V10,

        /// <summary>
        /// Version 11 of the cuBlas library.
        /// </summary>
        V11,
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore CA1707 // Identifiers should not contain underscores
