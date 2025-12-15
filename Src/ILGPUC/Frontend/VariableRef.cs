// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: VariableRef.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Frontend;

/// <summary>
/// The type of a single variable reference.
/// </summary>
enum VariableRefType
{
    /// <summary>
    /// Represents a reference to a function argument.
    /// </summary>
    Argument,

    /// <summary>
    /// Represents a reference to a local variable.
    /// </summary>
    Local,

    /// <summary>
    /// Represents a reference to a stack slot.
    /// </summary>
    Stack,

    /// <summary>
    /// Represents an abstract memory monad.
    /// </summary>
    Memory,
}

/// <summary>
/// Represents a single variable.
/// </summary>
/// <param name="Index">Index of the variable.</param>
/// <param name="RefType">Type of this variable reference.</param>
readonly record struct VariableRef(int Index, VariableRefType RefType)
{
    /// <summary>
    /// Returns the string representation of this variable.
    /// </summary>
    /// <returns>The string representation of this variable.</returns>
    public override string ToString() => $"{Index} [{RefType}]";
}
