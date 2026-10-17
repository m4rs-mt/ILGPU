// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: DelegateFieldException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ILGPUC.IR;

/// <summary>
/// The exception that is thrown when kernel code uses a structure, a tuple or
/// a closure with a field of a delegate type. Delegates are bound to their
/// targets at compile time and cannot be stored in memory.
/// </summary>
/// <param name="containingType">The type with the delegate field.</param>
/// <param name="field">The delegate field.</param>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Every error refers to a type and a field")]
sealed class DelegateFieldException(Type containingType, FieldInfo field)
    : NotSupportedException(string.Format(
        ErrorMessages.KernelDiagnosticDelegateFieldType,
        containingType,
        field.Name))
{
    /// <summary>
    /// Returns the type with the delegate field.
    /// </summary>
    public Type ContainingType { get; } = containingType;

    /// <summary>
    /// Returns the delegate field.
    /// </summary>
    public FieldInfo Field { get; } = field;
}
