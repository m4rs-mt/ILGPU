// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelNotCompiledException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Globalization;

namespace ILGPU.Runtime;

/// <summary>
/// The exception that is thrown when a kernel is launched on an accelerator it was
/// not compiled for by ILGPUC.
/// </summary>
[Serializable]
public sealed class KernelNotCompiledException : NotSupportedException
{
    /// <summary>
    /// Initializes a new instance of the KernelNotCompiledException class.
    /// </summary>
    public KernelNotCompiledException()
    { }

    /// <summary>
    /// Initializes a new instance of the KernelNotCompiledException class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public KernelNotCompiledException(string message)
        : base(message)
    { }

    /// <summary>
    /// Initializes a new instance of the KernelNotCompiledException class
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
    public KernelNotCompiledException(string message, Exception innerException)
        : base(message, innerException)
    { }

    /// <summary>
    /// Initializes a new instance of the KernelNotCompiledException class
    /// describing the kernel, the accelerator type and why no compiled kernel
    /// is available.
    /// </summary>
    /// <param name="kernelName">The name of the kernel that was launched.</param>
    /// <param name="acceleratorType">
    /// The accelerator type the kernel was launched on.
    /// </param>
    /// <param name="reason">Why no compiled kernel is available.</param>
    public KernelNotCompiledException(
        string kernelName,
        AcceleratorType acceleratorType,
        string reason)
        : base(string.Format(
            CultureInfo.CurrentCulture,
            RuntimeErrorMessages.KernelNotCompiled,
            kernelName,
            acceleratorType,
            reason))
    {
        KernelName = kernelName;
        AcceleratorType = acceleratorType;
    }

    /// <summary>
    /// Returns the name of the kernel that was launched, if known.
    /// </summary>
    public string? KernelName { get; }

    /// <summary>
    /// Returns the accelerator type the kernel was launched on, or
    /// <see cref="AcceleratorType.None"/> if unknown.
    /// </summary>
    public AcceleratorType AcceleratorType { get; }
}
