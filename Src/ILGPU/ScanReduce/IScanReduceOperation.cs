// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IScanReduceOperation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.ScanReduce;

/// <summary>
/// Implements a scan or a reduction operation.
/// </summary>
/// <typeparam name="T">The underlying type of the scan operation.</typeparam>
public interface IScanReduceOperation<T> where T : struct
{
    /// <summary>
    /// Returns the identity value (the neutral element of the operation), such that
    /// Apply(Apply(Identity, left), right) == Apply(left, right).
    /// </summary>
    static abstract T Identity { get; }

    /// <summary>
    /// Applies the current operation.
    /// </summary>
    /// <param name="first">The first operand.</param>
    /// <param name="second">The second operand.</param>
    /// <returns>The result of the operation.</returns>
    static abstract T Apply(T first, T second);

    /// <summary>
    /// Performs an atomic operation of the form target =
    /// AtomicUpdate(target.Value, value).
    /// </summary>
    /// <param name="target">The target address to update.</param>
    /// <param name="value">The value.</param>
    static abstract void AtomicApply(ref T target, T value);
}

/// <summary>
/// Holds the left and the right boundary of a scan operation.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// Constructs a new scan-boundaries instance.
/// </remarks>
/// <param name="LeftBoundary">The left boundary.</param>
/// <param name="RightBoundary">The right boundary.</param>
public readonly record struct ScanBoundaries<T>(T LeftBoundary, T RightBoundary)
    where T : struct
{
    /// <summary>
    /// Returns the string representation of these boundary values.
    /// </summary>
    /// <returns>The string representation of these boundary values.</returns>
    public override string ToString() => $"[{LeftBoundary}, {RightBoundary}]";
}

/// <summary>
/// Represents the scan operation type.
/// </summary>
public enum ScanKind
{
    /// <summary>
    /// An inclusive scan operation.
    /// </summary>
    Inclusive,

    /// <summary>
    /// An exclusive scan operation.
    /// </summary>
    Exclusive
}

/// <summary>
/// Represents an interface to parameterize scan operations.
/// </summary>
public interface IScanPredicate
{
    /// <summary>
    /// Returns the scan kind of the current operation.
    /// </summary>
    static abstract ScanKind ScanKind { get; }
}

/// <summary>
/// Contains pre-defined scan predicates.
/// </summary>
public static class ScanPredicates
{
    /// <summary>
    /// Represents an inclusive scan predicate.
    /// </summary>
    public readonly struct InclusiveScan : IScanPredicate
    {
        /// <summary>
        /// Returns <see cref="ScanKind.Inclusive"/>.
        /// </summary>
        public static ScanKind ScanKind => ScanKind.Inclusive;
    }

    /// <summary>
    /// Represents an exclusive scan predicate.
    /// </summary>
    public readonly struct ExclusiveScan : IScanPredicate
    {
        /// <summary>
        /// Returns <see cref="ScanKind.Exclusive"/>.
        /// </summary>
        public static ScanKind ScanKind => ScanKind.Exclusive;
    }
}
