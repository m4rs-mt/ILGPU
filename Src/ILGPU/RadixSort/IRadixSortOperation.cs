// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRadixSortOperation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.RadixSort;

/// <summary>
/// Implements a radix sort operation.
/// </summary>
/// <typeparam name="T">The underlying type of the sort operation.</typeparam>
public interface IRadixSortOperation<T> where T : struct
{
    /// <summary>
    /// Returns the number of bits to sort.
    /// </summary>
    static abstract int NumBits { get; }

    /// <summary>
    /// The default element value.
    /// </summary>
    static abstract T DefaultValue { get; }

    /// <summary>
    /// Converts the given value to a radix-sort compatible value.
    /// </summary>
    /// <param name="value">The value to map.</param>
    /// <param name="shift">The shift amount in bits.</param>
    /// <param name="bitMask">The lower bit mask bit use.</param>
    static abstract int ExtractRadixBits(T value, int shift, int bitMask);
}

/// <summary>
/// Represents a single specialization.
/// </summary>
public interface IRadixSortSpecialization
{
    /// <summary>
    /// Returns the associated constant unroll factor.
    /// </summary>
    static abstract int UnrollFactor { get; }

    /// <summary>
    /// Returns the number of bits to increment for the
    /// next radix-sort iteration.
    /// </summary>
    static abstract int BitIncrement { get; }
}

/// <summary>
/// Provides pre-defined specializations for RadixSort algorithm.
/// </summary>
public static class RadixSortSpecializations
{
    /// <summary>
    /// A specialization with unroll factor 4.
    /// </summary>
    public readonly struct Specialization4 : IRadixSortSpecialization
    {
        /// <inheritdoc cref="IRadixSortSpecialization.UnrollFactor"/>
        public static int UnrollFactor => 4;

        /// <inheritdoc cref="IRadixSortSpecialization.BitIncrement"/>
        public static int BitIncrement => 2;
    }
}
