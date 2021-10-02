// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: IHistogramOperations.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Algorithms.HistogramOperations
{
    /// <summary>
    /// Computes the histogram bin for a single input value.
    /// </summary>
    /// <typeparam name="T">The underlying type of the histogram operation.</typeparam>
    /// <typeparam name="TIndex">The index type.</typeparam>
    public interface IComputeSingleBinOperation<T, TIndex>
        where T : unmanaged
        where TIndex : struct, IGenericIndex<TIndex>
    {
        /// <summary>
        /// Calculates the histogram bin location for the given value.
        /// </summary>
        /// <param name="value">The value to map.</param>
        /// <param name="numBins">The total  number of bins.</param>
        /// <returns>The bin location</returns>
        TIndex ComputeHistogramBin(T value, TIndex numBins);
    }

    /// <summary>
    /// Computes and updates multiple histogram bins for a single input value.
    /// </summary>
    /// <typeparam name="T">The underlying type of the histogram operation.</typeparam>
    /// <typeparam name="TBinType">The underlying type of the histogram bins.</typeparam>
    /// <typeparam name="TIncrementor">
    /// The operation to increment the value of the bin.
    /// </typeparam>
    public interface IComputeMultiBinOperation<T, TBinType, TIncrementor>
        where T : unmanaged
        where TBinType : unmanaged
        where TIncrementor : struct, IIncrementOperation<TBinType>
    {
        /// <summary>
        /// Calculates and updates multiple histogram bin locations for the given value.
        /// </summary>
        /// <param name="value">The value to map.</param>
        /// <param name="histogram">The histogram to update.</param>
        /// <param name="incrementOperation">
        /// The operation used to increment the histogram.
        /// </param>
        /// <param name="incrementOverflow">
        /// Indicates when the increment has overflowed.
        /// </param>
        void ComputeHistogramBins(
            T value,
            ArrayView<TBinType> histogram,
            in TIncrementor incrementOperation,
            out bool incrementOverflow);
    }

    /// <summary>
    /// Increments the value in a histogram bin.
    /// </summary>
    /// <typeparam name="TBinType">The underlying type of the histogram bin.</typeparam>
    public interface IIncrementOperation<TBinType>
        where TBinType : unmanaged
    {
        /// <summary>
        /// Increments the histogram bin.
        /// </summary>
        /// <param name="target">The bin value to update.</param>
        /// <param name="incrementOverflow">
        /// Indicates when the increment has overflowed.
        /// </param>
        void Increment(ref TBinType target, out bool incrementOverflow);
    }
}
