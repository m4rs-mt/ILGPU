// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2020-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: HistogramExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.HistogramOperations;
using ILGPU.Algorithms.Resources;
using ILGPU.Runtime;
using System;
using System.Reflection;

namespace ILGPU.Algorithms
{
    #region Delegates

    /// <summary>
    /// Represents a histogram operation on the given view.
    /// </summary>
    /// <typeparam name="T">The input view element type.</typeparam>
    /// <typeparam name="TStride">The input view stride.</typeparam>
    /// <typeparam name="TBinType">The histogram bin type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The input view.</param>
    /// <param name="histogram">The histogram view to update.</param>
    /// <param name="histogramOverflow">
    /// Single-element view that indicates whether the histogram has overflowed.
    /// </param>
    public delegate void Histogram<T, TStride, TBinType>(
        AcceleratorStream stream,
        ArrayView1D<T, TStride> view,
        ArrayView<TBinType> histogram,
        ArrayView<int> histogramOverflow)
        where T : unmanaged
        where TBinType : unmanaged
        where TStride : unmanaged, IStride1D;

    /// <summary>
    /// Represents a histogram operation on the given view.
    /// </summary>
    /// <typeparam name="T">The input view element type.</typeparam>
    /// <typeparam name="TStride">The input view stride.</typeparam>
    /// <typeparam name="TBinType">The histogram bin type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The input view.</param>
    /// <param name="histogram">The histogram view to update.</param>
    public delegate void HistogramUnchecked<T, TStride, TBinType>(
        AcceleratorStream stream,
        ArrayView1D<T, TStride> view,
        ArrayView<TBinType> histogram)
        where T : unmanaged
        where TStride : unmanaged, IStride1D
        where TBinType : unmanaged;

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
        /// <typeparam name="TStride">The input view stride.</typeparam>
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
            TStride,
            TBinType,
            TIncrementor,
            TLocator>(
            AcceleratorStream stream,
            KernelConfig config,
            ArrayView1D<T, TStride> view,
            ArrayView<TBinType> histogram,
            ArrayView<int> histogramOverflow,
            int paddedLength)
            where T : unmanaged
            where TStride : unmanaged, IStride1D
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>;

        /// <summary>
        /// The delegate for the computing the histogram.
        /// </summary>
        /// <typeparam name="T">The input view element type.</typeparam>
        /// <typeparam name="TStride">The input view stride.</typeparam>
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
            TStride,
            TBinType,
            TIncrementor,
            TLocator>(
            AcceleratorStream stream,
            KernelConfig config,
            ArrayView1D<T, TStride> view,
            ArrayView<TBinType> histogram,
            int paddedLength)
            where T : unmanaged
            where TStride : unmanaged, IStride1D
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
        /// <typeparam name="TStride">The input view stride.</typeparam>
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
            TStride,
            TBinType,
            TIncrementor,
            TLocator>(
            ArrayView1D<T, TStride> view,
            ArrayView<TBinType> histogram,
            ArrayView<int> histogramOverflow,
            int paddedLength)
            where T : unmanaged
            where TStride : unmanaged, IStride1D
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            HistogramWorkKernel<T, TStride, TBinType, TIncrementor, TLocator>(
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
        /// <typeparam name="TStride">The input view stride.</typeparam>
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
            TStride,
            TBinType,
            TIncrementor,
            TLocator>(
            ArrayView1D<T, TStride> view,
            ArrayView<TBinType> histogram,
            int paddedLength)
            where T : unmanaged
            where TStride : unmanaged, IStride1D
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            HistogramWorkKernel<T, TStride, TBinType, TIncrementor, TLocator>(
                view,
                histogram,
                out _,
                paddedLength);
        }

        internal static void HistogramWorkKernel<
            T,
            TStride,
            TBinType,
            TIncrementor,
            TLocator>(
            ArrayView1D<T, TStride> view,
            ArrayView<TBinType> histogram,
            out bool histogramOverflow,
            int paddedLength)
            where T : unmanaged
            where TStride : unmanaged, IStride1D
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
                if (i < view.IntExtent)
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
        /// <typeparam name="TStride">The input view stride.</typeparam>
        /// <typeparam name="TBinType">The histogram bin type.</typeparam>
        /// <typeparam name="TIncrementor">
        /// The operation to increment the value of the bin.
        /// </typeparam>
        /// <typeparam name="TLocator">
        /// The operation to compute the bin location.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created histogram handler.</returns>
        public static Histogram<T, TStride, TBinType> CreateHistogram<
            T,
            TStride,
            TBinType,
            TIncrementor,
            TLocator>(
            this Accelerator accelerator)
            where T : unmanaged
            where TStride : unmanaged, IStride1D
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            var kernel = accelerator.LoadKernel<
                HistogramDelegate<
                    T,
                    TStride,
                    TBinType,
                    TIncrementor,
                    TLocator>>(
                    HistogramKernelMethod.MakeGenericMethod(
                        typeof(T),
                        typeof(TStride),
                        typeof(TBinType),
                        typeof(TIncrementor),
                        typeof(TLocator)));

