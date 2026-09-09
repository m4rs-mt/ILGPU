// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: GenericContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Helpers for the generic context of a definition: the type parameters that are
/// in scope in its body, the type arguments a reference supplies for them, and
/// substitution of closed type arguments into types that mention them.
/// </summary>
/// <remarks>
/// The type parameters of a definition are ordered from the outside in: those of
/// the containing types (outermost first), then those of the methods hosting a
/// local function (outermost first), then the definition's own type parameters.
/// <see cref="GetTypeArguments"/> returns the arguments of a reference in the same
/// order, so the two arrays can be zipped into a substitution.
/// </remarks>
static class GenericContext
{
    /// <summary>
    /// The display format used for keys: fully qualified, with type parameters,
    /// generic arguments, containing types and parameter types, without the
    /// <c>global::</c> prefix. Two different symbols never share a key.
    /// </summary>
    public static readonly SymbolDisplayFormat KeyFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle:
            SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
            SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeExplicitInterface,
        parameterOptions:
            SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.ExpandValueTuple);

    /// <summary>
    /// Compares definitions and type parameters. Local functions and their type
    /// parameters are compared by declaration, because the semantic model and
    /// the operation tree can hand out distinct, unequal symbol instances for
    /// the same local function. The type parameters of both parts of a partial
    /// method compare equal.
    /// </summary>
    public static IEqualityComparer<ISymbol> DefinitionComparer { get; } =
        new DeclarationComparer();

    /// <summary>
    /// Implements <see cref="DefinitionComparer"/>.
    /// </summary>
    private sealed class DeclarationComparer : IEqualityComparer<ISymbol>
    {
        public bool Equals(ISymbol? x, ISymbol? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (x is null || y is null)
                return false;
            var xKey = GetLocalKey(x);
            var yKey = GetLocalKey(y);
            if (xKey is not null || yKey is not null)
                return xKey == yKey;
            return SymbolEqualityComparer.Default.Equals(x, y);
        }

        public int GetHashCode(ISymbol obj) =>
            GetLocalKey(obj) is { } key
                ? key.GetHashCode(StringComparison.Ordinal)
                : SymbolEqualityComparer.Default.GetHashCode(obj);

        /// <summary>
        /// Returns a declaration key for local functions and their type
        /// parameters, and <see langword="null"/> for all other symbols.
        /// </summary>
        private static string? GetLocalKey(ISymbol symbol) =>
            symbol switch
            {
                IMethodSymbol { MethodKind: MethodKind.LocalFunction } method =>
                    $"local|{GetDeclarationKey(method.OriginalDefinition)}",
                ITypeParameterSymbol
                {
                    DeclaringMethod.MethodKind: MethodKind.LocalFunction
                } parameter =>
                    $"local|{GetDeclarationKey(parameter.DeclaringMethod!)}|" +
                    $"{parameter.Ordinal}",

                // Both parts of a partial method have their own type parameters;
                // identify them with those of the defining part
                ITypeParameterSymbol
                {
                    DeclaringMethod: { } method
                } parameter when method.PartialDefinitionPart is not null ||
                    method.PartialImplementationPart is not null =>
                    "partial|" +
                    $"{GetDeclarationKey(method.PartialDefinitionPart ?? method)}|" +
                    $"{parameter.Ordinal}",
                _ => null,
            };

        /// <summary>
        /// Returns the source location of a declaration as text.
        /// </summary>
        private static string GetDeclarationKey(ISymbol symbol) =>
            symbol.DeclaringSyntaxReferences is [var reference, ..]
                ? $"{reference.SyntaxTree.FilePath}|{reference.Span.Start}"
                : symbol.ToDisplayString(KeyFormat);
    }

    /// <summary>
    /// Returns the type parameters that are in scope in the body of
    /// <paramref name="definition"/>, ordered from the outside in.
    /// </summary>
    public static ImmutableArray<ITypeParameterSymbol> GetTypeParameters(
        ISymbol definition)
    {
        var result = ImmutableArray.CreateBuilder<ITypeParameterSymbol>();
        var type = definition as INamedTypeSymbol ?? definition.ContainingType;
        AddContainingTypeParameters(type, result);

        if (definition is IMethodSymbol method)
        {
            var hosts = new List<IMethodSymbol>();
            for (var host = method.ContainingSymbol as IMethodSymbol;
                 host is not null;
                 host = host.ContainingSymbol as IMethodSymbol)
            {
                hosts.Add(host);
            }
            for (int i = hosts.Count - 1; i >= 0; --i)
                result.AddRange(hosts[i].TypeParameters);
            result.AddRange(method.TypeParameters);
        }

        return result.ToImmutable();
    }

    /// <summary>
    /// Adds the type parameters of <paramref name="type"/> and of all its
    /// containing types, outermost first.
    /// </summary>
    private static void AddContainingTypeParameters(
        INamedTypeSymbol? type,
        ImmutableArray<ITypeParameterSymbol>.Builder result)
    {
        if (type is null)
            return;
        AddContainingTypeParameters(type.ContainingType, result);
        result.AddRange(type.OriginalDefinition.TypeParameters);
    }

    /// <summary>
    /// Returns the type arguments that the (possibly constructed) reference
    /// <paramref name="reference"/> supplies for the type parameters of its
    /// definition, in the order of <see cref="GetTypeParameters"/>. The arguments
    /// are expressed in the context of the code containing the reference.
    /// </summary>
    public static ImmutableArray<ITypeSymbol> GetTypeArguments(ISymbol reference)
    {
        var result = ImmutableArray.CreateBuilder<ITypeSymbol>();
        var type = reference as INamedTypeSymbol ?? reference.ContainingType;
        AddContainingTypeArguments(type, result);

        if (reference is IMethodSymbol method)
        {
            // Local functions see the type parameters of their hosts unchanged.
            // A constructed local function does not report its host as its
            // containing symbol, so walk from the definition.
            var hosts = new List<IMethodSymbol>();
            for (var host = method.OriginalDefinition.ContainingSymbol as IMethodSymbol;
                 host is not null;
                 host = host.ContainingSymbol as IMethodSymbol)
            {
                hosts.Add(host);
            }
            for (int i = hosts.Count - 1; i >= 0; --i)
                result.AddRange(hosts[i].TypeParameters);
            result.AddRange(method.TypeArguments);
        }

        return result.ToImmutable();
    }

    /// <summary>
    /// Adds the type arguments of <paramref name="type"/> and of all its
    /// containing types, outermost first.
    /// </summary>
    private static void AddContainingTypeArguments(
        INamedTypeSymbol? type,
        ImmutableArray<ITypeSymbol>.Builder result)
    {
        if (type is null)
            return;
        AddContainingTypeArguments(type.ContainingType, result);
        result.AddRange(type.TypeArguments);
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="type"/> mentions any type
    /// parameter, including inside constructed, array, pointer or containing types.
    /// </summary>
    public static bool ContainsTypeParameter(ITypeSymbol type) =>
        type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
            IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
            INamedTypeSymbol named =>
                named.TypeArguments.Any(ContainsTypeParameter)
                || named.ContainingType is { } container
                    && ContainsTypeParameter(container),
            _ => false,
        };

    /// <summary>
    /// Returns <see langword="true"/> if any of <paramref name="types"/> mentions a
    /// type parameter.
    /// </summary>
    public static bool ContainsTypeParameter(IEnumerable<ITypeSymbol> types) =>
        types.Any(ContainsTypeParameter);

    /// <summary>
    /// Returns <see langword="true"/> if the method <paramref name="method"/> still
    /// mentions a type parameter in its own type arguments or in those of its
    /// containing types.
    /// </summary>
    public static bool IsOpen(IMethodSymbol method) =>
        ContainsTypeParameter(GetTypeArguments(method));

    /// <summary>
    /// Returns how deeply generic arguments are nested in <paramref name="type"/>:
    /// <c>int</c> is 1, <c>Box&lt;int&gt;</c> is 2, <c>Box&lt;Box&lt;int&gt;&gt;</c>
    /// is 3. Used to stop polymorphic recursion.
    /// </summary>
    public static int GetNestingDepth(ITypeSymbol type) =>
        type switch
        {
            IArrayTypeSymbol array => 1 + GetNestingDepth(array.ElementType),
            IPointerTypeSymbol pointer => 1 + GetNestingDepth(pointer.PointedAtType),
            INamedTypeSymbol named =>
                1 + Math.Max(
                    named.TypeArguments.Select(GetNestingDepth).DefaultIfEmpty(0).Max(),
                    named.ContainingType is { } container
                        ? GetNestingDepth(container) - 1
                        : 0),
            _ => 1,
        };

    /// <summary>
    /// Creates a substitution mapping the type parameters of
    /// <paramref name="definition"/> to <paramref name="arguments"/>.
    /// </summary>
    public static Dictionary<ITypeParameterSymbol, ITypeSymbol> CreateSubstitution(
        ISymbol definition,
        ImmutableArray<ITypeSymbol> arguments)
    {
        var parameters = GetTypeParameters(definition);
        var map = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(
            parameters.Length,
            DefinitionComparer);
        for (int i = 0; i < parameters.Length && i < arguments.Length; ++i)
            map[parameters[i]] = arguments[i];
        return map;
    }

    /// <summary>
    /// Replaces every type parameter in <paramref name="type"/> that is mapped by
    /// <paramref name="substitution"/>. Unmapped type parameters stay as they are.
    /// </summary>
    public static ITypeSymbol Substitute(
        Compilation compilation,
        ITypeSymbol type,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol> substitution)
    {
        if (!ContainsTypeParameter(type))
            return type;

        switch (type)
        {
            case ITypeParameterSymbol parameter:
                return substitution.TryGetValue(parameter, out var argument)
                    ? argument
                    : parameter;
            case IArrayTypeSymbol array:
                return compilation.CreateArrayTypeSymbol(
                    Substitute(compilation, array.ElementType, substitution),
                    array.Rank);
            case IPointerTypeSymbol pointer:
                return compilation.CreatePointerTypeSymbol(
                    Substitute(compilation, pointer.PointedAtType, substitution));
            case INamedTypeSymbol named:
                return SubstituteNamed(compilation, named, substitution);
            default:
                return type;
        }
    }

    /// <summary>
    /// Substitutes into a named type, including its containing types.
    /// </summary>
    private static INamedTypeSymbol SubstituteNamed(
        Compilation compilation,
        INamedTypeSymbol named,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol> substitution)
    {
        var definition = named.OriginalDefinition;
        var target = definition;
        if (named.ContainingType is { } container && ContainsTypeParameter(container))
        {
            var newContainer = SubstituteNamed(compilation, container, substitution);
            target = newContainer
                .GetTypeMembers(definition.Name, definition.Arity)
                .First(t => SymbolEqualityComparer.Default.Equals(
                    t.OriginalDefinition, definition));
        }
        else if (named.ContainingType is { } unchangedContainer)
        {
            target = unchangedContainer
                .GetTypeMembers(definition.Name, definition.Arity)
                .FirstOrDefault(t => SymbolEqualityComparer.Default.Equals(
                    t.OriginalDefinition, definition)) ?? definition;
        }

        if (named.Arity == 0)
            return target;

        var arguments = named.TypeArguments
            .Select(t => Substitute(compilation, t, substitution))
            .ToArray();
        return target.Construct(arguments);
    }

    /// <summary>
    /// Constructs the method <paramref name="definition"/> with the closed type
    /// arguments <paramref name="arguments"/>, which are ordered as by
    /// <see cref="GetTypeParameters"/>.
    /// </summary>
    public static IMethodSymbol ConstructMethod(
        Compilation compilation,
        IMethodSymbol definition,
        ImmutableArray<ITypeSymbol> arguments)
    {
        definition = definition.OriginalDefinition;
        var substitution = CreateSubstitution(definition, arguments);

        var method = definition;
        if (definition.ContainingType is { } containingType
            && ContainsTypeParameter(containingType))
        {
            var closedType = (INamedTypeSymbol)Substitute(
                compilation, containingType, substitution);
            method = closedType.GetMembers(definition.Name)
                .OfType<IMethodSymbol>()
                .First(m => SymbolEqualityComparer.Default.Equals(
                    m.OriginalDefinition, definition));
        }

        if (definition.Arity == 0)
            return method;

        var ownArguments = definition.TypeParameters
            .Select(t => Substitute(compilation, t, substitution))
            .ToArray();
        return method.Construct(ownArguments);
    }

    /// <summary>
    /// Returns a stable textual key for a list of type arguments.
    /// </summary>
    public static string GetKey(IEnumerable<ITypeSymbol> arguments) =>
        string.Join(", ", arguments.Select(t => t.ToDisplayString(KeyFormat)));

    /// <summary>
    /// Returns a readable identifier fragment for <paramref name="type"/>, such as
    /// <c>Int32</c>, <c>Pair_Single</c> or <c>Single_Array</c>.
    /// </summary>
    public static string Mangle(ITypeSymbol type) =>
        type switch
        {
            IArrayTypeSymbol array => $"{Mangle(array.ElementType)}_Array",
            IPointerTypeSymbol pointer => $"{Mangle(pointer.PointedAtType)}_Ptr",
            INamedTypeSymbol named => MangleNamed(named),
            _ => SanitizeIdentifier(type.Name),
        };

    /// <summary>
    /// Mangles a named type with its containing types and type arguments.
    /// </summary>
    private static string MangleNamed(INamedTypeSymbol named)
    {
        var name = SanitizeIdentifier(named.Name);
        if (named.ContainingType is { } container)
            name = $"{MangleNamed(container)}_{name}";
        if (named.Arity > 0)
            name += "_" + string.Join("_", named.TypeArguments.Select(Mangle));
        return name;
    }

    /// <summary>
    /// Replaces every character that is not valid in a C# identifier by an
    /// underscore.
    /// </summary>
    public static string SanitizeIdentifier(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
            builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        return builder.ToString();
    }

    /// <summary>
    /// Returns a short hash of <paramref name="text"/> that is stable across
    /// processes, for disambiguating generated names.
    /// </summary>
    public static string GetStableHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }
}
