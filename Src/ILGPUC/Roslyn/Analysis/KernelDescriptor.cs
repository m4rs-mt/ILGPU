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
/// The concrete Roslyn type symbols substituted for the kernel's type parameters,
/// including those of its containing types, ordered as by
/// <see cref="GenericContext.GetTypeParameters"/>. Null for non-specialized kernels.
/// </param>
/// <param name="DispatchTypeArguments">
/// For a launch site whose kernel has open type arguments, the type arguments the
/// rewritten call passes to the generic <c>Launch</c> method of the dispatch stub,
/// in the context of the launch site. Null for all other kernels.
/// </param>
/// <param name="LiftedKernel">
/// For inline lambdas, the static local function the lambda body is lifted
/// into, which is declared next to the launch site so the kernel can be
/// resolved and compiled like a named method. Null for named kernels and for
/// inline lambdas that were rejected with a diagnostic.
/// </param>
/// <param name="InlineKernelSymbol">
/// For inline lambdas, the symbol of the lambda, whose enclosing generic
/// methods and types supply the type parameters of the lifted kernel. Null for
/// named kernels.
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
    ImmutableArray<ITypeSymbol>? DispatchTypeArguments = null,
    LocalFunctionStatementSyntax? LiftedKernel = null,
    IMethodSymbol? InlineKernelSymbol = null,
    ImmutableArray<DelegateCapture>? DelegateCaptures = null,
    ImmutableArray<string>? DelegateKeys = null)
{
    /// <summary>
    /// Returns the definition whose type parameters the kernel is generic over:
    /// the kernel method, or the inline lambda.
    /// </summary>
    public ISymbol? GenericDefinition =>
        (ISymbol?)KernelMethod?.OriginalDefinition ?? InlineKernelSymbol;

    /// <summary>
    /// Returns the type arguments of the kernel at its launch site, ordered as
    /// by <see cref="GenericContext.GetTypeParameters"/>. An inline lambda uses
    /// the type parameters in scope at the launch site unchanged.
    /// </summary>
    public ImmutableArray<ITypeSymbol> GetTypeArguments() =>
        KernelMethod is { } method
            ? GenericContext.GetTypeArguments(method)
            : InlineKernelSymbol is { } lambda
                ? [.. GenericContext.GetTypeParameters(lambda)]
                : [];

    /// <summary>
    /// Returns <see langword="true"/> if the kernel is a local function or an
    /// inline lambda, which the C# compiler emits as a method that takes the type
    /// parameters of its enclosing methods.
    /// </summary>
    public bool IsLocalKernel =>
        KernelMethod is null
            ? InlineKernelSymbol is not null
            : KernelMethod.MethodKind == MethodKind.LocalFunction;

    /// <summary>
    /// Returns a readable name of the kernel for diagnostics.
    /// </summary>
    public string DisplayName =>
        KernelMethod?.ToDisplayString() ?? "inline kernel";

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
