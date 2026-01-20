// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: SpecializationDiscoverer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Represents one concrete specialization of a generic kernel.
/// </summary>
/// <param name="TypeArguments">
/// Concrete Roslyn type symbols substituted for the kernel's type parameters,
/// in the same order as the kernel method's TypeParameters.
/// </param>
/// <param name="MangledSuffix">
/// A unique suffix for generated class names, e.g. <c>_Int32_AdditionOp</c>.
/// </param>
record KernelSpecialization(
    ImmutableArray<ITypeSymbol> TypeArguments,
    string MangledSuffix);

/// <summary>
/// Discovers concrete specializations for generic kernels whose type arguments
/// are still open type parameters (i.e., the launch site is inside a generic
/// wrapper method). Traces callers of the enclosing generic method to find
/// concrete type argument combinations.
/// </summary>
static class SpecializationDiscoverer
{
    /// <summary>
    /// For a kernel descriptor whose <see cref="KernelDescriptor.KernelMethod"/>
    /// has open type parameters, finds all concrete type argument combinations by
    /// tracing callers of the enclosing generic method.
    /// </summary>
    /// <param name="compilation">The Roslyn compilation.</param>
    /// <param name="kernel">
    /// A kernel descriptor with open generic type arguments.
    /// </param>
    /// <returns>
    /// A deduplicated list of concrete specializations discovered from callers.
    /// Empty if no concrete call sites were found.
    /// </returns>
    public static List<KernelSpecialization> DiscoverSpecializations(
        CSharpCompilation compilation,
        KernelDescriptor kernel)
    {
        var kernelMethod = kernel.KernelMethod;
        if (kernelMethod is null || !kernelMethod.IsGenericMethod)
            return [];

        // Step 1: Identify the enclosing generic method that contains the
        // launch site. Walk up from the invocation syntax to find the
        // enclosing method declaration.
        var enclosingMethod = FindEnclosingMethod(
            compilation, kernel.LaunchInfo.Invocation);
        if (enclosingMethod is null || !enclosingMethod.IsGenericMethod)
            return [];

        var enclosingOriginal = enclosingMethod.OriginalDefinition;

        // Step 2: Build the mapping from enclosing method type params to
        // kernel method type params. For example, if the enclosing method
        // is UsingAbstractFunction<T, TOp> and the kernel is
        // CalculatorKernel<T, TOp>, the mapping is positional via the
        // type parameter symbols used at the call site.
        var paramMapping = BuildTypeParamMapping(
            enclosingMethod, kernelMethod);
        if (paramMapping is null)
            return [];

        // Step 3: Walk all syntax trees to find callers of the enclosing
        // method with concrete type arguments.
        var results = new List<KernelSpecialization>();
        var seen = new HashSet<string>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var walker = new CallerWalker(model, enclosingOriginal);
            walker.Visit(tree.GetRoot());

            foreach (var callerSymbol in walker.ConcreteCallers)
            {
                // Map the enclosing method's concrete type args through
                // to the kernel's type parameter positions.
                var kernelTypeArgs = MapTypeArguments(
                    callerSymbol, paramMapping);
                if (kernelTypeArgs is null)
                    continue;

                var suffix = MangleTypeArgs(kernelTypeArgs.Value);
                if (!seen.Add(suffix))
                    continue;  // deduplicate

                results.Add(new KernelSpecialization(
                    kernelTypeArgs.Value, suffix));
            }
        }

