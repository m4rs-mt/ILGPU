// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelCompilationErrors.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPUC.IR;
using ILGPUC.Roslyn.Analysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Diagnostics.CodeAnalysis;
using RoslynLocation = Microsoft.CodeAnalysis.Location;

namespace ILGPUC.Roslyn.Generation;

/// <summary>
/// The exception that is thrown when compiling a kernel of an ilgpuc build
/// fails with an error that is reported as a diagnostic.
/// </summary>
/// <param name="diagnostic">The diagnostic to report.</param>
/// <param name="innerException">The compilation error.</param>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Every error is reported as a diagnostic")]
sealed class KernelDiagnosticException(
    KernelDiagnostic diagnostic,
    Exception innerException)
    : Exception(diagnostic.ToString(), innerException)
{
    /// <summary>
    /// Returns the diagnostic to report.
    /// </summary>
    public KernelDiagnostic Diagnostic { get; } = diagnostic;
}

/// <summary>
/// Converts errors and warnings of the kernel compiler into diagnostics of ilgpuc
/// builds.
/// </summary>
static class KernelCompilationErrors
{
    /// <summary>
    /// Converts the given kernel compilation error into a diagnostic: errors
    /// thrown as <see cref="KernelCompilationException"/> keep their
    /// identifiers, and unsupported constructs reported by the compiler are
    /// reported as <see cref="KernelDiagnostic.UnsupportedConstructId"/>.
    /// Diagnostics without a source location are reported at the given
    /// launch site.
    /// </summary>
    /// <param name="exception">The compilation error.</param>
    /// <param name="launchSite">
    /// The location of the launch site whose kernel was compiled.
    /// </param>
    /// <param name="diagnostic">The created diagnostic.</param>
    /// <returns>True if the error is reported as a diagnostic.</returns>
    public static bool TryCreateDiagnostic(
        Exception exception,
        RoslynLocation launchSite,
        out KernelDiagnostic? diagnostic)
    {
        if (exception is AggregateException { InnerExceptions.Count: 1 } aggregate)
            exception = aggregate.InnerExceptions[0];

        switch (exception)
        {
            case KernelCompilationException compilationException:
                diagnostic = new(
                    compilationException.Id,
                    compilationException.Message,
                    GetLocation(compilationException.Location, launchSite));
                return true;
            case InternalCompilerException
            {
                InnerException: NotSupportedException notSupported,
            } internalException:
                diagnostic = new(
                    KernelDiagnostic.UnsupportedConstructId,
                    string.Format(
                        ErrorMessages.KernelDiagnosticUnsupportedConstruct,
                        notSupported.Message),
                    GetLocation(internalException.SourceLocation, launchSite));
                return true;
            case NotSupportedException notSupported:
                diagnostic = new(
                    KernelDiagnostic.UnsupportedConstructId,
                    string.Format(
                        ErrorMessages.KernelDiagnosticUnsupportedConstruct,
                        notSupported.Message),
                    launchSite);
                return true;
            default:
                diagnostic = null;
                return false;
        }
    }

    /// <summary>
    /// Converts the given warning about kernel code into a diagnostic. Warnings
    /// without a source location are reported at the given launch site, if any.
    /// </summary>
    /// <param name="warning">The warning.</param>
    /// <param name="launchSite">
    /// The location of the launch site whose kernel was compiled, if known.
    /// </param>
    /// <returns>The created diagnostic.</returns>
    public static BuildDiagnostic CreateWarning(
        KernelCompilationWarning warning,
        RoslynLocation? launchSite) =>
        BuildDiagnostic.Warning(
            warning.Id,
            GetFileLocation(warning.Location) ?? launchSite,
            warning.Message);

    /// <summary>
    /// Returns the source location of a diagnostic: the given location if it
    /// refers to a file, and the given launch site otherwise.
    /// </summary>
    /// <param name="location">The location of the error.</param>
    /// <param name="launchSite">The location of the launch site.</param>
    /// <returns>The source location to report the diagnostic at.</returns>
    private static RoslynLocation GetLocation(
        Location? location,
        RoslynLocation launchSite) =>
        GetFileLocation(location) ?? launchSite;

    /// <summary>
    /// Returns the source location of the given location if it refers to a file.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns>The source location, or null.</returns>
    private static RoslynLocation? GetFileLocation(Location? location)
    {
        if (location is not FileLocation fileLocation)
            return null;

        // Sequence points use one-based lines and columns
        var start = new LinePosition(
            Math.Max(fileLocation.StartLine - 1, 0),
            Math.Max(fileLocation.StartColumn - 1, 0));
        var end = new LinePosition(
            Math.Max(fileLocation.EndLine - 1, start.Line),
            Math.Max(fileLocation.EndColumn - 1, 0));
        return RoslynLocation.Create(
            fileLocation.FileName,
            default,
            new LinePositionSpan(start, end < start ? start : end));
    }
}
