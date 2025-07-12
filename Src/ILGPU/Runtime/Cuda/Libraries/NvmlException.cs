// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: NvmlException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.Cuda.Libraries;

/// <summary>
/// Represents an exception that can be thrown by the NVML library.
/// </summary>
[Serializable]
public sealed class NvmlException : Exception
{
    #region Instance

    /// <summary>
    /// Constructs a new NVML exception.
    /// </summary>
    public NvmlException()
        : this(NvmlReturn.NVML_ERROR_UNKNOWN)
    { }

    /// <summary>
    /// Constructs a new NVML exception.
    /// </summary>
    /// <param name="errorCode">The NVML runtime error.</param>
    public NvmlException(NvmlReturn errorCode)
        : base()
    {
        Error = errorCode;
    }

    /// <summary cref="Exception(string)"/>
    public NvmlException(string message)
        : base(message)
    {
        Error = NvmlReturn.NVML_ERROR_UNKNOWN;
    }

    /// <summary cref="Exception(string, Exception)"/>
    public NvmlException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = NvmlReturn.NVML_ERROR_UNKNOWN;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the error.
    /// </summary>
    public NvmlReturn Error { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Checks the given status and throws an exception in case of an error.
    /// </summary>
    /// <param name="errorCode">The NVML error code to check.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfFailed(NvmlReturn errorCode)
    {
        if (errorCode != NvmlReturn.NVML_SUCCESS)
            throw new NvmlException(errorCode);
    }

    #endregion
}
