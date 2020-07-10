// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2020 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: HistogramExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.HistogramOperations;
using ILGPU.Runtime;
using System.Reflection;

namespace ILGPU.Algorithms
{
    #region Delegates

    /// <summary>
    /// Represents a histogram operation on the given view.
    /// </summary>
    /// <typeparam name="T">The input view element type.</typeparam>
    /// <typeparam name="TIndex">The input view index type.</typeparam>
    /// <typeparam name="TBinType">The histogram bin type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The input view.</param>
    /// <param name="histogram">The histogram view to update.</param>
    /// <param name="histogramOverflow">
    /// Single-element view that indicates whether the histogram has overflowed.
    /// </param>
    public delegate void Histogram<T, TIndex, TBinType>(
        AcceleratorStream stream,
        ArrayView<T, TIndex> view,
        ArrayView<TBinType> histogram,
        ArrayView<int> histogramOverflow)
        where T : unmanaged
        where TBinType : unmanaged
        where TIndex : unmanaged, IIndex, IGenericIndex<TIndex>;

    /// <summary>
    /// Represents a histogram operation on the given view.
    /// </summary>
    /// <typeparam name="T">The input view element type.</typeparam>
    /// <typeparam name="TIndex">The input view index type.</typeparam>
    /// <typeparam name="TBinType">The histogram bin type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The input view.</param>
    /// <param name="histogram">The histogram view to update.</param>
    public delegate void HistogramUnchecked<T, TIndex, TBinType>(
        AcceleratorStream stream,
        ArrayView<T, TIndex> view,
        ArrayView<TBinType> histogram)
        where T : unmanaged
        where TBinType : unmanaged
        where TIndex : unmanaged, IIndex, IGenericIndex<TIndex>;

    #endregion

    /// <summary>
    /// Contains extension methods for histogram operations.
    /// </summary>
    public static partial class HistogramExtensions
    {
        #region Histogram Helpers

        /// <summary>
        /// The delegate for the computing the histogram.
        /// </summary>
        /// <typeparam name="T">The input view element type.</typeparam>
        /// <typeparam name="TBinType">The histogram bin type.</typeparam>
        /// <typeparam name="TIncrementor">
        /// The operation to increment the value of the bin.
        /// </typeparam>
        /// <typeparam name="TLocator">
        /// The operation to compute the bin location.
        /// </typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="config">The kernel configuration to use.</param>
        /// <param name="view">The input view.</param>
        /// <param name="histogram">The histogram view to update.</param>
        /// <param name="histogramOverflow">
        /// Single-element view that indicates whether the histogram has overflowed.
        /// </param>
        /// <param name="paddedLength">The padded length of the input view.</param>
        private delegate void HistogramDelegate<
            T,
            TBinType,
            TIncrementor,
            TLocator>(
            AcceleratorStream stream,
            KernelConfig config,
            ArrayView<T> view,
            ArrayView<TBinType> histogram,
            ArrayView<int> histogramOverflow,
            int paddedLength)
            where T : unmanaged
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>;

        /// <summary>
        /// The delegate for the computing the histogram.
        /// </summary>
        /// <typeparam name="T">The input view element type.</typeparam>
        /// <typeparam name="TBinType">The histogram bin type.</typeparam>
        /// <typeparam name="TIncrementor">
        /// The operation to increment the value of the bin.
        /// </typeparam>
        /// <typeparam name="TLocator">
        /// The operation to compute the bin location.
        /// </typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="config">The kernel configuration to use.</param>
        /// <param name="view">The input view.</param>
        /// <param name="histogram">The histogram view to update.</param>
        /// <param name="paddedLength">The padded length of the input view.</param>
        private delegate void HistogramUncheckedDelegate<
            T,
            TBinType,
            TIncrementor,
            TLocator>(
            AcceleratorStream stream,
            KernelConfig config,
            ArrayView<T> view,
            ArrayView<TBinType> histogram,
            int paddedLength)
            where T : unmanaged
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>;

        #endregion

        #region Histogram Implementation

