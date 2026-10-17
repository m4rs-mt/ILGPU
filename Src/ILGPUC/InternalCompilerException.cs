// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InternalCompilerException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using System;

namespace ILGPUC;

/// <summary>
/// The exception that is thrown when an internal compiler error has been detected.
/// </summary>
[Serializable]
sealed class InternalCompilerException : Exception
{
    /// <summary>
    /// Initializes a new instance of the InternalCompilerException class.
    /// </summary>
    public InternalCompilerException()
    { }

    /// <summary>
    /// Initializes a new instance of the InternalCompilerException class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InternalCompilerException(string message)
        : base(message)
    { }

    /// <summary>
    /// Initializes a new instance of the InternalCompilerException class
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
    public InternalCompilerException(string message, Exception innerException)
        : base(message, innerException)
    { }

    /// <summary>
    /// Initializes a new instance of the InternalCompilerException class
    /// with a specified error message, a reference to the inner exception
    /// that is the cause of this exception and the source location of the
    /// error.
    /// </summary>
    /// <param name="message">
    /// The error message that explains the reason for the exception.
    /// </param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception.
    /// </param>
    /// <param name="sourceLocation">The source location of the error.</param>
    internal InternalCompilerException(
        string message,
        Exception innerException,
        Location? sourceLocation)
        : base(message, innerException)
    {
        SourceLocation = sourceLocation;
    }

    /// <summary>
    /// Returns the source location of the error (if any), which ilgpuc builds
    /// report errors at.
    /// </summary>
    [field: NonSerialized]
    internal Location? SourceLocation { get; }
}
