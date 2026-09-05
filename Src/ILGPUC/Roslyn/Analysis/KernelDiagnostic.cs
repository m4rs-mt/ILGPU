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
    /// An inline kernel, or a lambda passed for a delegate it captures, is
    /// declared in a generic method or type.
    /// </summary>
    public const string GenericContextId = "ILGPU1104";

    /// <summary>
    /// A local function used as a kernel, or called by an inline kernel,
    /// captures variables of its enclosing method.
    /// </summary>
    public const string LocalFunctionCapturesId = "ILGPU1105";

    /// <summary>
    /// A kernel launch is part of an expression instead of a statement.
    /// </summary>
    public const string LaunchNotStatementId = "ILGPU1106";

    /// <summary>
    /// A lambda passed for a delegate parameter captured by an inline kernel
    /// captures variables itself.
    /// </summary>
    public const string CapturingDelegateArgumentId = "ILGPU1107";

    /// <summary>
    /// An argument passed for a delegate parameter captured by an inline kernel
    /// cannot be traced to a lambda or a static method.
    /// </summary>
    public const string UntraceableDelegateArgumentId = "ILGPU1108";

    /// <summary>
    /// An inline kernel captures a delegate parameter that no call passes a
    /// lambda or a static method to.
    /// </summary>
    public const string UnboundDelegateParameterId = "ILGPU1109";

    // Errors reported by the IL frontend while compiling kernel code (ILGPU12xx),
    // which it throws as KernelCompilationException

    /// <summary>
    /// Kernel code uses a construct that is not supported in kernels.
    /// </summary>
    public const string UnsupportedConstructId = "ILGPU1200";

    /// <summary>
    /// Kernel code compares delegates, tests them for null or uses delegate
    /// operations other than invoking them.
    /// </summary>
    public const string DelegateIdentityId = "ILGPU1201";

    /// <summary>
    /// Kernel code invokes a delegate, or passes it to a method, that cannot be
    /// traced to a lambda expression or a static method.
    /// </summary>
    public const string UnresolvedDelegateId = "ILGPU1202";

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
