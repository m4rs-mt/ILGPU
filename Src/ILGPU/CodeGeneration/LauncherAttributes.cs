// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: LauncherAttributes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.CodeGeneration;

/// <summary>
/// Marks methods that act as wrappers around kernel launches.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class IsLauncherAttribute(bool isGrouped) : Attribute
{
    /// <summary>
    /// Returns true if the current launcher is a grouped launcher.
    /// </summary>
    public bool IsGrouped { get; } = isGrouped;
}

/// <summary>
/// Marks methods that will be directly replaced with launcher invocations.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ReplaceWithLauncherAttribute : Attribute;

/// <summary>
/// Marks methods that must not be called by ILGPU client applications.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MustNotBeCalledByClientAttribute : Attribute;