        private static readonly MethodInfo HistogramKernelMethod =
            typeof(HistogramExtensions).GetMethod(
                nameof(HistogramKernel),
                BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo HistogramUncheckedKernelMethod =
            typeof(HistogramExtensions).GetMethod(
                nameof(HistogramUncheckedKernel),
                BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// The actual histogram kernel implementation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TBinType">The histogram bin type.</typeparam>
        /// <typeparam name="TIncrementor">
        /// The operation to increment the value of the bin.
        /// </typeparam>
        /// <typeparam name="TLocator">
        /// The operation to compute the bin location.
        /// </typeparam>
        /// <param name="view">The input view.</param>
        /// <param name="histogram">The histogram view to update.</param>
        /// <param name="histogramOverflow">
        /// Single-element view that indicates 4 the histogram has overflowed.
        /// </param>
        /// <param name="paddedLength">The padded length of the input view.</param>
        internal static void HistogramKernel<
            T,
            TBinType,
            TIncrementor,
            TLocator>(
            ArrayView<T> view,
            ArrayView<TBinType> histogram,
            ArrayView<int> histogramOverflow,
            int paddedLength)
            where T : unmanaged
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            HistogramWorkKernel<T, TBinType, TIncrementor, TLocator>(
                view,
                histogram,
                out var histogramDidOverflow,
                paddedLength);
            Atomic.Or(ref histogramOverflow[0], histogramDidOverflow ? 1 : 0);
        }

        /// <summary>
        /// The actual histogram kernel implementation (without overflow checking).
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TBinType">The histogram bin type.</typeparam>
        /// <typeparam name="TIncrementor">
        /// The operation to increment the value of the bin.
        /// </typeparam>
        /// <typeparam name="TLocator">
        /// The operation to compute the bin location.
        /// </typeparam>
        /// <param name="view">The input view.</param>
        /// <param name="histogram">The histogram view to update.</param>
        /// <param name="paddedLength">The padded length of the input view.</param>
        internal static void HistogramUncheckedKernel<
            T,
            TBinType,
            TIncrementor,
            TLocator>(
            ArrayView<T> view,
            ArrayView<TBinType> histogram,
            int paddedLength)
            where T : unmanaged
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            HistogramWorkKernel<T, TBinType, TIncrementor, TLocator>(
                view,
                histogram,
                out _,
                paddedLength);
        }

        internal static void HistogramWorkKernel<T, TBinType, TIncrementor, TLocator>(
            ArrayView<T> view,
            ArrayView<TBinType> histogram,
            out bool histogramOverflow,
            int paddedLength)
            where T : unmanaged
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            TLocator operation = default;
            TIncrementor incrementOperation = default;

            var gridIdx = Grid.IdxX;
            var histogramDidOverflow = false;
            for (
                int i = Grid.GlobalIndex.X;
                i < paddedLength;
                i += GridExtensions.GridStrideLoopStride)
            {
                if (i < view.Length)
                {
                    operation.ComputeHistogramBins(
                        view[i],
                        histogram,
                        incrementOperation,
                        out var incrementDidOverflow);
                    histogramDidOverflow |= incrementDidOverflow;
                }

                gridIdx += Grid.DimX;
            }

            histogramOverflow = histogramDidOverflow;
        }

        /// <summary>
        /// Creates a kernel to calculate the histogram on a supplied view.
        /// </summary>
        /// <typeparam name="T">The input view element type.</typeparam>
        /// <typeparam name="TIndex">The input view index type.</typeparam>
        /// <typeparam name="TBinType">The histogram bin type.</typeparam>
        /// <typeparam name="TIncrementor">
        /// The operation to increment the value of the bin.
        /// </typeparam>
        /// <typeparam name="TLocator">
        /// The operation to compute the bin location.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created histogram handler.</returns>
        public static Histogram<T, TIndex, TBinType> CreateHistogram<
            T,
            TIndex,
            TBinType,
            TIncrementor,
            TLocator>(
            this Accelerator accelerator)
            where T : unmanaged
            where TIndex : unmanaged, IIndex, IGenericIndex<TIndex>
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            var kernel = accelerator.LoadKernel<
                HistogramDelegate<
                    T,
                    TBinType,
                    TIncrementor,
                    TLocator>>(
                    HistogramKernelMethod.MakeGenericMethod(
                        typeof(T),
                        typeof(TBinType),
                        typeof(TIncrementor),
                        typeof(TLocator)));

