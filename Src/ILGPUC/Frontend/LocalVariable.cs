// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: LocalVariable.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.Frontend.DebugInformation;

/// <summary>
/// Represents a local variable in a scope.
/// </summary>
/// <param name="Index">The variable index.</param>
/// <param name="Name">The variable name.</param>
readonly record struct LocalVariable(int Index, string Name)
{
    /// <summary>
    /// Returns the string representation of this local variable.
    /// </summary>
    /// <returns>The string representation of this local variable.</returns>
    public override string ToString() => $"{Index}: {Name}";
}
