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

using System;
using System.Linq;

namespace ILGPUC.IR;

/// <summary>
/// Formats types for diagnostics in the C# spelling of kernel code, such as
/// <c>int*</c> or <c>ArrayView1D&lt;float, Dense&gt;</c>.
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
}
