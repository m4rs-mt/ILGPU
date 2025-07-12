// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaInstructionSet.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ILGPU.Runtime.Cuda;

/// <summary>
/// Represents a Cuda (PTX) ISA (Instruction Set Architecture).
/// </summary>
/// <param name="major">The major version.</param>
/// <param name="minor">The minor version.</param>
[DebuggerDisplay("ISA {Major}.{Minor}")]
public readonly partial struct CudaInstructionSet(int major, int minor) :
    IEquatable<CudaInstructionSet>,
    IComparable<CudaInstructionSet>
{
    #region Static

    private static readonly Regex _parsingRegex = ISARegex();

    [GeneratedRegex(@"^(\d+)\.(\d+)$")]
    private static partial Regex ISARegex();

    /// <summary>
    /// Tries to parse a Cuda ISA from the given string expression.
    /// </summary>
    /// <param name="expression">The string expression.</param>
    /// <param name="isa">The Cuda ISA (if any).</param>
    /// <returns>True if an architecture could be parsed from the expression.</returns>
    public static bool TryParse(string expression, out CudaInstructionSet isa)
    {
        var match = _parsingRegex.Match(expression);
        if (!match.Success)
        {
            isa = default;
            return false;
        }

        int firstDigit = int.Parse(match.Groups[1].Value);
        int secondDigit = int.Parse(match.Groups[2].Value);
        isa = new(firstDigit, secondDigit);
        return true;
    }

    #endregion

    #region IEquatable

    /// <summary>
    /// Returns true if the given instruction set is equal to this instruction set.
    /// </summary>
    /// <param name="other">The other instruction set.</param>
    /// <returns>
    /// True, if the given instruction set is equal to this instruction set.
    /// </returns>
    public bool Equals(CudaInstructionSet other) => this == other;

    #endregion

    #region IComparable

    /// <summary>
    /// Compares this instruction set to the given one.
    /// </summary>
    /// <param name="other">The object to compare to.</param>
    /// <returns>The comparison result.</returns>
    public int CompareTo(CudaInstructionSet other)
    {
        if (this < other)
            return -1;
        else if (this > other)
            return 1;
        Debug.Assert(this == other);
        return 0;
    }

    #endregion

    #region Properties

    /// <summary>
    /// The major version
    /// </summary>
    public int Major { get; } = major;

    /// <summary>
    /// The minor version
    /// </summary>
    public int Minor { get; } = minor;

    #endregion

    #region Object

    /// <summary>
    /// Returns true if the given object is equal to this instruction set.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns>True,
    /// if the given object is equal to this instruction set.</returns>
    public override bool Equals(object? obj) =>
        obj is CudaInstructionSet instructionSet && instructionSet == this;

    /// <summary>
    /// Returns the hash code of this instruction set.
    /// </summary>
    /// <returns>The hash code of this instruction set.</returns>
    public override int GetHashCode() => Major.GetHashCode() ^ Minor.GetHashCode();

    /// <summary>
    /// Returns the string representation of the instruction set.
    /// </summary>
    /// <returns>The string representation of the instruction set.</returns>
    public override string ToString() => $"{Major}.{Minor}";

    #endregion

    #region Operators

    /// <summary>
    /// Returns true if the first and the second instruction set are the same.
    /// </summary>
    /// <param name="first">The first instruction set.</param>
    /// <param name="second">The second instruction set.</param>
    /// <returns>
    /// True, if the first and the second instruction set are the same.
    /// </returns>
    public static bool operator ==(
        CudaInstructionSet first,
        CudaInstructionSet second) =>
        first.Major == second.Major && first.Minor == second.Minor;

    /// <summary>
    /// Returns true if the first and the second instruction set are not the same.
    /// </summary>
    /// <param name="first">The first instruction set.</param>
    /// <param name="second">The second instruction set.</param>
    /// <returns>
    /// True, if the first and the second instruction set are not the same.
    /// </returns>
    public static bool operator !=(
        CudaInstructionSet first,
        CudaInstructionSet second) =>
        first.Major != second.Major || first.Minor != second.Minor;

    /// <summary>
    /// Returns true if the first instruction set is smaller than the second one.
    /// </summary>
    /// <param name="first">The first instruction set.</param>
    /// <param name="second">The second instruction set.</param>
    /// <returns>
    /// True, if the first instruction set is smaller than the second one.
    /// </returns>
    public static bool operator <(
        CudaInstructionSet first,
        CudaInstructionSet second) =>
        first.Major < second.Major ||
        first.Major == second.Major && first.Minor < second.Minor;

    /// <summary>
    /// Returns true if the first instruction set is less than or equal to the
    /// second instruction set.
    /// </summary>
    /// <param name="first">The first instruction set.</param>
    /// <param name="second">The second instruction set.</param>
    /// <returns>
    /// True, if the first instruction set is less or equal to the second instruction
    /// set.
    /// </returns>
    public static bool operator <=(
        CudaInstructionSet first,
        CudaInstructionSet second) =>
        first.Major < second.Major ||
        first.Major == second.Major && first.Minor <= second.Minor;

    /// <summary>
    /// Returns true if the first instruction set is greater than the second one.
    /// </summary>
    /// <param name="first">The first instruction set.</param>
    /// <param name="second">The second instruction set.</param>
    /// <returns>
    /// True, if the first instruction set is greater than the second one.
    /// </returns>
    public static bool operator >(
        CudaInstructionSet first,
        CudaInstructionSet second) =>
        first.Major > second.Major ||
        first.Major == second.Major && first.Minor > second.Minor;

    /// <summary>
    /// Returns true if the first instruction set is greater than or equal to the
    /// second instruction set.
    /// </summary>
    /// <param name="first">The first instruction set.</param>
    /// <param name="second">The second instruction set.</param>
    /// <returns>
    /// True, if the first instruction set is greater or equal to the second
    /// instruction set.
    /// </returns>
    public static bool operator >=(
        CudaInstructionSet first,
        CudaInstructionSet second) =>
        first.Major > second.Major ||
        first.Major == second.Major && first.Minor >= second.Minor;

    #endregion
}
