// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelDescriptor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Describes a kernel extracted from a launch site.
/// </summary>
/// <param name="KernelName">
/// The name used for the generated kernel class (e.g., <c>MainKernel</c>).
/// </param>
/// <param name="KernelMethod">
/// The resolved Roslyn method symbol, or <see langword="null"/> for inline lambdas.
/// </param>
/// <param name="KernelBody">
/// The syntax node for the inline kernel body;
/// <see langword="null"/> for method-group kernels.
/// </param>
/// <param name="Parameters">The captured parameters for this kernel.</param>
/// <param name="Variant">The launch variant (1D, 2D, 3D, grouped, etc.).</param>
/// <param name="LaunchInfo">The originating launch call site.</param>
/// <param name="GenericOriginName">
/// For concrete specializations of a generic kernel, the original kernel name
/// used for the dispatch stub (e.g., <c>CalculatorKernel</c>). Null for
/// non-specialized kernels.
/// </param>
/// <param name="ConcreteTypeArgs">
/// The concrete Roslyn type symbols substituted for the kernel's type parameters.
/// Null for non-specialized kernels.
/// </param>
record KernelDescriptor(
    string KernelName,
    IMethodSymbol? KernelMethod,
    SyntaxNode? KernelBody,
    ImmutableArray<CapturedParameterInfo> Parameters,
    LaunchVariant Variant,
    KernelLaunchInfo LaunchInfo,
    string? GenericOriginName = null,
    ImmutableArray<ITypeSymbol>? ConcreteTypeArgs = null);
