// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: MaskedSparseMatrixExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.MatrixOperations
{
    #region Mask Predicates

    /// <summary>
    /// An internal predicate used to represent a masked array.
    /// </summary>
    /// <typeparam name="T">The mask element type.</typeparam>
    /// <typeparam name="TStride">The 2D stride.</typeparam>
    public readonly struct MaskPredicate<T, TStride> : InlineList.IPredicate<Index2D>
        where T : unmanaged, IEquatable<T>
        where TStride : struct, IStride2D
    {
        private readonly ArrayView2D<T, TStride> mask;
        private readonly T emptyValue;

        /// <summary>
        /// Creates a new mask predicate.
        /// </summary>
        /// <param name="maskView">The mask view to use.</param>
        /// <param name="emptyValueConstant">
        /// The masking constant to compare each element to.
        /// </param>
        public MaskPredicate(ArrayView2D<T, TStride> maskView, T emptyValueConstant)
        {
            mask = maskView;
            emptyValue = emptyValueConstant;
        }

        /// <summary>
        /// Returns true if the current mask element is not equal to the empty mask
        /// value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(Index2D item) => !mask[item.X, item.Y].Equals(emptyValue);
    }

    /// <summary>
    /// An internal predicate based on float values.
    /// </summary>
    /// <typeparam name="TStride">The 2D stride.</typeparam>
    public readonly struct FloatEpsPredicate<TStride> : InlineList.IPredicate<Index2D>
        where TStride : struct, IStride2D
    {
        private readonly ArrayView2D<float, TStride> values;
        private readonly float eps;

        /// <summary>
        /// Creates a new float masking predicate.
        /// </summary>
        /// <param name="valueView">The input value view.</param>
        /// <param name="epsConstant">
        /// The eps constant to compare each element to.
        /// </param>
        public FloatEpsPredicate(ArrayView2D<float, TStride> valueView, float epsConstant)
        {
            values = valueView;
            eps = epsConstant;
        }

        /// <summary>
        /// Returns true if the absolute value of the stored mask element is greater
        /// than the epsilon constant.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(Index2D item) => Math.Abs(values[item.X, item.Y]) > eps;
    }

    /// <summary>
    /// An internal predicate based on double values.
    /// </summary>
    /// <typeparam name="TStride">The 2D stride.</typeparam>
    public readonly struct DoubleEpsPredicate<TStride> : InlineList.IPredicate<Index2D>
        where TStride : struct, IStride2D
    {
        private readonly ArrayView2D<double, TStride> values;
        private readonly double eps;

        /// <summary>
        /// Creates a new float masking predicate.
        /// </summary>
        /// <param name="valueView">The input value view.</param>
        /// <param name="epsConstant">
        /// The eps constant to compare each element to.
        /// </param>
        public DoubleEpsPredicate(
            ArrayView2D<double, TStride> valueView,
            double epsConstant)
        {
            values = valueView;
            eps = epsConstant;
        }

        /// <summary>
        /// Returns true if the absolute value of the stored mask element is greater
        /// than the epsilon constant.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(Index2D item) => Math.Abs(values[item.X, item.Y]) > eps;
    }

    #endregion

    #region Matrix Processors

    /// <summary>
    /// An abstract context-specific mul-add operation for sparse matrix multiplications.
    /// </summary>
    /// <typeparam name="T">The value type to operate on.</typeparam>
    public interface IMaskedSparseMatrixProcessor<T>
        where T : struct
    {
        /// <summary>
        /// Performs a specialized sparse-matrix mul-add operation.
        /// </summary>
        /// <param name="summed">The currently summed value.</param>
        /// <param name="left">The left operand to multiply.</param>
        /// <param name="right">The right operand to multiply.</param>
        /// <returns>The summed and multiplied result.</returns>
        T MultiplyAdd(T summed, T left, T right);
    }

    /// <summary>
    /// A float-specific masked sparse matrix processor for matrix multiplications.
    /// </summary>
    public readonly struct FloatMaskedSparseMatrixProcessor
        : IMaskedSparseMatrixProcessor<float>
    {
        /// <summary>
        /// Performs a fma operation on floats.
        /// </summary>
        public float MultiplyAdd(float summed, float left, float right) =>
            summed + left * right;
    }

    /// <summary>
    /// A double-specific masked sparse matrix processor for matrix multiplications.
    /// </summary>
    public readonly struct DoubleMaskedSparseMatrixProcessor
        : IMaskedSparseMatrixProcessor<double>
    {
        /// <summary>
        /// Performs a fma operation on doubles.
        /// </summary>
        public double MultiplyAdd(double summed, double left, double right) =>
            summed + left * right;
    }

    #endregion

    /// <summary>
    /// A specialized accelerator-centric masked sparse matrix multiplier.
    /// </summary>
    /// <typeparam name="T">The abstract matrix value type.</typeparam>
    /// <typeparam name="TPredicate">The predicate type.</typeparam>
    /// <typeparam name="TStride">The matrix stride.</typeparam>
    /// <param name="stream">The current accelerator stream.</param>
    /// <param name="maskPredicate">
    /// The input masking predicate (targeting a dense matrix of shape MxK).
    /// </param>
    /// <param name="aView">A dense input matrix of shape MxK.</param>
    /// <param name="bView">A sparse matrix B of shape NxK (will transpose).</param>
    /// <param name="outView">
    /// A dense output view containing the results of the multiplication.
    /// </param>
    public delegate void MaskedSparseMatrixMultiplier<T, TPredicate, TStride>(
        AcceleratorStream stream,
        TPredicate maskPredicate,
        ArrayView2D<T, TStride> aView,
        SparseMatrixView<T, TStride> bView,
        ArrayView2D<T, TStride> outView)
        where T : unmanaged
        where TStride : struct, IStride2D
        where TPredicate : struct, InlineList.IPredicate<Index2D>;

    /// <summary>
    /// Specialized extensions to operate on sparse matrices while taking predicates
    /// into account to skip specific elements.
    /// </summary>
    public static class MaskedSparseMatrixExtensions
    {
        /// <summary>
        /// The matrix multiplication kernel that runs on the accelerated device while
        /// using thread compaction to free masked warps.
        /// </summary>
        /// <param name="maskPredicate">
        /// The input masking predicate (targeting a dense matrix of shape MxK).
        /// </param>
        /// <param name="aView">A dense input matrix of shape MxK.</param>
        /// <param name="bView">A sparse matrix B of shape NxK (will transpose).</param>
        /// <param name="outView">
        /// A dense output view containing the results of the multiplication.
        /// </param>
        /// <param name="processor">An instance of an actual mul-add operation.</param>
        internal static void MaskedSparseTransposedMatrixMultiplierKernel<
            T,
            TPredicate,
            TStride,
            TProcessor>(
            TPredicate maskPredicate,
            ArrayView2D<T, TStride> aView,
            SparseMatrixView<T, TStride> bView,
            ArrayView2D<T, TStride> outView,
            TProcessor processor)
            where T : unmanaged
            where TStride : struct, IStride2D
            where TPredicate : struct, InlineList.IPredicate<Index2D>
            where TProcessor : struct, IMaskedSparseMatrixProcessor<T>
        {
            var maskView = SharedMemory.GetDynamic<int>();
            Trace.Assert(
                Group.DimX <= maskView.IntLength,
                "Invalid shared memory config");

            // Get all predicate results and perform thread compaction
            int index = Grid.GlobalLinearIndex;
            var maskIndex2D = outView.Stride.ReconstructFromElementIndex(index);
            bool apply =
                maskIndex2D.X < outView.Extent.X &
                maskIndex2D.Y < outView.Extent.Y;

            // Check the predicate (if in range) and compute thread-compaction offsets
            if (apply)
                apply = maskPredicate.Apply(maskIndex2D);
            int groupOffset = GroupExtensions.ExclusiveScan<int, AddInt32>(
                Utilities.Select(apply, 1, 0));

            // Store current result and determine the total number of matrix entries
            if (apply)
                maskView[groupOffset] = index;
            int numEntries = Group.BarrierPopCount(apply);

            // Load the current entry (if any)
            if (Group.IdxX >= numEntries)
                return;

            // Get the transposed index from our processing index
            int processingIndex = maskView[Group.IdxX];
            var index2D = outView.Stride.ReconstructFromElementIndex(
                processingIndex);

            // Load next number of neighbors
            T dotProduct = default;
            int numNeighbors = bView.NumNeighbors[index2D.Y];

            // Note that profiling yielded that explicit L1 shared-memory caching did not
            // help to improve performance in this case. This is also due to the fact
            // that most reused results automatically end up being fetching into L2 cache
            // and loaded upon request (based on the structure of the algorithm) into the
            // L1 parts of the processing unit

            for (var neighborIndex = 0; neighborIndex < numNeighbors; ++neighborIndex)
            {
                // Load index and sparse edge weight; note that the format for bView
                // means we will read the transposed entry from bView
                int columnIndex = bView.Neighbors[index2D.Y, neighborIndex];
                T bValue = bView.EdgeWeights[index2D.Y, neighborIndex];

                // Load our multiplication value from aView
                T aValue = aView[index2D.X, columnIndex];

                // Accumulate result
                dotProduct = processor.MultiplyAdd(dotProduct, bValue, aValue);
            }

            // Store our result value
            outView[index2D] = dotProduct;
        }

        /// <summary>
        /// Creates a specialized sparse matrix multiplier.
        /// </summary>
        /// <typeparam name="T">The abstract matrix value type.</typeparam>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <typeparam name="TStride">The matrix stride.</typeparam>
        /// <typeparam name="TProcessor">The processor type to operate on T.</typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <returns>A new sparse matrix multiplier.</returns>
        public static MaskedSparseMatrixMultiplier<T, TPredicate, TStride>
            CreateSparseTransposedMatrixMultiplierMasked<
            T,
            TPredicate,
            TStride,
            TProcessor>(
            this Accelerator accelerator)
            where T : unmanaged
            where TStride : struct, IStride2D
            where TPredicate : struct, InlineList.IPredicate<Index2D>
            where TProcessor : struct, IMaskedSparseMatrixProcessor<T>
        {
            // Load basic sparse matrix convert kernel
            var kernel = accelerator.LoadKernel<
                TPredicate,
                ArrayView2D<T, TStride>,
                SparseMatrixView<T, TStride>,
                ArrayView2D<T, TStride>,
                TProcessor>(MaskedSparseTransposedMatrixMultiplierKernel);

            // Get the optimal group size
            int groupSize = accelerator.EstimateGroupSize(kernel.GetKernel());

            // Return new launcher delegate
            return (stream, predicate, view, bView, outView) =>
            {
                // Get actual processor
                TProcessor processor = default;
                
                // Bounds checks
                if (view.Extent.X != outView.Extent.X)
                    throw new ArgumentOutOfRangeException(nameof(view));
                if (bView.NumRows != outView.Extent.Y)
                    throw new ArgumentOutOfRangeException(nameof(bView));

                // Determine launch dimensions
                var sharedMemoryConfig = SharedMemoryConfig.RequestDynamic<int>(
                    groupSize);
                int gridDim = XMath.DivRoundUp(outView.IntLength, groupSize);
                KernelConfig kernelConfig = (gridDim, groupSize, sharedMemoryConfig);

                // Launch kernel
                kernel(stream, kernelConfig, predicate, view, bView, outView, processor);
            };
        }

        /// <summary>
        /// Multiplies a masked sparse matrix based on arbitrary types.
        /// </summary>
        /// <typeparam name="T">The abstract matrix value type.</typeparam>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <typeparam name="TStride">The matrix stride.</typeparam>
        /// <typeparam name="TProcessor">The processor type to operate on T.</typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="maskPredicate">The mask predicate input.</param>
        /// <param name="aView">The dense input matrix a of shape MxK.</param>
        /// <param name="bView">The sparse matrix b of shape NxK.</param>
        /// <param name="outView">A dense output matrix of shape of aView.</param>
        public static void MultiplySparseTransposedMatrixMasked<
            T,
            TPredicate,
            TStride,
            TProcessor>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            TPredicate maskPredicate,
            ArrayView2D<T, TStride> aView,
            SparseMatrixView<T, TStride> bView,
            ArrayView2D<T, TStride> outView)
            where T : unmanaged
            where TStride : struct, IStride2D
            where TPredicate : struct, InlineList.IPredicate<Index2D>
            where TProcessor : struct, IMaskedSparseMatrixProcessor<T>
        {
            // Get multiplier kernel (from cache, if possible)
            var multiplierKernel = accelerator.
                CreateSparseTransposedMatrixMultiplierMasked<
                T,
                TPredicate,
                TStride,
                TProcessor>();

            // Launch kernel
            multiplierKernel(stream, maskPredicate, aView, bView, outView);
        }

        /// <summary>
        /// Multiplies a masked sparse matrix based on floats.
        /// </summary>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <typeparam name="TStride">The matrix stride.</typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="maskPredicate">The mask predicate input.</param>
        /// <param name="aView">The dense input matrix a of shape MxK.</param>
        /// <param name="bView">The sparse matrix b of shape NxK.</param>
        /// <param name="outView">A dense output matrix of shape of aView.</param>
        public static void MultiplySparseTransposedMatrixMasked<TPredicate, TStride>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            TPredicate maskPredicate,
            ArrayView2D<float, TStride> aView,
            SparseMatrixView<float, TStride> bView,
            ArrayView2D<float, TStride> outView)
            where TStride : struct, IStride2D
            where TPredicate : struct, InlineList.IPredicate<Index2D> =>
            accelerator.MultiplySparseTransposedMatrixMasked<
                float,
                TPredicate,
                TStride,
                FloatMaskedSparseMatrixProcessor>(
                stream,
                maskPredicate,
                aView,
                bView,
                outView);

        /// <summary>
        /// Multiplies a masked sparse matrix based on doubles.
        /// </summary>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <typeparam name="TStride">The matrix stride.</typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="maskPredicate">The mask predicate input.</param>
        /// <param name="aView">The dense input matrix a of shape MxK.</param>
        /// <param name="bView">The sparse matrix b of shape NxK.</param>
        /// <param name="outView">A dense output matrix of shape of aView.</param>
        public static void MultiplySparseTransposedMatrixMasked<TPredicate, TStride>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            TPredicate maskPredicate,
            ArrayView2D<double, TStride> aView,
            SparseMatrixView<double, TStride> bView,
            ArrayView2D<double, TStride> outView)
            where TStride : struct, IStride2D
            where TPredicate : struct, InlineList.IPredicate<Index2D> =>
            accelerator.MultiplySparseTransposedMatrixMasked<
                double,
                TPredicate,
                TStride,
                DoubleMaskedSparseMatrixProcessor>(
                stream,
                maskPredicate,
                aView,
                bView,
                outView);
    }
}
