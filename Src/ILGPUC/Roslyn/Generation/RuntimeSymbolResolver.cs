// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: RuntimeSymbolResolver.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Roslyn.Analysis;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace ILGPUC.Roslyn.Generation;

/// <summary>
/// Resolves Roslyn symbols to the runtime types and methods of a loaded assembly
/// that was compiled from the same source.
/// </summary>
/// <remarks>
/// Types are looked up by metadata name, so nested types (<c>Outer+Inner</c>),
/// generic types (<c>Pair`1</c>), types nested in generic types, arrays and
/// pointers resolve. Types declared in the compiled assembly are looked up there
/// first; other types are looked up in the assembly of the same name in the load
/// context of the compiled assembly, then in the default load context.
/// </remarks>
static class RuntimeSymbolResolver
{
    /// <summary>
    /// Resolves the runtime type of <paramref name="symbol"/>.
    /// </summary>
    /// <param name="assembly">The assembly compiled from the source.</param>
    /// <param name="symbol">The type symbol to resolve.</param>
    /// <returns>The runtime type, or <see langword="null"/> if not found.</returns>
    public static Type? ResolveType(Assembly assembly, ITypeSymbol symbol)
    {
        switch (symbol)
        {
            case IArrayTypeSymbol array:
                var element = ResolveType(assembly, array.ElementType);
                return element is null
                    ? null
                    : array.IsSZArray
                        ? element.MakeArrayType()
                        : element.MakeArrayType(array.Rank);
            case IPointerTypeSymbol pointer:
                return ResolveType(assembly, pointer.PointedAtType)?.MakePointerType();
            case INamedTypeSymbol named:
                return ResolveNamedType(assembly, named);
            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves a named type, constructing it if it or one of its containing types
    /// has type arguments.
    /// </summary>
    private static Type? ResolveNamedType(Assembly assembly, INamedTypeSymbol named)
    {
        var definition = ResolveDefinition(assembly, named.OriginalDefinition);
        if (definition is null)
            return null;
        if (!definition.IsGenericTypeDefinition)
            return definition;

        // Runtime generic arguments of nested types include those of all
        // containing types, outermost first.
        var symbols = GenericContext.GetTypeArguments(named);
        var arguments = new Type[symbols.Length];
        for (int i = 0; i < symbols.Length; ++i)
        {
            if (symbols[i] is ITypeParameterSymbol)
                return definition;
            var argument = ResolveType(assembly, symbols[i]);
            if (argument is null)
                return null;
            arguments[i] = argument;
        }
        return definition.MakeGenericType(arguments);
    }

    /// <summary>
    /// Resolves the runtime type definition of a type definition symbol.
    /// </summary>
    private static Type? ResolveDefinition(Assembly assembly, INamedTypeSymbol definition)
    {
        var metadataName = GetMetadataName(definition);
        var assemblyName = definition.ContainingAssembly?.Identity.Name;

        if (assemblyName is null || assemblyName == assembly.GetName().Name)
        {
            if (assembly.GetType(metadataName) is { } local)
                return local;
        }

        if (assemblyName is not null)
        {
            var context = AssemblyLoadContext.GetLoadContext(assembly);
            var candidates = (context?.Assemblies ?? [])
                .Concat(AppDomain.CurrentDomain.GetAssemblies())
                .Where(a => a.GetName().Name == assemblyName);
            foreach (var candidate in candidates)
            {
                if (candidate.GetType(metadataName) is { } type)
                    return type;
            }

            if (Type.GetType($"{metadataName}, {assemblyName}") is { } loaded)
                return loaded;
        }

        return assembly.GetType(metadataName) ?? Type.GetType(metadataName);
    }

    /// <summary>
    /// Returns the metadata name of a type definition, for example
    /// <c>N.Outer`1+Inner</c>.
    /// </summary>
    private static string GetMetadataName(INamedTypeSymbol definition)
    {
        var builder = new StringBuilder();
        if (definition.ContainingType is { } container)
        {
            builder.Append(GetMetadataName(container.OriginalDefinition));
            builder.Append('+');
        }
        else if (definition.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            builder.Append(ns.ToDisplayString());
            builder.Append('.');
        }
        builder.Append(definition.MetadataName);
        return builder.ToString();
    }

    /// <summary>
    /// Resolves the runtime method of <paramref name="symbol"/>, constructed with
    /// its type arguments if it is generic.
    /// </summary>
    /// <param name="assembly">The assembly compiled from the source.</param>
    /// <param name="symbol">The method symbol to resolve.</param>
    /// <returns>The runtime method, or <see langword="null"/> if not found.</returns>
    public static MethodInfo? ResolveMethod(Assembly assembly, IMethodSymbol symbol)
    {
        if (symbol.MethodKind == MethodKind.LocalFunction)
        {
            return ResolveLocalFunction(
                assembly,
                symbol.OriginalDefinition,
                symbol.Name,
                GenericContext.GetTypeArguments(symbol));
        }

        var type = ResolveType(assembly, symbol.ContainingType);
        if (type is null)
            return null;

        const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance |
            BindingFlags.DeclaredOnly;
        // Match overloads on the generic type definition, where parameter types
        // still mention the type's own type parameters as the symbol's do, then
        // map the match onto the constructed type
        var definition = symbol.OriginalDefinition;
        var definitionType = type.IsConstructedGenericType
            ? type.GetGenericTypeDefinition()
            : type;
        var candidates = definitionType.GetMethods(Flags)
            .Where(m => m.Name == symbol.MetadataName
                && m.GetGenericArguments().Length == definition.Arity
                && m.GetParameters().Length == definition.Parameters.Length)
            .ToArray();

        var method = candidates.Length == 1
            ? candidates[0]
            : candidates.FirstOrDefault(m => ParametersMatch(m, definition));
        if (method is null)
            return null;
        if (type.IsConstructedGenericType)
        {
            method = (MethodInfo?)MethodBase.GetMethodFromHandle(
                method.MethodHandle,
                type.TypeHandle);
            if (method is null)
                return null;
        }

        if (method.IsGenericMethodDefinition)
        {
            var arguments = new Type[symbol.TypeArguments.Length];
            for (int i = 0; i < arguments.Length; ++i)
            {
                var argument = ResolveType(assembly, symbol.TypeArguments[i]);
                if (argument is null)
                    return null;
                arguments[i] = argument;
            }
            method = method.MakeGenericMethod(arguments);
        }

        return method;
    }

    /// <summary>
    /// Resolves a local function, or the static local function an inline lambda
    /// was lifted into, for the given type arguments. The C# compiler emits it
    /// under a mangled name on the type containing its owner, or on a closure type
    /// nested in it; the type takes the type arguments of the containing types,
    /// and the method those of its enclosing methods and its own.
    /// </summary>
    /// <param name="assembly">The assembly compiled from the source.</param>
    /// <param name="definition">
    /// The local function definition, or the lambda a kernel was lifted from.
    /// </param>
    /// <param name="name">The name of the local function.</param>
    /// <param name="arguments">
    /// The type arguments, ordered as by <see cref="GenericContext.GetTypeParameters"/>.
    /// </param>
    /// <returns>The runtime method, or <see langword="null"/> if not found.</returns>
    public static MethodInfo? ResolveLocalFunction(
        Assembly assembly,
        ISymbol definition,
        string name,
        ImmutableArray<ITypeSymbol> arguments)
    {
        if (definition.ContainingType is not { } containingType ||
            ResolveDefinition(assembly, containingType.OriginalDefinition) is not
                { } typeDefinition)
        {
            return null;
        }

        // Lifted kernels have unique names; local functions are matched by owner
        string? owner = null;
        if (definition is IMethodSymbol { MethodKind: MethodKind.LocalFunction })
        {
            var host = definition.ContainingSymbol;
            while (host is IMethodSymbol
                {
                    MethodKind: MethodKind.LocalFunction or MethodKind.AnonymousFunction,
                })
            {
                host = host.ContainingSymbol;
            }
            owner = host.Name;
        }
        var types = assembly.GetTypes()
            .Where(t => t == typeDefinition || IsNestedIn(t, typeDefinition));
        var method = FindLocalFunction(types, owner, name);
        if (method is null)
            return null;

        int typeArity = GenericContext.GetTypeParameters(
            containingType.OriginalDefinition).Length;
        if (arguments.Length < typeArity)
            return null;
        if (typeArity > 0)
        {
            if (method.DeclaringType is not { IsGenericTypeDefinition: true } declaring ||
                declaring.GetGenericArguments().Length != typeArity ||
                ResolveTypes(assembly, arguments[..typeArity]) is not { } typeArguments)
            {
                return null;
            }
            method = (MethodInfo?)MethodBase.GetMethodFromHandle(
                method.MethodHandle,
                declaring.MakeGenericType(typeArguments).TypeHandle);
            if (method is null)
                return null;
        }

        var methodArguments = arguments[typeArity..];
        if (!method.IsGenericMethodDefinition)
            return methodArguments.Length == 0 ? method : null;
        if (method.GetGenericArguments().Length != methodArguments.Length ||
            ResolveTypes(assembly, methodArguments) is not { } resolved)
        {
            return null;
        }
        return method.MakeGenericMethod(resolved);
    }

    /// <summary>
    /// Resolves the runtime types of <paramref name="symbols"/>, or returns
    /// <see langword="null"/> if one cannot be resolved.
    /// </summary>
    private static Type[]? ResolveTypes(
        Assembly assembly,
        ImmutableArray<ITypeSymbol> symbols)
    {
        var types = new Type[symbols.Length];
        for (int i = 0; i < types.Length; ++i)
        {
            if (ResolveType(assembly, symbols[i]) is not { } type)
                return null;
            types[i] = type;
        }
        return types;
    }

    /// <summary>
    /// Finds a compiled local function in the given types. The C# compiler
    /// emits local functions as methods named
    /// <c>&lt;Owner&gt;g__Name|i_j</c>, where <c>Owner</c> is the member
    /// declaring them.
    /// </summary>
    /// <param name="types">The types to search.</param>
    /// <param name="owner">
    /// The name of the declaring member, or <see langword="null"/> to accept
    /// any member.
    /// </param>
    /// <param name="name">The name of the local function.</param>
    /// <returns>The method if exactly one matches; otherwise null.</returns>
    private static MethodInfo? FindLocalFunction(
        IEnumerable<Type> types,
        string? owner,
        string name)
    {
        var marker = $">g__{name}|";
        var prefix = owner is null ? "<" : $"<{owner}>";
        var matches = types
            .SelectMany(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.DeclaredOnly))
            .Where(m => m.Name.StartsWith(prefix, StringComparison.Ordinal)
                && m.Name.Contains(marker, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="type"/> is nested,
    /// directly or transitively, in <paramref name="outer"/>.
    /// </summary>
    private static bool IsNestedIn(Type type, Type outer)
    {
        for (var current = type.DeclaringType; current != null;
            current = current.DeclaringType)
        {
            if (current == outer)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the parameter types of a runtime method
    /// match the parameter types of a method definition symbol.
    /// </summary>
    private static bool ParametersMatch(MethodInfo method, IMethodSymbol definition)
    {
        var parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; ++i)
        {
            var runtimeType = parameters[i].ParameterType;
            if (runtimeType.IsByRef)
                runtimeType = runtimeType.GetElementType()!;
            if (!TypesMatch(runtimeType, definition.Parameters[i].Type))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> if a runtime type, which may mention generic
    /// parameters, has the same shape as a type symbol.
    /// </summary>
    private static bool TypesMatch(Type runtimeType, ITypeSymbol symbol)
    {
        switch (symbol)
        {
            case ITypeParameterSymbol parameter:
                return runtimeType.IsGenericParameter
                    && runtimeType.Name == parameter.Name;
            case IArrayTypeSymbol array:
                return runtimeType.IsArray
                    && runtimeType.GetArrayRank() == array.Rank
                    && TypesMatch(runtimeType.GetElementType()!, array.ElementType);
            case IPointerTypeSymbol pointer:
                return runtimeType.IsPointer
                    && TypesMatch(runtimeType.GetElementType()!, pointer.PointedAtType);
            case INamedTypeSymbol named:
                var definition = runtimeType.IsConstructedGenericType
                    ? runtimeType.GetGenericTypeDefinition()
                    : runtimeType;
                if (definition.FullName is { } fullName
                    && fullName != GetMetadataName(named.OriginalDefinition))
                {
                    return false;
                }
                if (definition.FullName is null
                    && definition.Name != named.MetadataName)
                {
                    return false;
                }
                var runtimeArguments = runtimeType.IsConstructedGenericType
                    ? runtimeType.GetGenericArguments()
                    : [];
                var symbolArguments = GenericContext.GetTypeArguments(named);
                if (runtimeArguments.Length == 0)
                    return true;
                return runtimeArguments.Length == symbolArguments.Length
                    && runtimeArguments
                        .Zip(symbolArguments)
                        .All(p => TypesMatch(p.First, p.Second));
            default:
                return false;
        }
    }
}
