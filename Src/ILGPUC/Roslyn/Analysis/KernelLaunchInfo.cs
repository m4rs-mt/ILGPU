// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelLaunchInfo.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Represents a single launch call site found in user code.
/// </summary>
/// <param name="Invocation">The full invocation expression node.</param>
/// <param name="LaunchMethod">
/// The resolved Roslyn method symbol for the <c>Launch</c> call.
/// </param>
/// <param name="StreamExpression">
/// The receiver expression (the <c>AcceleratorStream</c> instance).
/// </param>
/// <param name="ExtentOrConfig">
/// The first argument: an index extent for auto-size variants,
/// or a <c>KernelConfig</c> for grouped launches.
/// </param>
/// <param name="KernelArgument">
/// The second argument: the lambda or method-group representing the kernel body.
/// </param>
/// <param name="Variant">The classified launch variant.</param>
record KernelLaunchInfo(
    InvocationExpressionSyntax Invocation,
    IMethodSymbol LaunchMethod,
    ExpressionSyntax StreamExpression,
    ExpressionSyntax ExtentOrConfig,
    SyntaxNode KernelArgument,
    LaunchVariant Variant);

/// <summary>
/// Classifies which <c>AcceleratorStream.Launch*</c> overload was called.
/// </summary>
enum LaunchVariant
{
    /// <summary>
    /// <c>Launch(Index1D, kernel)</c> — 1-D auto-config using an
    /// <c>int</c> or <c>Index1D</c> extent.
    /// </summary>
    Auto1D,

    /// <summary>
    /// <c>Launch(LongIndex1D, kernel)</c> — 1-D auto-config using a <c>long</c> extent.
    /// </summary>
    AutoLong1D,

    /// <summary>
    /// <c>Launch(KernelConfig, kernel)</c> — explicit grouped launch configuration.
    /// </summary>
    Grouped,

    /// <summary><c>Launch2D(Index2D, kernel)</c> — 2-D auto-config.</summary>
    Auto2D,

    /// <summary>
    /// <c>Launch2D&lt;TStride&gt;(Index2D, kernel)</c> — 2-D auto-config with stride.
    /// </summary>
    Auto2DStride,

    /// <summary>
    /// <c>Launch2D(LongIndex2D, kernel)</c> — 2-D auto-config using long indices.
    /// </summary>
    AutoLong2D,

    /// <summary>
    /// <c>Launch2D&lt;TStride&gt;(LongIndex2D, kernel)</c> — 2-D auto-config with
    /// stride and long indices.
    /// </summary>
    AutoLong2DStride,

    /// <summary>
    /// <c>Launch3D(Index3D, kernel)</c> — 3-D auto-config.
    /// </summary>
    Auto3D,

    /// <summary>
    /// <c>Launch3D&lt;TStride&gt;(Index3D, kernel)</c> — 3-D auto-config with stride.
    /// </summary>
    Auto3DStride,

    /// <summary>
    /// <c>Launch3D(LongIndex3D, kernel)</c> — 3-D auto-config using long indices.
    /// </summary>
    AutoLong3D,

    /// <summary>
    /// <c>Launch3D&lt;TStride&gt;(LongIndex3D, kernel)</c> — 3-D auto-config with
    /// stride and long indices.
    /// </summary>
    AutoLong3DStride,
}
