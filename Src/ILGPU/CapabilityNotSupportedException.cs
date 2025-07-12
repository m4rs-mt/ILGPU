﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CapabilityNotSupportedException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU;

/// <summary>
/// The exception that is thrown when a capability is not supported by an accelerator.
/// </summary>
[Serializable]
public sealed class CapabilityNotSupportedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the CapabilityNotSupportedException class.
    /// </summary>
    public CapabilityNotSupportedException()
    { }

    /// <summary>
    /// Initializes a new instance of the CapabilityNotSupportedException class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CapabilityNotSupportedException(string message)
        : base(message)
    { }

    /// <summary>
    /// Initializes a new instance of the CapabilityNotSupportedException class
    /// with a specified error message and a reference to the inner exception
    /// that is the cause of this exception.
    /// </summary>
    /// <param name="message">
    /// The error message that explains the reason for the exception.
    /// </param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception, or a null reference
    /// if no inner exception is specified.
    /// </param>
    public CapabilityNotSupportedException(string message, Exception innerException)
        : base(message, innerException)
    { }
}
