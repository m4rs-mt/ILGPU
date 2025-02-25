// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueKind.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Reflection;

namespace ILGPUC.IR;

/// <summary>
/// Marks value classes with specific value kinds.
/// </summary>
/// <param name="kind">The value kind.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
sealed class ValueKindAttribute(ValueKind kind) : Attribute
{
    /// <summary>
    /// Returns the value kind.
    /// </summary>
    public ValueKind Kind { get; } = kind;
}

/// <summary>
/// Utility methods for <see cref="ValueKind"/> enumeration values.
/// </summary>
static partial class ValueKinds
{
    /// <summary>
    /// Gets the value kind of the value type specified.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <returns>The determined value kind.</returns>
    public static ValueKind GetValueKind<TValue>()
        where TValue : Value =>
        typeof(TValue).GetCustomAttribute<ValueKindAttribute>().AsNotNull().Kind;

    /// <summary>
    /// Gets the value kind of the type specified.
    /// </summary>
    /// <returns>The determined value kind.</returns>
    public static ValueKind? GetValueKind(Type type) =>
        type.GetCustomAttribute<ValueKindAttribute>()?.Kind;
}
