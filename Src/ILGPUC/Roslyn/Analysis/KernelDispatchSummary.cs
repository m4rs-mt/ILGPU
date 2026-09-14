// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelDispatchSummary.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// A kernel launch that a public generic method of an assembly performs, described
/// for the builds of other assemblies.
/// </summary>
/// <param name="DispatchKey">
/// The key under which the assembly's dispatch code looks up launchers.
/// </param>
/// <param name="Kernel">The documentation ID of the kernel definition.</param>
/// <param name="Variant">The launch variant.</param>
/// <param name="SkippedParameters">
/// The number of leading kernel parameters that the launch does not pass.
/// </param>
/// <param name="TypeArguments">
/// The kernel's type arguments as templates over the type parameters of the method
/// that performs the launch (see <see cref="KernelDispatchSummary.Encode"/>).
/// </param>
sealed record DispatchLaunch(
    string DispatchKey,
    string Kernel,
    LaunchVariant Variant,
    int SkippedParameters,
    ImmutableArray<string> TypeArguments);

/// <summary>
/// The kernel launches of one public method.
/// </summary>
/// <param name="Method">The documentation ID of the method definition.</param>
/// <param name="Launches">The launches it performs, through any call depth.</param>
sealed record DispatchEntry(string Method, ImmutableArray<DispatchLaunch> Launches);

/// <summary>
/// Reads and writes the kernel launch summaries that assemblies carry in
/// <c>KernelDispatchSummaryAttribute</c>, and the type templates in them.
/// </summary>
/// <remarks>
/// A template is a type in which <c>!n</c> stands for the n-th type parameter of the
/// method that performs the launch, in the order of
/// <see cref="GenericContext.GetTypeParameters"/>. Named types are written as
/// <c>assembly|metadata name</c> followed by all their type arguments, those of
/// containing types first, in angle brackets; arrays and pointers append
/// <c>[]</c>, <c>[,]</c> or <c>*</c>.
/// </remarks>
static class KernelDispatchSummary
{
    #region Constants

    /// <summary>
    /// The metadata name of the attribute that carries a summary.
    /// </summary>
    public const string AttributeMetadataName =
        "ILGPU.Runtime.KernelDispatchSummaryAttribute";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    #endregion

    #region Serialization

    /// <summary>
    /// Serializes the summary of an assembly.
    /// </summary>
    public static string Serialize(IEnumerable<DispatchEntry> entries) =>
        JsonSerializer.Serialize(entries.ToArray(), JsonOptions);

