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
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
/// For concrete specializations of a generic kernel and for kernels bound to
/// delegate targets, the original kernel name used for the dispatch stub
/// (e.g., <c>CalculatorKernel</c>). Null for non-specialized kernels.
/// </param>
/// <param name="ConcreteTypeArgs">
/// The concrete Roslyn type symbols substituted for the kernel's type parameters.
/// Null for non-specialized kernels.
/// </param>
/// <param name="LiftedKernel">
/// For inline lambdas, the static local function the lambda body is lifted
/// into, which is declared next to the launch site so the kernel can be
/// resolved and compiled like a named method. Null for named kernels and for
/// inline lambdas that were rejected with a diagnostic.
/// </param>
/// <param name="DelegateCaptures">
/// For inline lambdas, the delegate parameters of the launching method that
/// the lambda captures. Such a kernel is compiled once for each combination of
/// the targets passed to these parameters. Null if there are none.
/// </param>
/// <param name="DelegateKeys">
/// For kernels bound to delegate targets, the key of the target bound to each
/// delegate capture of the original kernel. Null for all other kernels.
/// </param>
record KernelDescriptor(
    string KernelName,
    IMethodSymbol? KernelMethod,
    SyntaxNode? KernelBody,
    ImmutableArray<CapturedParameterInfo> Parameters,
    LaunchVariant Variant,
    KernelLaunchInfo LaunchInfo,
    string? GenericOriginName = null,
    ImmutableArray<ITypeSymbol>? ConcreteTypeArgs = null,
    LocalFunctionStatementSyntax? LiftedKernel = null,
    ImmutableArray<DelegateCapture>? DelegateCaptures = null,
    ImmutableArray<string>? DelegateKeys = null)
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
            // Lifted inline lambdas always take the KernelIndex of the lambda
            var firstParam = KernelMethod?.Parameters.FirstOrDefault();
            return EntryPointLaunch.Grouped(
                hasKernelIndex: LiftedKernel is not null
                    || firstParam?.Type.Name is "KernelIndex");
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

/// <summary>
/// A delegate parameter of the launching method captured by an inline kernel.
/// </summary>
/// <param name="ParameterIndex">
/// The index of the capture in <see cref="KernelDescriptor.Parameters"/>.
/// </param>
/// <param name="Parameter">The captured delegate parameter.</param>
/// <param name="Reference">The first reference to it in the inline kernel.</param>
record DelegateCapture(
    int ParameterIndex,
    IParameterSymbol Parameter,
    SyntaxNode Reference);
