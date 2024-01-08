// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: SymbolExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;

namespace ILGPU.Analyzers
{
    internal static class SymbolExtensions
    {
        /// <summary>
        /// Checks whether the symbol is a primitive type.
        /// </summary>
        /// <param name="typeSymbol">The type symbol to check.</param>
        /// <returns>True, if a primitive type.</returns>
        public static bool IsPrimitiveType(this ITypeSymbol typeSymbol) =>
            typeSymbol.SpecialType == SpecialType.System_SByte
            || typeSymbol.SpecialType == SpecialType.System_Byte
            || typeSymbol.SpecialType == SpecialType.System_Int16
            || typeSymbol.SpecialType == SpecialType.System_UInt16
            || typeSymbol.SpecialType == SpecialType.System_Int32
            || typeSymbol.SpecialType == SpecialType.System_UInt32
            || typeSymbol.SpecialType == SpecialType.System_Int64
            || typeSymbol.SpecialType == SpecialType.System_UInt64
            || typeSymbol.SpecialType == SpecialType.System_Single
            || typeSymbol.SpecialType == SpecialType.System_Double;

        /// <summary>
        /// Converts a primitive <see cref="SpecialType"/> to a <see cref="SyntaxKind"/>.
        /// </summary>
        /// <param name="specialType">The special type.</param>
        /// <returns>The syntax kind.</returns>
        public static SyntaxKind ToSyntaxKind(this SpecialType specialType) =>
            specialType switch
            {
                SpecialType.System_SByte => SyntaxKind.SByteKeyword,
                SpecialType.System_Byte => SyntaxKind.ByteKeyword,
                SpecialType.System_Int16 => SyntaxKind.ShortKeyword,
                SpecialType.System_UInt16 => SyntaxKind.UShortKeyword,
                SpecialType.System_Int32 => SyntaxKind.IntKeyword,
                SpecialType.System_UInt32 => SyntaxKind.UIntKeyword,
                SpecialType.System_Int64 => SyntaxKind.LongKeyword,
                SpecialType.System_UInt64 => SyntaxKind.ULongKeyword,
                SpecialType.System_Single => SyntaxKind.FloatKeyword,
                SpecialType.System_Double => SyntaxKind.DoubleKeyword,
                _ => throw new NotSupportedException(),
            };

        /// <summary>
        /// Converts an <see cref="Accessibility"/> to a <see cref="SyntaxKind"/>.
        /// </summary>
        /// <param name="accessibility">The accessibility.</param>
        /// <returns>The syntax kind.</returns>
        public static SyntaxKind ToSyntaxKind(this Accessibility accessibility) =>
            accessibility switch
            {
                Accessibility.Public => SyntaxKind.PublicKeyword,
                Accessibility.Private => SyntaxKind.PrivateKeyword,
                Accessibility.Internal => SyntaxKind.InternalKeyword,
                _ => throw new NotSupportedException(),
            };
    }
}
