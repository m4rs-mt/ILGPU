// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ExternalAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.CodeGeneration;

/// <summary>
/// Marks external methods that are opaque in the scope of the ILGPU compiler internals.
/// </summary>
/// <param name="name">The external name.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExternalAttribute(string name) : Attribute
{
    /// <summary>
    /// Returns the associated internal function name.
    /// </summary>
    public string Name { get; } = name;
}
