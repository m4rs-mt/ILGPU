// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AssemblyVisibility.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using System.Linq;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Determines which methods other assemblies can call.
/// </summary>
static class AssemblyVisibility
{
    /// <summary>
    /// Returns <see langword="true"/> if the given method can be called from
    /// other assemblies: it is public or protected and so are all containing
    /// types, or it is not private and the assembly grants other assemblies
    /// access to its internals. Protected methods are only accessible through
    /// derived types, so they are not visible if their type cannot be derived
    /// from in other assemblies. Other assemblies do not call the methods of an
    /// executable, so none of them is visible.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="compilation">The compilation declaring the method.</param>
    /// <param name="isLibrary">
    /// True if the compilation builds a library; false for an executable.
    /// </param>
    public static bool IsVisibleOutsideAssembly(
        IMethodSymbol method,
        Compilation compilation,
        bool isLibrary)
    {
        if (!isLibrary || method.MethodKind == MethodKind.LocalFunction)
            return false;

        bool isPublic = true;
        for (ISymbol? symbol = method;
            symbol is not null and not INamespaceSymbol;
            symbol = symbol.ContainingSymbol)
        {
            switch (symbol.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    break;
                case Accessibility.Internal:
                case Accessibility.ProtectedAndInternal:
                    isPublic = false;
                    break;
                default:
                    return false;
            }
        }

        bool internalsVisible = HasInternalsVisibleTo(compilation);
        if (!isPublic && !internalsVisible)
            return false;

        // Protected internal methods are accessible to friend assemblies
        // without deriving from their type
        bool onlyThroughDerivation = method.DeclaredAccessibility switch
        {
            Accessibility.Protected or Accessibility.ProtectedAndInternal => true,
            Accessibility.ProtectedOrInternal => !internalsVisible,
            _ => false,
        };
        return !onlyThroughDerivation
            || CanDeriveOutsideAssembly(method.ContainingType, internalsVisible);
    }

    /// <summary>
    /// Returns <see langword="true"/> if other assemblies can derive from the
    /// given type: it is neither sealed nor static and has a constructor they
    /// can access.
    /// </summary>
    private static bool CanDeriveOutsideAssembly(
        INamedTypeSymbol type,
        bool internalsVisible) =>
        type.TypeKind == TypeKind.Class
        && !type.IsSealed
        && !type.IsStatic
        && type.InstanceConstructors.Any(constructor =>
            constructor.DeclaredAccessibility switch
            {
                Accessibility.Public or Accessibility.Protected
                    or Accessibility.ProtectedOrInternal => true,
                Accessibility.Internal or Accessibility.ProtectedAndInternal =>
                    internalsVisible,
                _ => false,
            });

    /// <summary>
    /// Returns <see langword="true"/> if the assembly grants other assemblies
    /// access to its internals.
    /// </summary>
    private static bool HasInternalsVisibleTo(Compilation compilation) =>
        compilation.Assembly.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() ==
                "System.Runtime.CompilerServices.InternalsVisibleToAttribute");
}
