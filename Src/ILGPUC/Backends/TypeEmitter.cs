// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;

namespace ILGPUC.Backends;

/// <summary>
/// Handles emission of type declarations and type names.
/// Ensures types are emitted in the correct dependency order.
/// </summary>
sealed class TypeEmitter(GenerationContext context)
{
    private readonly ValueSet<Module, TypeValue> _emittedTypes =
        context.Module.CreateSet<TypeValue>();
    private readonly ValueMap<Module, TypeValue, string> _typeNames =
        context.Module.CreateMap<TypeValue, string>();

    /// <summary>
    /// Emits all type declarations for the module in dependency order.
    /// </summary>
    public void EmitTypeDeclarations()
    {
        // Sort types in dependency order (types that depend on others come after)
        var sortedTypes = GetSortedTypes();

        // Emit each type declaration
        foreach (var type in sortedTypes)
            EmitTypeDeclaration(type);
    }

    /// <summary>
    /// Gets the name to use for a type.
    /// </summary>
    public string GetTypeName(BasicValueType type) =>
        context.LanguageConfig.GetPrimitiveTypeName(type);

    /// <summary>
    /// Gets the name to use for a type.
    /// </summary>
    public string GetTypeName(ArithmeticBasicValueType type) =>
        context.LanguageConfig.GetPrimitiveTypeName(type);

    /// <summary>
    /// Gets the name to use for a type.
    /// </summary>
    public string GetTypeName(TypeValue type)
    {
        return type switch
        {
            PrimitiveType primitive => context.LanguageConfig.GetPrimitiveTypeName(
                primitive.BasicValueType),
            PointerType pointer => GetPointerTypeName(pointer),
            ViewType view => GetViewTypeName(view),
            StructureType structure => GetStructureTypeName(structure),
            PaddingType padding => context.LanguageConfig.GetPrimitiveTypeName(
                padding.BasicValueType),
            VoidType => "void",
            _ => throw new NotSupportedException($"Type {type} not supported")
        };
    }

    /// <summary>
    /// Emits a type declaration if needed.
    /// </summary>
    private void EmitTypeDeclaration(TypeValue type)
    {
        if (_emittedTypes.Contains(type))
            return;

        _emittedTypes.Add(type);

        // Only structures need declarations
        if (type is StructureType structure)
        {
            EmitStructureDeclaration(structure);
        }
    }

    /// <summary>
    /// Emits a structure type declaration.
    /// </summary>
    private void EmitStructureDeclaration(StructureType structure)
    {
        var typeName = GetStructureTypeName(structure);

        context.WriteLine($"{context.LanguageConfig.StructKeyword} {typeName}");
        context.OpenScope();

        // Emit each field
        for (int i = 0; i < structure.NumFields; i++)
        {
            var fieldType = structure.Fields[i];
            var fieldName = StructureType.GetFieldName(i);
            var fieldTypeName = GetTypeName(fieldType);

            context.WriteLine($"{context.LanguageConfig.StructFieldModifier}{fieldTypeName} {fieldName};");
        }

        context.PopIndent();
        context.WriteLine("};");

        // C99 / OpenCL C require either the `struct` tag or a typedef
        // alias at every reference site (`struct foo x;` not `foo x;`).
        // The codegen always uses the bare name elsewhere, so emit a
        // matching `typedef struct foo foo;` alias for languages that
        // need it. C++ backends and C# leave the flag false and skip
        // this line.
        if (context.LanguageConfig.EmitStructTypedefAlias)
        {
            context.WriteLine(
                $"typedef {context.LanguageConfig.StructKeyword} " +
                $"{typeName} {typeName};");
        }

        context.WriteLine();
    }

    /// <summary>
    /// Gets the name for a structure type.
    /// </summary>
    private string GetStructureTypeName(StructureType structure)
    {
        if (_typeNames.TryGetValue(structure, out var name))
            return name;

        // Generate a unique name
        name = $"struct_{structure.Id}";
        _typeNames[structure] = name;
        return name;
    }

    /// <summary>
    /// Gets the name for a pointer type with proper address space qualification.
    /// </summary>
    /// <remarks>
    /// This method generates language-specific pointer syntax with address space keywords.
    /// Examples:
    /// - Metal: "device int*", "threadgroup float*"
    /// - OpenCL: "global int*", "local float*"
    /// - CUDA: "int*", "__shared__ float*"
    /// </remarks>
    private string GetPointerTypeName(PointerType pointer)
    {
        var elementTypeName = GetTypeName(pointer.ElementType);
        var addrSpace = pointer.AddressSpace;
        var addrSpaceKeyword = context.LanguageConfig.GetAddressSpaceKeyword(addrSpace);

        // All languages use pointer star syntax
        if (string.IsNullOrEmpty(addrSpaceKeyword))
            return $"{elementTypeName}*";

        return $"{addrSpaceKeyword} {elementTypeName}*";
    }

    /// <summary>
    /// Gets the name for a view type, delegating to the language configuration.
    /// </summary>
    /// <remarks>
    /// On backends that emit views as plain pointers (default), the view
    /// inherits its address space from the IR — shared/threadgroup/local
    /// views must emit with the correct address-space qualifier or
    /// compilation fails (Metal rejects bare <c>int*</c>).
    /// </remarks>
    private string GetViewTypeName(ViewType view)
    {
        var elementTypeName = GetTypeName(view.ElementType);
        var addrSpaceKeyword = context.LanguageConfig.GetAddressSpaceKeyword(
            view.AddressSpace);
        return context.LanguageConfig.GetViewTypeName(
            elementTypeName, addrSpaceKeyword);
    }

    /// <summary>
    /// Performs a topological sort of types based on dependencies.
    /// </summary>
    private List<TypeValue> GetSortedTypes()
    {
        var sorted = new List<TypeValue>();
        var visited = context.Module.CreateSet<TypeValue>();
        var visiting = context.Module.CreateSet<TypeValue>();

        void Visit(TypeValue type)
        {
            if (visited.Contains(type))
                return;

            if (visiting.Contains(type))
            {
                // Circular dependency - this shouldn't happen in valid IR
                return;
            }

            visiting.Add(type);

            // Visit dependencies first
            if (type is StructureType structure)
            {
                foreach (var fieldType in structure.Fields)
                {
                    if (fieldType is StructureType)
                        Visit(fieldType);
                }
            }

            visiting.Remove(type);
            visited.Add(type);
            sorted.Add(type);
        }

        // Visit all structure types
        foreach (var type in context.Module.Types)
        {
            if (type is not StructureType)
                continue;
            Visit(type);
        }

        return sorted;
    }
}
