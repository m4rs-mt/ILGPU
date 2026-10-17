// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AnalysisOptions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Options for analyzing a compilation.
/// </summary>
/// <param name="Limits">
/// Limits for finding the instantiations of generic kernels; the defaults are used
/// if <see langword="null"/>.
/// </param>
/// <param name="WarnOnOpenPublicEntryPoints">
/// True to warn for every externally visible method whose type arguments reach a
/// kernel launch, even when the compilation also calls it with closed type
/// arguments.
/// </param>
/// <param name="GeneratedTrees">
/// The syntax trees produced by source generators. They take part in the
/// analysis, but launch sites in them cannot be rewritten.
/// </param>
/// <param name="AssemblyName">
/// The name of the assembly being built. When set, dispatch code looks up kernels
/// compiled by the builds of other assemblies, and the launches of public generic
/// methods are summarized for them.
/// </param>
/// <param name="IsLibrary">
/// True if the compilation builds a library, whose methods other assemblies can
/// call; false for an executable, whose launches are not summarized.
/// </param>
sealed record AnalysisOptions(
    GenericInstantiationLimits? Limits = null,
    bool WarnOnOpenPublicEntryPoints = false,
    IReadOnlySet<SyntaxTree>? GeneratedTrees = null,
    string? AssemblyName = null,
    bool IsLibrary = true)
{
    /// <summary>The default options.</summary>
    public static AnalysisOptions Default { get; } = new();
}