            return (stream, view, histogram, histogramOverflow) =>
            {
                var input = view.AsLinearView();
                var (gridDim, groupDim) = accelerator.ComputeGridStrideLoopExtent(
                       input.Length,
                       out int numIterationsPerGroup);
                int numVirtualGroups = gridDim * numIterationsPerGroup;
                int lengthInformation =
                    XMath.DivRoundUp(input.Length, groupDim) * groupDim;

                kernel(
                    stream,
                    (gridDim, groupDim),
                    input,
                    histogram,
                    histogramOverflow,
                    lengthInformation);
            };
        }

        /// <summary>
        /// Creates a kernel to calculate the histogram on a supplied view
        /// (without overflow checking).
        /// </summary>
        /// <typeparam name="T">The input view element type.</typeparam>
        /// <typeparam name="TIndex">The input view index type.</typeparam>
        /// <typeparam name="TBinType">The histogram bin type.</typeparam>
        /// <typeparam name="TIncrementor">
        /// The operation to increment the value of the bin.
        /// </typeparam>
        /// <typeparam name="TLocator">
        /// The operation to compute the bin location.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created histogram handler.</returns>
        public static HistogramUnchecked<T, TIndex, TBinType> CreateHistogramUnchecked<
            T,
            TIndex,
            TBinType,
            TIncrementor,
            TLocator>(
            this Accelerator accelerator)
            where T : unmanaged
            where TIndex : unmanaged, IIndex, IGenericIndex<TIndex>
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            var kernel = accelerator.LoadKernel<
                HistogramUncheckedDelegate<
                    T,
                    TBinType,
                    TIncrementor,
                    TLocator>>(
                    HistogramUncheckedKernelMethod.MakeGenericMethod(
                        typeof(T),
                        typeof(TBinType),
                        typeof(TIncrementor),
                        typeof(TLocator)));

            return (stream, view, histogram) =>
            {
                var input = view.AsLinearView();
                var (gridDim, groupDim) = accelerator.ComputeGridStrideLoopExtent(
                       input.Length,
                       out int numIterationsPerGroup);
                int numVirtualGroups = gridDim * numIterationsPerGroup;
                int lengthInformation =
                    XMath.DivRoundUp(input.Length, groupDim) * groupDim;

                kernel(
                    stream,
                    (gridDim, groupDim),
                    input,
                    histogram,
                    lengthInformation);
            };
        }

        /// <summary>
        /// Calculates the histogram on the given view.
        /// </summary>
        /// <typeparam name="T">The input view element type.</typeparam>
        /// <typeparam name="TIndex">The input view index type.</typeparam>
        /// <typeparam name="TBinType">The histogram bin type.</typeparam>
        /// <typeparam name="TIncrementor">
        /// The operation to increment the value of the bin.
        /// </typeparam>
        /// <typeparam name="TLocator">
        /// The operation to compute the bin location.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The input view.</param>
        /// <param name="histogram">The histogram view to update.</param>
        /// <param name="histogramOverflow">
        /// Single-element view that indicates whether the histogram has overflowed.
        /// </param>
        public static void Histogram<T, TIndex, TBinType, TIncrementor, TLocator>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T, TIndex> view,
            ArrayView<TBinType> histogram,
            ArrayView<int> histogramOverflow)
            where T : unmanaged
            where TIndex : unmanaged, IIndex, IGenericIndex<TIndex>
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            accelerator.CreateHistogram<T, TIndex, TBinType, TIncrementor, TLocator>()(
                stream,
                view,
                histogram,
                histogramOverflow);
        }

        /// <summary>
        /// Calculates the histogram on the given view (without overflow checking).
        /// </summary>
        /// <typeparam name="T">The input view element type.</typeparam>
        /// <typeparam name="TIndex">The input view index type.</typeparam>
        /// <typeparam name="TBinType">The histogram bin type.</typeparam>
        /// <typeparam name="TIncrementor">
        /// The operation to increment the value of the bin.
        /// </typeparam>
        /// <typeparam name="TLocator">
        /// The operation to compute the bin location.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="view">The input view.</param>
        /// <param name="histogram">The histogram view to update.</param>
        public static void HistogramUnchecked<
            T,
            TIndex,
            TBinType,
            TIncrementor,
            TLocator>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T, TIndex> view,
            ArrayView<TBinType> histogram)
            where T : unmanaged
            where TIndex : unmanaged, IIndex, IGenericIndex<TIndex>
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            accelerator.CreateHistogramUnchecked<
                T,
                TIndex,
                TBinType,
                TIncrementor,
                TLocator>()(
                    stream,
                    view,
                    histogram);
        }

        #endregion
    }
}
