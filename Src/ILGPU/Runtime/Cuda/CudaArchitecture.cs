// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaArchitecture.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ILGPU.Runtime.Cuda;

/// <summary>
/// Represents a Cuda architecture.
/// </summary>
/// <param name="major">The major version.</param>
/// <param name="minor">The minor version.</param>
public readonly partial struct CudaArchitecture(int major, int minor) :
    IEquatable<CudaArchitecture>,
    IComparable<CudaArchitecture>
{
    #region Static

    /// <summary>
    /// Returns the current Cuda architecture the current kernel is being compiled for.
    /// </summary>
    /// <remarks>
    /// Note that this property is only supported inside kernels.
    /// </remarks>
    public static CudaArchitecture Current
    {
        [BackendIntrinsic]
        get => throw new InvalidKernelOperationException();
    }

    private static readonly Regex _parsingRegex = ArchitectureRegex();

    [GeneratedRegex(@"^SM_(\d)(\d)$")]
    private static partial Regex ArchitectureRegex();

    /// <summary>
    /// Tries to parse a Cuda architecture from the given string expression.
    /// </summary>
    /// <param name="expression">The string expression.</param>
    /// <param name="architecture">The Cuda architecture (if any).</param>
    /// <returns>True if an architecture could be parsed from the expression.</returns>
    public static bool TryParse(string expression, out CudaArchitecture architecture)
    {
        var match = _parsingRegex.Match(expression);
        if (!match.Success)
        {
            architecture = default;
            return false;
        }

        int firstDigit = int.Parse(match.Groups[1].Value);
        int secondDigit = int.Parse(match.Groups[2].Value);
        architecture = new(firstDigit, secondDigit);
        return true;
    }

    #endregion

    #region IEquatable

    /// <summary>
    /// Returns true if the given architecture is equal to this architecture.
    /// </summary>
    /// <param name="other">The other architecture.</param>
    /// <returns>
    /// True, if the given architecture is equal to this architecture.
    /// </returns>
    public bool Equals(CudaArchitecture other) => this == other;

    #endregion

    #region IComparable

    /// <summary>
    /// Compares this architecture to the given one.
    /// </summary>
    /// <param name="other">The object to compare to.</param>
    /// <returns>The comparison result.</returns>
    public int CompareTo(CudaArchitecture other)
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
    /// The major version.
    /// </summary>
    public int Major { get; } = major;

    /// <summary>
    /// The minor version.
    /// </summary>
    public int Minor { get; } = minor;

    #endregion

    #region Object

    /// <summary>
    /// Returns true if the given object is equal to this architecture.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns>True,
    /// if the given object is equal to this architecture.</returns>
    public override bool Equals(object? obj) =>
        obj is CudaArchitecture architecture && architecture == this;

    /// <summary>
    /// Returns the hash code of this architecture.
    /// </summary>
    /// <returns>The hash code of this architecture.</returns>
    public override int GetHashCode() => Major.GetHashCode() ^ Minor.GetHashCode();

    /// <summary>
    /// Returns the string representation of the architecture.
    /// </summary>
    /// <returns>The string representation of the architecture.</returns>
    public override string ToString() => $"SM_{Major}{Minor}";


    #endregion

    #region Operators

    /// <summary>
    /// Returns true if the first and the second architecture are the same.
    /// </summary>
    /// <param name="first">The first architecture.</param>
    /// <param name="second">The second architecture.</param>
    /// <returns>
    /// True, if the first and the second architecture are the same.
    /// </returns>
    public static bool operator ==(
        CudaArchitecture first,
        CudaArchitecture second) =>
        first.Major == second.Major && first.Minor == second.Minor;

    /// <summary>
    /// Returns true if the first and the second architecture are not the same.
    /// </summary>
    /// <param name="first">The first architecture.</param>
    /// <param name="second">The second architecture.</param>
    /// <returns>
    /// True, if the first and the second architecture are not the same.
    /// </returns>
    public static bool operator !=(
        CudaArchitecture first,
        CudaArchitecture second) =>
        first.Major != second.Major || first.Minor != second.Minor;

    /// <summary>
    /// Returns true if the first architecture is smaller than the second one.
    /// </summary>
    /// <param name="first">The first architecture.</param>
    /// <param name="second">The second architecture.</param>
    /// <returns>
    /// True, if the first architecture is smaller than the second one.
    /// </returns>
    public static bool operator <(
        CudaArchitecture first,
        CudaArchitecture second) =>
        first.Major < second.Major ||
        first.Major == second.Major && first.Minor < second.Minor;

    /// <summary>
    /// Returns true if the first architecture is less than or equal to the
    /// second architecture.
    /// </summary>
    /// <param name="first">The first architecture.</param>
    /// <param name="second">The second architecture.</param>
    /// <returns>
    /// True, if the first architecture is less or equal to the second architecture.
    /// </returns>
    public static bool operator <=(
        CudaArchitecture first,
        CudaArchitecture second) =>
        first.Major < second.Major ||
        first.Major == second.Major && first.Minor <= second.Minor;

    /// <summary>
    /// Returns true if the first architecture is greater than the second one.
    /// </summary>
    /// <param name="first">The first architecture.</param>
    /// <param name="second">The second architecture.</param>
    /// <returns>
    /// True, if the first architecture is greater than the second one.
    /// </returns>
    public static bool operator >(
        CudaArchitecture first,
        CudaArchitecture second) =>
        first.Major > second.Major ||
        first.Major == second.Major && first.Minor > second.Minor;

    /// <summary>
    /// Returns true if the first architecture is greater than or equal to the
    /// second architecture.
    /// </summary>
    /// <param name="first">The first architecture.</param>
    /// <param name="second">The second architecture.</param>
    /// <returns>
    /// True, if the first architecture is greater or equal to the second
    /// architecture.
    /// </returns>
    public static bool operator >=(
        CudaArchitecture first,
        CudaArchitecture second) =>
        first.Major > second.Major ||
        first.Major == second.Major && first.Minor >= second.Minor;

    #endregion
}
