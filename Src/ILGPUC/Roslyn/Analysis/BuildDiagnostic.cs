// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: BuildDiagnostic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using System.Globalization;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// The diagnostic codes reported by the <c>build</c> command.
/// </summary>
static class BuildDiagnosticCodes
{
    /// <summary>
    /// A kernel is launched with open type arguments, and no closed instantiation
    /// reaches the launch site in this compilation.
    /// </summary>
    public const string OpenLaunchSite = "ILGPU1001";

    /// <summary>
    /// A generic instantiation nests type arguments deeper than allowed.
    /// </summary>
    public const string TypeDepthExceeded = "ILGPU1002";

    /// <summary>
    /// A generic definition is instantiated more often than allowed.
    /// </summary>
    public const string DefinitionInstantiationsExceeded = "ILGPU1003";

    /// <summary>
    /// The compilation instantiates more generic definitions than allowed.
    /// </summary>
    public const string TotalInstantiationsExceeded = "ILGPU1004";

    /// <summary>
    /// A kernel method could not be resolved in the compiled assembly.
    /// </summary>
    public const string UnresolvedKernel = "ILGPU1005";

    /// <summary>
    /// A kernel instantiation found through a generic wrapper throws an exception
    /// for its type arguments and is skipped.
    /// </summary>
    public const string SkippedThrowingInstantiation = "ILGPU1006";

    /// <summary>
    /// The compilation that ILGPUC builds from the project does not compile.
    /// </summary>
    public const string CompilationErrors = "ILGPU1007";

    /// <summary>
    /// A kernel is launched from source-generated code, which cannot be rewritten.
    /// </summary>
    public const string LaunchInGeneratedCode = "ILGPU1008";

    /// <summary>
    /// A source generator could not be loaded or failed.
    /// </summary>
    public const string SourceGeneratorFailed = "ILGPU1009";

    /// <summary>
    /// An externally visible method passes its type arguments to a kernel launch,
    /// so other assemblies may launch instantiations that are not compiled. Only
    /// reported on request.
    /// </summary>
    public const string OpenPublicEntryPoint = "ILGPU1010";

    /// <summary>
    /// A kernel launch uses a type that generated code cannot access.
    /// </summary>
    public const string InaccessibleKernelType = "ILGPU1011";
}

/// <summary>
/// A diagnostic reported by the <c>build</c> command. Its string form is the
/// canonical <c>file(line,col): severity code: message</c> format, which MSBuild's
/// <c>Exec</c> task turns into build warnings and errors.
/// </summary>
/// <param name="Code">The diagnostic code, for example <c>ILGPU1001</c>.</param>
/// <param name="IsError">
/// <see langword="true"/> for an error, <see langword="false"/> for a warning.
/// </param>
/// <param name="Location">The source location, if known.</param>
/// <param name="Message">The message text.</param>
sealed record BuildDiagnostic(
    string Code,
    bool IsError,
    Location? Location,
    string Message)
{
    /// <summary>
    /// Creates a warning.
    /// </summary>
    public static BuildDiagnostic Warning(
        string code,
        Location? location,
        string message) =>
        new(code, false, location, message);

    /// <summary>
    /// Creates an error.
    /// </summary>
    public static BuildDiagnostic Error(
        string code,
        Location? location,
        string message) =>
        new(code, true, location, message);

    /// <summary>
    /// Formats the diagnostic in the canonical MSBuild format.
    /// </summary>
    public override string ToString()
    {
        var severity = IsError ? "error" : "warning";
        var origin = "ILGPUC";
        if (Location is { } location && location.GetLineSpan() is
            { IsValid: true, Path.Length: > 0 } span)
        {
            var start = span.StartLinePosition;
            origin = string.Format(
                CultureInfo.InvariantCulture,
                "{0}({1},{2})",
                span.Path,
                start.Line + 1,
                start.Character + 1);
        }
        return $"{origin}: {severity} {Code}: {Message}";
    }
}
