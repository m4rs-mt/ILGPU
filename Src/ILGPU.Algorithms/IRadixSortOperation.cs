// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRadixSortOperation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Algorithms.RadixSortOperations
{
    /// <summary>
    /// Implements a radix sort operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
    public interface IRadixSortOperation<T>
        where T : struct
    {
        /// <summary>
        /// Returns the number of bits to sort.
        /// </summary>
        int NumBits { get; }

        /// <summary>
        /// The default element value.
        /// </summary>
        T DefaultValue { get; }

        /// <summary>
        /// Converts the given value to a radix-sort compatible value.
        /// </summary>
        /// <param name="value">The value to map.</param>
        /// <param name="shift">The shift amount in bits.</param>
        /// <param name="bitMask">The lower bit mask bit use.</param>
        int ExtractRadixBits(T value, int shift, int bitMask);
    }
}
