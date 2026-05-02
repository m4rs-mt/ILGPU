// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelLibraryAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU;

/// <summary>
/// Marks an assembly as containing kernel-callable code that the ILGPU
/// frontend should walk eagerly when discovering the call graph for a
/// kernel compilation.
/// </summary>
/// <remarks>
/// <para>
/// Without this attribute, methods declared in the assembly may still be
/// compiled correctly — the frontend's lazy on-the-fly disassembly path
/// covers any method codegen actually reaches, and intrinsic remappings
/// (e.g., <c>System.Math.Abs</c> → <c>ILGPU.XMath.Abs</c>) are resolved at
/// codegen time regardless of whether the source method was visited
/// eagerly.
/// </para>
/// <para>
/// Apply this attribute to assemblies whose helpers are heavily reused
/// across many kernels in a single compiler session: the eager walk
/// populates the per-compiler-session disassembly cache once, so
/// subsequent kernels in the same session pay zero frontend cost for
/// the helpers in this assembly.
/// </para>
/// <para>
/// Assemblies referenced directly by a kernel-containing assembly are
/// auto-walked even without the attribute, so the common pattern of
/// <c>UserApp.csproj → MyHelpers.csproj → kernel.cs</c> works out of the
/// box. The attribute is only required for transitive references that
/// the frontend would otherwise classify as third-party.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class KernelLibraryAttribute : Attribute { }
