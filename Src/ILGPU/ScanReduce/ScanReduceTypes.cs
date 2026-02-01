// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ScanReduceTypes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.ScanReduce;

/// <summary>
/// Represents an atomic binary operation applied to a reference target.
/// Used by the lambda-based reduction API.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="target">The target reference to update atomically.</param>
/// <param name="value">The value to apply.</param>
public delegate void AtomicApplyAction<T>(ref T target, T value);

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