        return results;
    }

    /// <summary>
    /// Finds the <see cref="IMethodSymbol"/> of the method containing
    /// the given syntax node.
    /// </summary>
    private static IMethodSymbol? FindEnclosingMethod(
        CSharpCompilation compilation,
        SyntaxNode node)
    {
        var model = compilation.GetSemanticModel(node.SyntaxTree);

        // Walk up to find the enclosing method/local-function declaration
        for (var current = node.Parent; current is not null;
             current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax methodDecl:
                    return model.GetDeclaredSymbol(methodDecl);
                case LocalFunctionStatementSyntax localFunc:
                    return model.GetDeclaredSymbol(localFunc) as IMethodSymbol;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds a mapping from enclosing method type parameter ordinals to
    /// kernel method type parameter ordinals. Returns null if the mapping
    /// cannot be established (e.g., the kernel uses type params not from
    /// the enclosing method).
    /// </summary>
    /// <returns>
    /// An array indexed by enclosing method type param ordinal, where each
    /// value is the kernel type param ordinal that should receive the
    /// concrete type argument. -1 means the enclosing param is not used
    /// by the kernel.
    /// </returns>
    private static int[]? BuildTypeParamMapping(
        IMethodSymbol enclosingMethod,
        IMethodSymbol kernelMethod)
    {
        // Map: enclosing type param ordinal → kernel type param ordinal
        var mapping = new int[enclosingMethod.TypeParameters.Length];
        Array.Fill(mapping, -1);

        for (int ki = 0; ki < kernelMethod.TypeArguments.Length; ki++)
        {
            var kernelArg = kernelMethod.TypeArguments[ki];
            if (kernelArg is not ITypeParameterSymbol typeParam)
                continue;

            // Find this type param in the enclosing method's type params
            bool found = false;
            for (int ei = 0; ei < enclosingMethod.TypeParameters.Length; ei++)
            {
                if (SymbolEqualityComparer.Default.Equals(
                    typeParam, enclosingMethod.TypeParameters[ei]))
                {
                    mapping[ei] = ki;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Type param not from the enclosing method — can't resolve
                return null;
            }
        }

        return mapping;
    }

    /// <summary>
    /// Maps a caller's concrete type arguments through the param mapping
    /// to produce the kernel's concrete type arguments.
    /// </summary>
    private static ImmutableArray<ITypeSymbol>? MapTypeArguments(
        IMethodSymbol callerSymbol,
        int[] paramMapping)
    {
        // Start with the kernel type args from the original kernel method.
        // For positions that have a mapping, substitute the caller's concrete arg.
        // For positions without a mapping, keep the original (should be concrete).

        // First, determine the total number of kernel type params
        int maxKernelIdx = -1;
        foreach (int ki in paramMapping)
        {
            if (ki > maxKernelIdx)
                maxKernelIdx = ki;
        }
        if (maxKernelIdx < 0)
            return null;

        var result = new ITypeSymbol[maxKernelIdx + 1];

        for (int ei = 0; ei < paramMapping.Length; ei++)
        {
            int ki = paramMapping[ei];
            if (ki < 0)
                continue;

            if (ei >= callerSymbol.TypeArguments.Length)
                return null;

            var concreteArg = callerSymbol.TypeArguments[ei];
            if (concreteArg is ITypeParameterSymbol)
            {
                // Nested generic — not supported in v1
                Console.Error.WriteLine(
                    $"Warning: nested generic call to " +
                    $"'{callerSymbol.ToDisplayString()}' — " +
                    $"type parameter not yet resolved.");
                return null;
            }

            result[ki] = concreteArg;
        }

        // Verify all positions are filled
        for (int i = 0; i < result.Length; i++)
        {
            if (result[i] is null)
                return null;
        }

        return ImmutableArray.Create(result);
    }

    /// <summary>
    /// Generates a mangled suffix from concrete type arguments.
    /// E.g., <c>[int, AdditionOp]</c> → <c>_Int32_AdditionOp</c>.
    /// </summary>
    private static string MangleTypeArgs(ImmutableArray<ITypeSymbol> typeArgs)
    {
        var parts = typeArgs.Select(t =>
        {
            var name = t.Name;
            if (t is INamedTypeSymbol { ContainingType: not null } nested)
                name = $"{nested.ContainingType.Name}_{name}";
            // Replace any remaining invalid identifier characters
            return name.Replace('.', '_').Replace('<', '_').Replace('>', '_');
        });
        return "_" + string.Join("_", parts);
    }

    /// <summary>
    /// Walks a syntax tree to find all invocations of a specific method's
    /// <see cref="IMethodSymbol.OriginalDefinition"/> that have concrete
    /// (non-type-parameter) type arguments.
    /// </summary>
    private sealed class CallerWalker(
        SemanticModel model,
        IMethodSymbol targetOriginal)
        : CSharpSyntaxWalker
    {
        public List<IMethodSymbol> ConcreteCallers { get; } = [];

        public override void VisitInvocationExpression(
            InvocationExpressionSyntax node)
        {
            var symbolInfo = model.GetSymbolInfo(node);
            if (symbolInfo.Symbol is IMethodSymbol method
                && SymbolEqualityComparer.Default.Equals(
                    method.OriginalDefinition, targetOriginal)
                && method.IsGenericMethod
                && method.TypeArguments.All(
                    t => t is not ITypeParameterSymbol))
            {
                ConcreteCallers.Add(method);
            }

            base.VisitInvocationExpression(node);
        }
    }
}
