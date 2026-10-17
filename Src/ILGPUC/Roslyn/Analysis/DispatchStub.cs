// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DispatchStub.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Linq;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// A generated class that routes launches of a kernel with open type arguments to
/// the compiled kernels of its closed instantiations. There is one stub per
/// kernel definition and launch shape, shared by all launch sites that launch it
/// with open type arguments.
/// </summary>
/// <param name="Name">
/// The kernel name of the stub; the generated class is
/// <c>{Name}_CompiledKernel</c>.
/// </param>
/// <param name="KernelDefinition">
/// The generic kernel definition: a method, or the inline lambda whose lifted
/// kernel takes the type parameters of its enclosing methods and types.
/// </param>
/// <param name="Variant">The launch variant shared by all launch sites.</param>
/// <param name="SkippedParameters">
/// The number of leading kernel parameters supplied by the runtime rather than by
/// the launch site (the thread index), 0 or 1.
/// </param>
/// <param name="Specializations">
/// The concrete kernels, one per closed instantiation found.
/// </param>
/// <param name="LaunchSites">The launch sites that use this stub.</param>
/// <param name="DispatchKey">
/// The key under which the stub looks up kernels compiled by the builds of other
/// assemblies, or null if it does not.
/// </param>
sealed record DispatchStub(
    string Name,
    ISymbol KernelDefinition,
    LaunchVariant Variant,
    int SkippedParameters,
    ImmutableArray<KernelDescriptor> Specializations,
    ImmutableArray<KernelDescriptor> LaunchSites,
    string? DispatchKey = null)
{
    /// <summary>
    /// Returns the kernel parameters the launch site supplies, in the context of
    /// the kernel definition: the parameters of a kernel method, or the values an
    /// inline lambda captures.
    /// </summary>
    public ImmutableArray<(string Name, ITypeSymbol Type)> LaunchParameters =>
        KernelDefinition is IMethodSymbol
        {
            MethodKind: not MethodKind.AnonymousFunction,
        } method
            ? [.. method.Parameters
                .Skip(SkippedParameters)
                .Select(p => (p.Name, p.Type))]
            : [.. LaunchSites[0].Parameters.Select(p => (p.Name, p.Type))];

    /// <summary>
    /// Returns a readable name of the kernel for messages.
    /// </summary>
    public string KernelName =>
        KernelDefinition is IMethodSymbol
        {
            MethodKind: not MethodKind.AnonymousFunction,
        } method
            ? method.Name
            : Name;
}

/// <summary>
/// A generated class that routes launches of an inline kernel capturing
/// delegate parameters of its launching method to the compiled kernels bound
/// to the targets of the passed delegates. There is one stub per such launch
/// site.
/// </summary>
/// <param name="Original">
/// The launch site of the inline kernel, whose delegate captures are bound.
/// </param>
/// <param name="Bindings">
/// The kernels bound to the targets, one per combination of targets.
/// </param>
sealed record DelegateDispatchStub(
    KernelDescriptor Original,
    ImmutableArray<KernelDescriptor> Bindings)
{
    /// <summary>
    /// Returns the kernel name of the stub; the generated class is
    /// <c>{Name}_CompiledKernel</c>.
    /// </summary>
    public string Name => Original.KernelName;
}
