// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelCompilationException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using System;
using System.Diagnostics.CodeAnalysis;

namespace ILGPUC;

/// <summary>
/// The exception that is thrown when kernel code uses a construct that ILGPUC
/// rejects with a diagnostic, such as an unsupported use of a delegate. Unlike
/// other code generation errors, it is reported right away instead of turning
/// the failing method into an external declaration, except for static fields
/// that kernels cannot load, which are reported only if a call to the failing
/// method survives optimization. ilgpuc builds report it in the canonical
/// <c>file(line,column): error ID: message</c> form.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Every error requires a diagnostic identifier and a location")]
sealed class KernelCompilationException : Exception
{
    /// <summary>
    /// Initializes a new kernel compilation exception.
    /// </summary>
    /// <param name="id">The diagnostic identifier (e.g. <c>ILGPU1201</c>).</param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="location">The source location of the error.</param>
    public KernelCompilationException(string id, string message, Location location)
        : base(message)
    {
        Id = id;
        Location = location;
    }

    /// <summary>
    /// Returns the diagnostic identifier (e.g. <c>ILGPU1201</c>).
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Returns the source location of the error, which is unknown if no
    /// debug information is available.
    /// </summary>
    public Location Location { get; }

    /// <summary>
    /// Returns the identifier, the message and the location of this error.
    /// </summary>
    public override string ToString() =>
        $"{Id}: {Location.FormatErrorMessage(Message)}";
}
