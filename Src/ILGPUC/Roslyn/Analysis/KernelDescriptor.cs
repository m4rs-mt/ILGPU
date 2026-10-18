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
using ILGPUC.Backends;
using System.Linq;

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
    ImmutableArray<ITypeSymbol>? ConcreteTypeArgs = null)
{
    /// <summary>
    /// Returns how the kernel is launched, which the generated device code,
    /// the generated Launch method and the rewritten call site agree on:
    /// implicitly grouped launches with 1-3 index dimensions, or grouped
    /// launches whose kernel does or does not take a <c>KernelIndex</c>.
    /// </summary>
    public EntryPointLaunch GetEntryPointLaunch()
    {
        if (Variant == LaunchVariant.Grouped)
        {
            var firstParam = KernelMethod?.Parameters.FirstOrDefault();
            return EntryPointLaunch.Grouped(
                hasKernelIndex: firstParam?.Type.Name is "KernelIndex");
        }

        return EntryPointLaunch.Implicit(Variant switch
        {
            LaunchVariant.Auto2D or LaunchVariant.AutoLong2D
                or LaunchVariant.Auto2DStride
                or LaunchVariant.AutoLong2DStride => 2,
            LaunchVariant.Auto3D or LaunchVariant.AutoLong3D
                or LaunchVariant.Auto3DStride
                or LaunchVariant.AutoLong3DStride => 3,
            _ => 1,
        });
    }
}
