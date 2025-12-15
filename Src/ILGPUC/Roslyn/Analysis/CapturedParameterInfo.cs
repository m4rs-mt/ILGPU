// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CapturedParameterInfo.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Describes a single parameter captured at a kernel launch site,
/// including its name, source expression, resolved type, and kind.
/// </summary>
/// <param name="Name">The parameter name used in the generated kernel signature.</param>
/// <param name="SourceExpression">
/// The syntax node from which this parameter was captured.
/// </param>
/// <param name="Type">The resolved Roslyn type symbol for this parameter.</param>
/// <param name="Kind">
/// Classification of the parameter for code generation purposes.
/// </param>
record CapturedParameterInfo(
    string Name,
    ExpressionSyntax SourceExpression,
    ITypeSymbol Type,
    ParameterKind Kind);

/// <summary>
/// Classifies a kernel parameter by its memory/value semantics.
/// </summary>
enum ParameterKind
{
    /// <summary>
    /// Scalar value type (int, float, bool, etc.).
    /// </summary>
    Primitive,

    /// <summary>
    /// ILGPU ArrayView (<see cref="ILGPU.ArrayView{T}"/>, ArrayView1D, etc.).
    /// </summary>
    View,

    /// <summary>
    /// Value-type struct that contains one or more view fields.
    /// </summary>
    StructWithViews,

    /// <summary>
    /// Plain value-type struct with no view fields.
    /// </summary>
    StructPlain,

    /// <summary>
    /// Raw pointer (IntPtr, UIntPtr, or pointer type).
    /// </summary>
    Pointer,
}
