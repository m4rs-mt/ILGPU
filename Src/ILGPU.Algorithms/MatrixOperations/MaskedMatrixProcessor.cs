// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: MaskedMatrixProcessor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections.Generic;

namespace ILGPU.Algorithms.MatrixOperations
{
    /// <summary>
    /// A processor for masked matrices to efficiently operate on multiple matrix
    /// instances in parallel to maximize occupancy.
    /// </summary>
    public class MaskedMatrixProcessor<T, TPredicate, TStride, TProcessor>
        : ConcurrentStreamProcessor
        where T : unmanaged
        where TStride : struct, IStride2D
        where TPredicate : struct, InlineList.IPredicate<Index2D>
        where TProcessor : struct, IMaskedSparseMatrixProcessor<T>
    {
        #region Instance

        /// <summary>
        /// The internal masked matrix multiplier which contains pre-compiled kernels.
        /// </summary>
        private readonly MaskedSparseMatrixMultiplier<T, TPredicate, TStride>
            matrixMultiplier;

        /// <summary>
        /// Constructs a new masked processor.
        /// </summary>
        /// <param name="accelerator">The parent accelerator.</param>
        /// <param name="maxNumConcurrentStreams">
        /// The maximum number of concurrent streams to use (if any).
        /// </param>
        /// <param name="streamProvider">
        /// A custom stream provider function to construct specialized streams.
        /// </param>
        public MaskedMatrixProcessor(
            Accelerator accelerator,
            int maxNumConcurrentStreams = 0,
            Func<Accelerator, AcceleratorStream>? streamProvider = null)
            : base(accelerator, maxNumConcurrentStreams, streamProvider)
        {
            matrixMultiplier = accelerator.CreateSparseTransposedMatrixMultiplierMasked<
                T,
                TPredicate,
                TStride,
                TProcessor>();
        }

        #endregion

        /// <summary>
        /// Returns the current predicate to use (if any).
        /// </summary>
        public TPredicate? Predicate { get; set; }

        #region Methods

        /// <summary>
        /// Multiplies the given matrices using the currently assigned predicate while
        /// transposing the matrix given by <paramref name="bView"/>.
        /// </summary>
        /// <param name="stream">The current accelerator stream to use.</param>
        /// <param name="aView">The dense input matrix a of shape MxK.</param>
        /// <param name="bView">The sparse matrix b of shape NxK (will transpose).</param>
        /// <param name="outView">A dense output matrix of shape of MxN.</param>
        public void MultiplyTransposed(
            AcceleratorStream stream,
            ArrayView2D<T, TStride> aView,
            SparseMatrixView<T, TStride> bView,
            ArrayView2D<T, TStride> outView)
        {
            if (!Predicate.HasValue)
                throw new InvalidOperationException();
            matrixMultiplier(stream, Predicate.Value, aView, bView, outView);
        }

        /// <summary>
        /// Multiplies the given matrices using the currently assigned predicate while
        /// transposing the matrices given by <paramref name="bViews"/>.
        /// </summary>
        /// <param name="stream">The current accelerator stream to use.</param>
        /// <param name="aViews">The dense input matrices a of shape MxK.</param>
        /// <param name="bViews">
        /// The sparse matrices b of shape NxK (will transpose).
        /// </param>
        /// <param name="outViews">Dense output matrices of shape of MxN.</param>
        public void MultiplyBatchedTransposed(
            AcceleratorStream stream,
            IReadOnlyList<ArrayView2D<T, TStride>> aViews,
            IReadOnlyList<SparseMatrixView<T, TStride>> bViews,
            IReadOnlyList<ArrayView2D<T, TStride>> outViews)
        {
            if (aViews.Count != bViews.Count)
                throw new ArgumentOutOfRangeException(nameof(bViews));
            if (aViews.Count != outViews.Count)
                throw new ArgumentOutOfRangeException(nameof(outViews));

            ProcessConcurrently(stream, aViews.Count, (acceleratorStream, i) =>
                MultiplyTransposed(acceleratorStream, aViews[i], bViews[i], outViews[i]));
        }

        #endregion
    }
}
