// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ROCmException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.ROCm;

/// <summary>
/// Represents a HIP exception.
/// </summary>
public sealed class ROCmException : Exception
{
    /// <summary>
    /// Creates a new HIP exception.
    /// </summary>
    public ROCmException() : this(ROCmError.Unknown)
    { }

    /// <summary>
    /// Creates a new HIP exception.
    /// </summary>
    /// <param name="error">The error code.</param>
    public ROCmException(ROCmError error)
        : base($"HIP error: {error}")
    {
        Error = error;
    }

    /// <summary>
    /// Creates a new HIP exception with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ROCmException(string message)
        : base(message)
    {
        Error = ROCmError.Unknown;
    }

    /// <summary>
    /// Creates a new HIP exception with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ROCmException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = ROCmError.Unknown;
    }

    /// <summary>
    /// The HIP error code.
    /// </summary>
    public ROCmError Error { get; }

    /// <summary>
    /// Throws if the error code indicates failure.
    /// </summary>
    /// <param name="error">The error code to check.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfFailed(ROCmError error)
    {
        if (error != ROCmError.Success)
            throw new ROCmException(error);
    }
}
