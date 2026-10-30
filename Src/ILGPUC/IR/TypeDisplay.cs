// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: TypeDisplay.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ILGPUC.IR;

/// <summary>
/// Formats types for diagnostics in the C# spelling of kernel code, such as
/// <c>int*</c> or <c>ArrayView1D&lt;float, Dense&gt;</c>, instead of the spelling
/// of IR types.
/// </summary>
static class TypeDisplay
{
    /// <summary>
    /// Returns the C# keyword of the given type, if it has one.
    /// </summary>
    private static string? GetKeyword(Type type) =>
        Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => "bool",
            TypeCode.Char => "char",
            TypeCode.SByte => "sbyte",
            TypeCode.Byte => "byte",
            TypeCode.Int16 => "short",
            TypeCode.UInt16 => "ushort",
            TypeCode.Int32 => "int",
            TypeCode.UInt32 => "uint",
            TypeCode.Int64 => "long",
            TypeCode.UInt64 => "ulong",
            TypeCode.Single => "float",
            TypeCode.Double => "double",
            TypeCode.Decimal => "decimal",
            TypeCode.String => "string",
            _ when type.IsEnum => null,
            _ when type == typeof(void) => "void",
            _ when type == typeof(object) => "object",
            _ when type == typeof(IntPtr) => "nint",
            _ when type == typeof(UIntPtr) => "nuint",
            _ => null,
        };

    /// <summary>
    /// Formats the given managed type in C# spelling, for example <c>int*</c>,
    /// <c>ref float</c> or <c>Pair&lt;int&gt;</c>.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The formatted type.</returns>
    public static string Format(Type type)
    {
        if (type.IsByRef)
            return $"ref {Format(type.GetElementType()!)}";
        if (type.IsPointer)
            return $"{Format(type.GetElementType()!)}*";
        if (type.IsArray)
        {
            var commas = new string(',', type.GetArrayRank() - 1);
            return $"{Format(type.GetElementType()!)}[{commas}]";
        }
        if (GetKeyword(type) is { } keyword)
            return keyword;
        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name;
        int tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick >= 0)
            name = name[..tick];
        var arguments = string.Join(", ", type.GetGenericArguments().Select(Format));
        return $"{name}<{arguments}>";
    }

    /// <summary>
    /// Formats the given IR type in C# spelling where the managed type is not
    /// known: a primitive type as its signed C# type, a pointer as <c>int*</c>,
    /// a view as <c>ArrayView&lt;int&gt;</c> and a structure as a tuple of its
    /// fields.
    /// </summary>
    /// <param name="type">The IR type.</param>
    /// <returns>The formatted type.</returns>
    public static string Format(TypeValue? type) =>
        type switch
        {
            null => "void",
            PrimitiveType primitive => Format(primitive.BasicValueType),
            PointerType pointer => $"{Format(pointer.ElementType)}*",
            ViewType view => $"ArrayView<{Format(view.ElementType)}>",
            ArrayType array =>
                $"{Format(array.ElementType)}" +
                $"[{new string(',', array.NumDimensions - 1)}]",
            StructureType structure => FormatStructure(structure),
            VoidType => "void",
            StringType => "string",
            DelegateHandleType => "Delegate",
            _ => type.ToString(),
        };

    /// <summary>
    /// Formats the given basic value type as its signed C# type.
    /// </summary>
    /// <param name="basicValueType">The basic value type.</param>
    /// <returns>The formatted type.</returns>
    public static string Format(BasicValueType basicValueType) =>
        basicValueType switch
        {
            BasicValueType.Int1 => "bool",
            BasicValueType.Int8 => "sbyte",
            BasicValueType.Int16 => "short",
            BasicValueType.Int32 => "int",
            BasicValueType.Int64 => "long",
            BasicValueType.Float16 => "Half",
            BasicValueType.Float32 => "float",
            BasicValueType.Float64 => "double",
            _ => basicValueType.ToString(),
        };

    /// <summary>
    /// Formats a structure type as a tuple of its fields without padding.
    /// </summary>
    private static string FormatStructure(StructureType structure)
    {
        var fields = new List<string>();
        foreach (var field in structure.Fields)
        {
            if (field is not PaddingType)
                fields.Add(Format(field));
        }
        return $"({string.Join(", ", fields)})";
    }
}
