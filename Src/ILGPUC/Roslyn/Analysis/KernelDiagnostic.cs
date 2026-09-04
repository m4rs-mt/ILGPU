// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelDiagnostic.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// An error found while analyzing a kernel launch site, reported at the
/// offending source location before any kernel is compiled.
/// </summary>
/// <param name="Id">The diagnostic identifier (e.g. <c>ILGPU1101</c>).</param>
/// <param name="Message">The human-readable description of the problem.</param>
/// <param name="Location">The source location the diagnostic refers to.</param>
record KernelDiagnostic(string Id, string Message, Location Location)
{
    /// <summary>
    /// An inline kernel uses the enclosing instance.
    /// </summary>
    public const string CapturesThisId = "ILGPU1101";

    /// <summary>
    /// An inline kernel assigns a variable of its enclosing method.
    /// </summary>
    public const string AssignsCaptureId = "ILGPU1102";

    /// <summary>
    /// An inline kernel captures a value that cannot be a kernel argument.
    /// </summary>
    public const string UnsupportedCaptureId = "ILGPU1103";

    /// <summary>
    /// An inline kernel is declared in a generic method or type.
    /// </summary>
    public const string GenericContextId = "ILGPU1104";

    /// <summary>
    /// A local function used as a kernel, or called by an inline kernel,
    /// captures variables of its enclosing method.
    /// </summary>
    public const string LocalFunctionCapturesId = "ILGPU1105";

    /// <summary>
    /// Formats this diagnostic in the canonical
    /// <c>file(line,column): error ID: message</c> form that MSBuild and IDEs
    /// pick up from tool output.
    /// </summary>
    public override string ToString()
    {
        var span = Location.GetLineSpan();
        var position = span.StartLinePosition;
        var path = string.IsNullOrEmpty(span.Path) ? "ILGPUC" : span.Path;
        return $"{path}({position.Line + 1},{position.Character + 1}): " +
            $"error {Id}: {Message}";
    }
}