    /// <summary>
    /// Reads the summaries of all assemblies that <paramref name="compilation"/>
    /// references, keyed by the documentation ID of the summarized method.
    /// </summary>
    public static Dictionary<string, DispatchEntry> Read(Compilation compilation)
    {
        var result = new Dictionary<string, DispatchEntry>(StringComparer.Ordinal);
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol
                assembly)
            {
                continue;
            }
            foreach (var attribute in assembly.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() !=
                        AttributeMetadataName ||
                    attribute.ConstructorArguments is not [{ Value: string json }])
                {
                    continue;
                }
                DispatchEntry[]? entries;
                try
                {
                    entries = JsonSerializer.Deserialize<DispatchEntry[]>(
                        json,
                        JsonOptions);
                }
                catch (JsonException)
                {
                    continue;
                }
                foreach (var entry in entries ?? [])
                    result[entry.Method] = entry;
            }
        }
        return result;
    }

    #endregion

    #region Templates

    /// <summary>
    /// Encodes <paramref name="type"/> as a template over
    /// <paramref name="parameters"/>, or returns null if it mentions other type
    /// parameters.
    /// </summary>
    public static string? Encode(
        ITypeSymbol type,
        ImmutableArray<ITypeParameterSymbol> parameters)
    {
        var builder = new StringBuilder();
        return EncodeTo(type, parameters, builder) ? builder.ToString() : null;
    }

    private static bool EncodeTo(
        ITypeSymbol type,
        ImmutableArray<ITypeParameterSymbol> parameters,
        StringBuilder builder)
    {
        switch (type)
        {
            case ITypeParameterSymbol parameter:
                for (int i = 0; i < parameters.Length; ++i)
                {
                    if (GenericContext.DefinitionComparer.Equals(
                        parameter,
                        parameters[i]))
                    {
                        builder
                            .Append('!')
                            .Append(i.ToString(CultureInfo.InvariantCulture));
                        return true;
                    }
                }
                return false;
            case IArrayTypeSymbol array:
                if (!EncodeTo(array.ElementType, parameters, builder))
                    return false;
                builder.Append('[').Append(',', array.Rank - 1).Append(']');
                return true;
            case IPointerTypeSymbol pointer:
                if (!EncodeTo(pointer.PointedAtType, parameters, builder))
                    return false;
                builder.Append('*');
                return true;
            case INamedTypeSymbol named:
                {
                    var definition = named.OriginalDefinition;
                    builder
                        .Append(definition.ContainingAssembly?.Identity.Name)
                        .Append('|')
                        .Append(GetMetadataName(definition));
                    var arguments = GenericContext.GetTypeArguments(named);
                    if (arguments.Length == 0)
                        return true;
                    builder.Append('<');
                    for (int i = 0; i < arguments.Length; ++i)
                    {
                        if (i > 0)
                            builder.Append(',');
                        if (!EncodeTo(arguments[i], parameters, builder))
                            return false;
                    }
                    builder.Append('>');
                    return true;
                }
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns the metadata name of a type definition, for example
    /// <c>N.Outer`1+Inner</c>.
    /// </summary>
    private static string GetMetadataName(INamedTypeSymbol definition)
    {
        if (definition.ContainingType is { } container)
            return $"{GetMetadataName(container)}+{definition.MetadataName}";
        var ns = definition.ContainingNamespace;
        return ns is null || ns.IsGlobalNamespace
            ? definition.MetadataName
            : $"{ns.ToDisplayString()}.{definition.MetadataName}";
    }

    /// <summary>
    /// Decodes a template, substituting <paramref name="arguments"/> for its type
    /// parameters, or returns null if a type cannot be found.
    /// </summary>
    public static ITypeSymbol? Decode(
        string template,
        Compilation compilation,
        ImmutableArray<ITypeSymbol> arguments)
    {
        int position = 0;
        var type = Decode(template, ref position, compilation, arguments);
        return position == template.Length ? type : null;
    }

    private static ITypeSymbol? Decode(
        string text,
        ref int position,
        Compilation compilation,
        ImmutableArray<ITypeSymbol> arguments)
    {
        ITypeSymbol? type;
        if (position < text.Length && text[position] == '!')
        {
            int start = ++position;
            while (position < text.Length && char.IsAsciiDigit(text[position]))
                ++position;
            if (!int.TryParse(
                    text.AsSpan(start, position - start),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int index) ||
                index >= arguments.Length)
            {
                return null;
            }
            type = arguments[index];
        }
        else
        {
            int separator = text.IndexOf('|', position);
            if (separator < 0)
                return null;
            var assemblyName = text[position..separator];
            position = separator + 1;
            int end = position;
            while (end < text.Length &&
                text[end] is not ('<' or '>' or ',' or '[' or '*'))
                ++end;
            var metadataName = text[position..end];
            position = end;

            var typeArguments = new List<ITypeSymbol>();
            if (position < text.Length && text[position] == '<')
            {
                do
                {
                    ++position;
                    var argument = Decode(text, ref position, compilation, arguments);
                    if (argument is null)
                        return null;
                    typeArguments.Add(argument);
                }
                while (position < text.Length && text[position] == ',');
                if (position >= text.Length || text[position] != '>')
                    return null;
                ++position;
            }

            var definition = FindType(compilation, assemblyName, metadataName);
            type = definition is null ? null : Construct(definition, typeArguments);
            if (type is null)
                return null;
        }

        // Array and pointer suffixes
        while (position < text.Length)
        {
            if (text[position] == '*')
            {
                type = compilation.CreatePointerTypeSymbol(type);
                ++position;
            }
            else if (text[position] == '[')
            {
                int rank = 1;
                ++position;
                while (position < text.Length && text[position] == ',')
                {
                    ++rank;
                    ++position;
                }
                if (position >= text.Length || text[position] != ']')
                    return null;
                ++position;
                type = compilation.CreateArrayTypeSymbol(type, rank);
            }
            else
            {
                break;
            }
        }
        return type;
    }

    /// <summary>
    /// Finds a type definition by assembly and metadata name.
    /// </summary>
    private static INamedTypeSymbol? FindType(
        Compilation compilation,
        string assemblyName,
        string metadataName)
    {
        if (compilation.Assembly.Identity.Name == assemblyName)
            return compilation.Assembly.GetTypeByMetadataName(metadataName);
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol
                {
                    Identity.Name: var name
                } assembly &&
                name == assemblyName)
            {
                return assembly.GetTypeByMetadataName(metadataName);
            }
        }
        return compilation.GetTypeByMetadataName(metadataName);
    }

    /// <summary>
    /// Constructs a type definition with all type arguments, those of containing
    /// types first.
    /// </summary>
    private static INamedTypeSymbol? Construct(
        INamedTypeSymbol definition,
        List<ITypeSymbol> typeArguments)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var current = definition;
             current is not null;
             current = current.ContainingType)
            chain.Add(current);
        chain.Reverse();
        if (chain.Sum(t => t.Arity) != typeArguments.Count)
            return null;

        INamedTypeSymbol? result = null;
        int offset = 0;
        foreach (var level in chain)
        {
            var current = result is null
                ? level
                : result.GetTypeMembers(level.Name, level.Arity).FirstOrDefault();
            if (current is null)
                return null;
            if (level.Arity > 0)
            {
                current = current.Construct(
                    [.. typeArguments.Skip(offset).Take(level.Arity)]);
                offset += level.Arity;
            }
            result = current;
        }
        return result;
    }

    #endregion
}