            return (stream, view, histogram, histogramOverflow) =>
            {
                if (!view.IsValid)
                    throw new ArgumentNullException(nameof(view));
                if (view.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(view));
                if (!histogram.IsValid)
                    throw new ArgumentNullException(nameof(histogram));
                if (histogram.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(histogram));
                if (!histogramOverflow.IsValid)
                    throw new ArgumentNullException(nameof(histogramOverflow));
                if (histogramOverflow.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(histogramOverflow));
                if (view.Length > int.MaxValue || histogram.Length > int.MaxValue)
                {
                    throw new NotSupportedException(
                        ErrorMessages.NotSupportedArrayView64);
                }
                int numElements = view.IntExtent;
                var (gridDim, groupDim) = accelerator.ComputeGridStrideLoopExtent(
                       numElements,
                       out int numIterationsPerGroup);
                int lengthInformation =
                    XMath.DivRoundUp(numElements, groupDim) * groupDim;

                kernel(
                    stream,
                    (gridDim, groupDim),
                    view,
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
        /// <typeparam name="TStride">The input view stride.</typeparam>
        /// <typeparam name="TBinType">The histogram bin type.</typeparam>
        /// <typeparam name="TIncrementor">
        /// The operation to increment the value of the bin.
        /// </typeparam>
        /// <typeparam name="TLocator">
        /// The operation to compute the bin location.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created histogram handler.</returns>
        public static HistogramUnchecked<T, TStride, TBinType> CreateHistogramUnchecked<
            T,
            TStride,
            TBinType,
            TIncrementor,
            TLocator>(
            this Accelerator accelerator)
            where T : unmanaged
            where TStride : unmanaged, IStride1D
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            var kernel = accelerator.LoadKernel<
                HistogramUncheckedDelegate<
                    T,
                    TStride,
                    TBinType,
                    TIncrementor,
                    TLocator>>(
                    HistogramUncheckedKernelMethod.MakeGenericMethod(
                        typeof(T),
                        typeof(TStride),
                        typeof(TBinType),
                        typeof(TIncrementor),
                        typeof(TLocator)));

            return (stream, view, histogram) =>
            {
                if (!view.IsValid)
                    throw new ArgumentNullException(nameof(view));
                if (view.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(view));
                if (!histogram.IsValid)
                    throw new ArgumentNullException(nameof(histogram));
                if (histogram.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(histogram));
                if (view.Length > int.MaxValue || histogram.Length > int.MaxValue)
                {
                    throw new NotSupportedException(
                        ErrorMessages.NotSupportedArrayView64);
                }
                int numElements = view.IntExtent;
                var (gridDim, groupDim) = accelerator.ComputeGridStrideLoopExtent(
                       numElements,
                       out int numIterationsPerGroup);
                int lengthInformation =
                    XMath.DivRoundUp(numElements, groupDim) * groupDim;

                kernel(
                    stream,
                    (gridDim, groupDim),
                    view,
                    histogram,
                    lengthInformation);
            };
        }

        /// <summary>
        /// Calculates the histogram on the given view.
        /// </summary>
        /// <typeparam name="T">The input view element type.</typeparam>
        /// <typeparam name="TStride">The input view stride.</typeparam>
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
        public static void Histogram<T, TStride, TBinType, TIncrementor, TLocator>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView1D<T, TStride> view,
            ArrayView<TBinType> histogram,
            ArrayView<int> histogramOverflow)
            where T : unmanaged
            where TStride : unmanaged, IStride1D
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            accelerator.CreateHistogram<T, TStride, TBinType, TIncrementor, TLocator>()(
                stream,
                view,
                histogram,
                histogramOverflow);
        }

        /// <summary>
        /// Calculates the histogram on the given view (without overflow checking).
        /// </summary>
        /// <typeparam name="T">The input view element type.</typeparam>
        /// <typeparam name="TStride">The input view stride.</typeparam>
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
            TStride,
            TBinType,
            TIncrementor,
            TLocator>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView1D<T, TStride> view,
            ArrayView<TBinType> histogram)
            where T : unmanaged
            where TStride : unmanaged, IStride1D
            where TBinType : unmanaged
            where TIncrementor : struct, IIncrementOperation<TBinType>
            where TLocator : struct, IComputeMultiBinOperation<T, TBinType, TIncrementor>
        {
            accelerator.CreateHistogramUnchecked<
                T,
                TStride,
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
