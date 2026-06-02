// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
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

        // Languages with explicit field offsets place every field at its IR
        // offset when the natural layout would deviate from it
        bool explicitOffsets = context.LanguageConfig.SupportsExplicitFieldOffsets
            && !HasNaturalLayout(structure);
        if (explicitOffsets)
        {
            context.WriteLine(
                "[System.Runtime.InteropServices.StructLayout(" +
                "System.Runtime.InteropServices.LayoutKind.Explicit, " +
                $"Size = {structure.Size})]");
        }
        context.WriteLine($"{context.LanguageConfig.StructKeyword} {typeName}");
        context.OpenScope();

        // Emit each field. Flattened nested structures place their first field
        // at the alignment of the nested structure, which can exceed the
        // natural alignment of the field itself: raise the field alignment
        // explicitly so the native layout matches the IR (host) offsets.
        int naturalEnd = 0;
        for (int i = 0; i < structure.NumFields; i++)
        {
            var fieldType = structure.Fields[i];
            var fieldName = StructureType.GetFieldName(i);
            var fieldTypeName =
                fieldType.BasicValueType == BasicValueType.Int1 &&
                context.LanguageConfig.BoolStorageTypeName is string boolName
                ? boolName
                : GetTypeName(fieldType);
            int offset = structure.GetOffset(i);
            var alignment = explicitOffsets
                ? string.Empty
                : GetFieldAlignment(
                    structure,
                    naturalEnd,
                    offset,
                    fieldType.Alignment);
            naturalEnd = offset + fieldType.Size;

            var fieldOffset = explicitOffsets
                ? $"[System.Runtime.InteropServices.FieldOffset({offset})] "
                : string.Empty;
            context.WriteLine(
                $"{fieldOffset}{context.LanguageConfig.StructFieldModifier}" +
                $"{fieldTypeName} {fieldName}{alignment};");
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
    /// Returns the alignment suffix required to place a field at the given
    /// offset, or an empty string if the natural alignment suffices.
    /// </summary>
    private string GetFieldAlignment(
        StructureType structure,
        int naturalEnd,
        int offset,
        int naturalAlignment)
    {
        if (Align(naturalEnd, naturalAlignment) == offset)
            return string.Empty;

        // Languages without field alignment control keep their own layout
        var config = context.LanguageConfig;
        if (config.FormatFieldAlignment(naturalAlignment) is null)
            return string.Empty;

        for (int alignment = naturalAlignment * 2; alignment <= offset;
            alignment *= 2)
        {
            if (Align(naturalEnd, alignment) == offset)
                return config.FormatFieldAlignment(alignment) ?? string.Empty;
        }
        throw new NotSupportedException(
            $"The layout of structure type {structure} cannot be represented " +
            $"in the target language (field offset {offset})");
    }

    /// <summary>
    /// Returns true if laying out the fields one after another at their
    /// natural alignments yields the IR offsets of all fields.
    /// </summary>
    private static bool HasNaturalLayout(StructureType structure)
    {
        int naturalEnd = 0;
        for (int i = 0; i < structure.NumFields; i++)
        {
            var fieldType = structure.Fields[i];
            int offset = structure.GetOffset(i);
            if (Align(naturalEnd, fieldType.Alignment) != offset)
                return false;
            naturalEnd = offset + fieldType.Size;
        }
        return true;
    }

    /// <summary>
    /// Aligns the given offset to the given power-of-two alignment.
    /// </summary>
    private static int Align(int offset, int alignment) =>
        (offset + alignment - 1) & ~(alignment - 1);

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
    /// This method generates language-specific pointer syntax with address
    /// space keywords. Examples:
    /// - Metal: "device int*", "threadgroup float*"
    /// - OpenCL: "global int*", "local float*"
    /// - CUDA / HIP: always "int*" — pointer types in CUDA C++ don't carry
    ///   address-space qualifiers; the address space is implied by the
    ///   pointee's storage and is tracked by the compiler. Emitting
    ///   <c>__shared__ int* p</c> as a local variable would put the
    ///   variable <c>p</c> itself in shared memory, causing all threads to
    ///   share a single pointer cell and racing on assignment — which
    ///   manifested as scrambled radix-sort output (e.g. all 8 threads
    ///   reading the same shared slot). The per-language override lives
    ///   on <see cref="LanguageConfiguration.GetPointerTypeName"/>.
    /// </remarks>
    private string GetPointerTypeName(PointerType pointer)
    {
        var elementTypeName = GetTypeName(pointer.ElementType);
        var addrSpaceKeyword = context.LanguageConfig.GetAddressSpaceKeyword(
            pointer.AddressSpace);
        return context.LanguageConfig.GetPointerTypeName(
            elementTypeName, addrSpaceKeyword);
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
