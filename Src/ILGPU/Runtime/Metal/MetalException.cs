// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime.Metal;

/// <summary>
/// Represents an exception that is thrown when a Metal operation fails.
/// </summary>
public sealed class MetalException : AcceleratorException
{
    /// <summary>
    /// Creates a new Metal exception with a default message.
    /// </summary>
    public MetalException()
        : base("A Metal operation failed.")
    {
        Error = MetalError.Error;
    }

    /// <summary>
    /// Creates a new Metal exception with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public MetalException(string message)
        : base(message)
    {
        Error = MetalError.Error;
    }

    /// <summary>
    /// Creates a new Metal exception with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MetalException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = MetalError.Error;
    }

    /// <summary>
    /// Creates a new Metal exception from an error code.
    /// </summary>
    /// <param name="error">The Metal error code.</param>
    public MetalException(MetalError error)
        : base(error.ToString())
    {
        Error = error;
    }

    /// <summary>
    /// Creates a new Metal exception with a custom message.
    /// </summary>
    /// <param name="error">The Metal error code.</param>
    /// <param name="message">The error message.</param>
    public MetalException(MetalError error, string message)
        : base(message)
    {
        Error = error;
    }

    /// <summary>
    /// Creates a new Metal exception from an NSError handle.
    /// </summary>
    /// <param name="error">The Metal error code.</param>
    /// <param name="nsError">The NSError handle.</param>
    /// <param name="operation">Description of the operation that failed.</param>
    public MetalException(MetalError error, IntPtr nsError, string operation)
        : base(FormatNSError(nsError, operation))
    {
        Error = error;
    }

    /// <summary>
    /// The Metal error code.
    /// </summary>
    public MetalError Error { get; }

    /// <inheritdoc/>
    public override AcceleratorType AcceleratorType => AcceleratorType.Metal;

    /// <summary>
    /// Throws a <see cref="MetalException"/> if the error is not
    /// <see cref="MetalError.Success"/>.
    /// </summary>
    /// <param name="error">The error code to check.</param>
    /// <param name="expression">
    /// The caller expression (auto-captured).
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ThrowIfFailed(
        MetalError error,
        [CallerArgumentExpression(nameof(error))] string? expression = null)
    {
        if (error != MetalError.Success)
            throw new MetalException(error, expression ?? error.ToString());
    }

    /// <summary>
    /// Throws a <see cref="MetalException"/> if the result pointer is null,
    /// extracting the error message from the NSError handle.
    /// </summary>
    /// <param name="result">The result pointer to check.</param>
    /// <param name="nsError">The NSError handle from the ObjC call.</param>
    /// <param name="error">The error code to use if result is null.</param>
    /// <param name="operation">Description of the failed operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ThrowIfNull(
        IntPtr result,
        IntPtr nsError,
        MetalError error,
        string operation)
    {
        if (result == IntPtr.Zero)
            throw new MetalException(error, nsError, operation);
    }

    /// <summary>
    /// Verifies disposal status. Only throws when actively disposing
    /// (not from finalizer).
    /// </summary>
    /// <param name="disposing">True if called from Dispose.</param>
    /// <param name="error">The error code to check.</param>
    internal static void VerifyDisposed(bool disposing, MetalError error)
    {
        if (disposing)
            ThrowIfFailed(error);
    }

    private static string FormatNSError(IntPtr nsError, string operation)
    {
        var description = MetalAPI.GetNSErrorDescription(nsError);
        return description != null
            ? $"{operation}: {description}"
            : operation;
    }
}
