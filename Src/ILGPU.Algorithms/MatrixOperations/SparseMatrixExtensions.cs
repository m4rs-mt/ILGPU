// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: SparseMatrixExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using System;

namespace ILGPU.Algorithms.MatrixOperations
{
    #region Sparse Matrix Types
        
    /// <summary>
    /// A generic target to compute sparse matrix information.
    /// </summary>
    public interface ISparseMatrixShapeInfoTarget
    {
        /// <summary>
        /// Outputs the given number of row entries for the specified row.
        /// </summary>
        /// <param name="rowIndex">The absolute row index.</param>
        /// <param name="numRowEntries">The number of row entries.</param>
        void OutputNumNeighbors(int rowIndex, int numRowEntries);
        
        /// <summary>
        /// Atomically computes the maximum of all local num neighbors.
        /// </summary>
        /// <param name="maxNumLocalNeighbors">
        /// The locally determined max number of neighbors.
        /// </param>
        void ComputeAtomicMaxNumNeighbors(int maxNumLocalNeighbors);
    }

    /// <summary>
    /// A provider for matrix shape information accepting a generic target info receiver.
    /// </summary>
    /// <typeparam name="TPredicate">The predicate type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    public delegate void SparseMatrixShapeInfoProvider<TPredicate, TTarget>(
        AcceleratorStream stream,
        LongIndex2D matrixExtent,
        TPredicate predicate,
        TTarget target)
        where TPredicate : struct, InlineList.IPredicate<Index2D>
        where TTarget : struct, ISparseMatrixShapeInfoTarget;

    /// <summary>
    /// A provider for matrix shape information accepting a predicate and a number of
    /// neighbors per row view (needs to pre-allocated).
    /// </summary>
    /// <typeparam name="TPredicate">The predicate type.</typeparam>
    public delegate int SparseMatrixShapeInfoProvider<TPredicate>(
        AcceleratorStream stream,
        LongIndex2D matrixExtent,
        TPredicate predicate,
        ArrayView<int> numNeighbors)
        where TPredicate : struct, InlineList.IPredicate<Index2D>;

    /// <summary>
    /// A sparse matrix shape converter that translates dense matrices into sparse shapes.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TPredicate">The predicate type.</typeparam>
    /// <typeparam name="TStride">The matrix stride.</typeparam>
    public delegate SparseMatrixShapeView<TStride> SparseMatrixShapeConverter<
        T,
        TPredicate,
        TStride>(
        AcceleratorStream stream,
        ArrayView2D<T, TStride> inputMatrix,
        TPredicate predicate,
        ArrayView<int> numNeighbors,
        Func<int, ArrayView2D<int, TStride>> getNeighborsFunc)
        where T : unmanaged
        where TPredicate : struct, InlineList.IPredicate<Index2D>
        where TStride : struct, IStride2D;
    
    /// <summary>
    /// A sparse matrix converter that translates a sparse shape view and a dense matrix
    /// into its sparse view representation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The matrix stride.</typeparam>
    public delegate SparseMatrixView<T, TStride> SparseMatrixConverter<T, TStride>(
        AcceleratorStream stream,
        ArrayView2D<T, TStride> inputMatrix,
        SparseMatrixShapeView<TStride> shapeView,
        ArrayView2D<T, TStride> dataView)
        where T : unmanaged
        where TStride : struct, IStride2D;

    #endregion
    
    /// <summary>
    /// Sparse matrix extensions to convert dense matrices into sparse versions.
    /// </summary>
    public static class SparseMatrixExtensions
    {
        /// <summary>
        /// An explicitly grouped kernel to compute direct neighbor information and the
        /// maximum number of non-zero entries per row.
        /// </summary>
        /// <typeparam name="TPredicate">
        /// The predicate type used to sparsify a dense input matrix.
        /// </typeparam>
        /// <typeparam name="TTarget">The sparsity output type.</typeparam>
        internal static void SparseMatrixShapeInfoKernel<TPredicate, TTarget>(
            LongIndex2D matrixExtent,
            TPredicate predicate,
            TTarget target)
            where TPredicate : struct, InlineList.IPredicate<Index2D>
            where TTarget : struct, ISparseMatrixShapeInfoTarget
        {
            // Setup our shared max-num-neighbors memory counter
            ref var sharedMax = ref SharedMemory.Allocate<int>();
            if (Group.IsFirstThread)
                sharedMax = 0;
            Group.Barrier();
            
            // Get the actual row index and reject out-of-bounds reads
            int rowIndex = Grid.GlobalLinearIndex;
            if (rowIndex >= matrixExtent.X)
                return;
            
            int numRowEntries = 0;
            for (int i = 0, numColumns = (int)matrixExtent.Y; i < numColumns; ++i)
            {
                if (predicate.Apply(new Index2D(rowIndex, i)))
                    ++numRowEntries;
            }
            
            // Store number of neighbors per row and adjust shared max row counter
            target.OutputNumNeighbors(rowIndex, numRowEntries);
            Atomic.Max(ref sharedMax, numRowEntries);
            
            // Wait for all threads and adjust global max-non-zero counter
            Group.Barrier();
            if (Group.IsFirstThread)
                target.ComputeAtomicMaxNumNeighbors(sharedMax);
        }
        
        /// <summary>
        /// A generic sparse matrix converter kernel to convert a given dense input matrix
        /// into its sparse shape representation.
        /// </summary>
        /// <typeparam name="T">The value type to operate on.</typeparam>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <typeparam name="TStride">The element striding of the matrices.</typeparam>
        internal static void SparseMatrixShapeConverterKernel<T, TPredicate, TStride>(
            Index1D index,
            ArrayView2D<T, TStride> inputMatrix,
            TPredicate predicate,
            ArrayView2D<int, TStride> neighbors)
            where T : unmanaged
            where TStride : struct, IStride2D
            where TPredicate : struct, InlineList.IPredicate<Index2D>
        {
            int relativeIndex = 0;
            for (int i = 0, numColumns = (int)inputMatrix.Extent.Y; i < numColumns; ++i)
            {
                if (!predicate.Apply(new Index2D(index, i)))
                    continue;
                int columnIndex = relativeIndex++;
                neighbors[index, columnIndex] = i;
            }
        }
        
        /// <summary>
        /// A generic sparse matrix converter kernel to convert a given dense input matrix
        /// into its sparse data representation.
        /// </summary>
        /// <typeparam name="T">The value type to operate on.</typeparam>
        /// <typeparam name="TStride">The element striding of the matrices.</typeparam>
        internal static void SparseMatrixConverterKernel<T, TStride>(
            Index1D index,
            ArrayView2D<T, TStride> inputMatrix,
            SparseMatrixShapeView<TStride> shapeView,
            ArrayView2D<T, TStride> edgeWeights)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            int numNeighbors = shapeView.NumNeighbors[index];
            for (int i = 0; i < numNeighbors; ++i)
            {
                int columnIndex = shapeView.Neighbors[index, i];
                edgeWeights[index, i] = inputMatrix[index, columnIndex];
            }
        }
        
        /// <summary>
        /// Creates a new sparse matrix shape info provider.
        /// </summary>
        /// <typeparam name="TPredicate">
        /// The predicate used to sparsify the input matrix.
        /// </typeparam>
        /// <typeparam name="TTarget">The target view type.</typeparam>
        public static SparseMatrixShapeInfoProvider<TPredicate, TTarget>
            CreateSparseMatrixInfoProvider<
            TPredicate,
            TTarget>(this Accelerator accelerator)
            where TPredicate : struct, InlineList.IPredicate<Index2D>
            where TTarget : struct, ISparseMatrixShapeInfoTarget
        {
            // Load basic sparse matrix info kernel
            var kernel = accelerator.LoadKernel<
                LongIndex2D,
                TPredicate,
                TTarget>(SparseMatrixShapeInfoKernel);

            // Determine the optimal group size for this kernel
            int groupSize = accelerator.EstimateGroupSize(kernel.GetKernel());

            return (stream, extent, predicate, target) =>
            {
                int numGroups = (int)XMath.DivRoundUp(extent.X, groupSize);
                kernel(stream, (numGroups, groupSize), extent, predicate, target);
            };
        }
        
        /// <summary>
        /// A specific sparse view target.
        /// </summary>
        internal readonly struct SparseViewTarget : ISparseMatrixShapeInfoTarget
        {
            private readonly ArrayView<int> numNeighbors;
            private readonly VariableView<int> maxNumNeighbors;

            /// <summary>
            /// Constructs a new sparse view target.
            /// </summary>
            /// <param name="numNeighborsView">The global number of neighbors.</param>
            /// <param name="maxNumNeighborsView">
            /// The global max number of neighbors.
            /// </param>
            public SparseViewTarget(
                ArrayView<int> numNeighborsView,
                VariableView<int> maxNumNeighborsView)
            {
                numNeighbors = numNeighborsView;
                maxNumNeighbors = maxNumNeighborsView;
            }
            
            /// <summary>
            /// Stores the given number of row entries as number of neighbors.
            /// </summary>
            public void OutputNumNeighbors(int rowIndex, int numRowEntries) =>
                numNeighbors[rowIndex] = numRowEntries;

            /// <summary>
            /// Atomically computes the global maximum number of neighbors.
            /// </summary>
            public void ComputeAtomicMaxNumNeighbors(int maxNumLocalNeighbors) =>
                Atomic.Max(ref maxNumNeighbors.Value, maxNumLocalNeighbors);
        }
        
        /// <summary>
        /// Creates a new sparse matrix shape info provider.
        /// </summary>
        /// <typeparam name="TPredicate">
        /// The predicate used to sparsify the input matrix.
        /// </typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="tempView">
        /// The input temporary value view (at least of length 1).
        /// </param>
        public static SparseMatrixShapeInfoProvider<TPredicate>
            CreateSparseMatrixInfoProvider<TPredicate>(
            this Accelerator accelerator,
            ArrayView<int> tempView)
            where TPredicate : struct, InlineList.IPredicate<Index2D>
        {
            // Load basic sparse matrix info kernel
            var infoProvider = accelerator.CreateSparseMatrixInfoProvider<
                TPredicate,
                SparseViewTarget>();

            // Allocate page locked memory for fast count transfers
            if (tempView.Length < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tempView),
                    "Temp view needs to have at least a single element");
            }
            var internalView = tempView.SubView(0, 1);
            
            // Return custom wrapper
            return (stream, extent, predicate, numNeighborsView) =>
            {
                // Construct sparse target and reset data
                var sparseTarget = new SparseViewTarget(
                    numNeighborsView,
                    internalView.VariableView(0));
                internalView.MemSetToZero(stream);

                // Get info
                infoProvider(stream, extent, predicate, sparseTarget);
                
                // Fetch max count
                int maxCount = 0;
                internalView.CopyToCPU(stream, ref maxCount, 1);
                return maxCount;
            };
        }

        /// <summary>
        /// Compute new sparse matrix shape info.
        /// </summary>
        /// <typeparam name="TPredicate">
        /// The predicate used to sparsify the input matrix.
        /// </typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="matrixExtent">The dense input matrix extent.</param>
        /// <param name="predicate">The predicate used to sparsify the matrix.</param>
        /// <param name="numNeighbors">The number of neighbors per row.</param>
        /// <param name="tempView">
        /// The input temporary value view (at least of length 1).
        /// </param>
        public static int ComputeSparseMatrixShapeInfo<TPredicate>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            Index2D matrixExtent,
            TPredicate predicate,
            ArrayView<int> numNeighbors,
            ArrayView<int> tempView)
            where TPredicate : struct, InlineList.IPredicate<Index2D>
        {
            // Get or create provider
            var provider = accelerator.CreateSparseMatrixInfoProvider<TPredicate>(
                tempView);

            // Compute actual sparsity information
            return provider(stream, matrixExtent, predicate, numNeighbors);
        }
        
        /// <summary>
        /// Creates a new sparse matrix shape provider.
        /// </summary>
        /// <typeparam name="T">The value type to operate on.</typeparam>
        /// <typeparam name="TPredicate">
        /// The predicate used to sparsify the input matrix.
        /// </typeparam>
        /// <typeparam name="TStride">The element striding of the matrices.</typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="tempView">
        /// The input temporary value view (at least of length 1).
        /// </param>
        public static SparseMatrixShapeConverter<T, TPredicate, TStride>
            CreateSparseMatrixShapeConverter<T, TPredicate, TStride>(
            this Accelerator accelerator,
            ArrayView<int> tempView)
            where T : unmanaged
            where TStride : struct, IStride2D
            where TPredicate : struct, InlineList.IPredicate<Index2D>
        {
            // Load basic sparse matrix convert kernel
            var kernel = accelerator.LoadAutoGroupedKernel<
                Index1D,
                ArrayView2D<T, TStride>,
                TPredicate,
                ArrayView2D<int, TStride>>(SparseMatrixShapeConverterKernel);
            
            // Load basic info provider
            var infoProvider = accelerator.CreateSparseMatrixInfoProvider<TPredicate>(
                tempView);
            
            // Returns new launcher delegate
            return (stream, matrix, predicate, numNeighbors, getNeighborsFunc) =>
            {
                // Determine an info value
                int max = infoProvider(stream, matrix.Extent, predicate, numNeighbors);
                
                // Convert the actual neighbor information
                var neighbors = getNeighborsFunc(max);
                kernel(stream, (int)matrix.Extent.X, matrix, predicate, neighbors);
                return new SparseMatrixShapeView<TStride>(
                    neighbors,
                    numNeighbors,
                    matrix.IntExtent.X,
                    matrix.IntExtent.Y);
            };
        }

        /// <summary>
        /// Creates a new sparse matrix shape provider.
        /// </summary>
        /// <typeparam name="T">The value type to operate on.</typeparam>
        /// <typeparam name="TPredicate">
        /// The predicate used to sparsify the input matrix.
        /// </typeparam>
        /// <typeparam name="TStride">The element striding of the matrices.</typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="inputMatrix">The dense input matrix.</param>
        /// <param name="predicate">The predicate used to sparsify the matrix.</param>
        /// <param name="numNeighbors">The number of neighbors per row.</param>
        /// <param name="getNeighborsFunc">A 2D sparse neighbors view provider.</param>
        /// <param name="tempView">
        /// The input temporary value view (at least of length 1).
        /// </param>
        public static SparseMatrixShapeView<TStride>
            ComputeSparseMatrixShapeConverter<T, TPredicate, TStride>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView2D<T, TStride> inputMatrix,
            TPredicate predicate,
            ArrayView<int> numNeighbors,
            Func<int, ArrayView2D<int, TStride>> getNeighborsFunc,
            ArrayView<int> tempView)
            where T : unmanaged
            where TPredicate : struct, InlineList.IPredicate<Index2D>
            where TStride : struct, IStride2D
        {
            // Get or create provider
            var converter = accelerator.CreateSparseMatrixShapeConverter<
                T,
                TPredicate,
                TStride>(tempView);
            
            // Convert to the resulting shape shape view
            return converter(
                stream,
                inputMatrix,
                predicate,
                numNeighbors,
                getNeighborsFunc);
        }

        /// <summary>
        /// Creates a new sparse matrix converter.
        /// </summary>
        /// <typeparam name="T">The value type to operate on.</typeparam>
        /// <typeparam name="TStride">The element striding of the matrices.</typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        public static SparseMatrixConverter<T, TStride> CreateSparseMatrixConverter<
            T,
            TStride>(this Accelerator accelerator)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            // Load basic sparse matrix convert kernel
            var kernel = accelerator.LoadAutoGroupedKernel<
                Index1D,
                ArrayView2D<T, TStride>,
                SparseMatrixShapeView<TStride>,
                ArrayView2D<T, TStride>>(SparseMatrixConverterKernel);
            
            // Returns new launcher delegate
            return (stream, matrix, shapeView, edgeView) =>
            {
                kernel(stream, (int)matrix.Extent.X, matrix, shapeView, edgeView);
                return new SparseMatrixView<T, TStride>(edgeView, shapeView);
            };
        }
        
        /// <summary>
        /// Computes a new sparse matrix view.
        /// </summary>
        /// <typeparam name="T">The value type to operate on.</typeparam>
        /// <typeparam name="TStride">The element striding of the matrices.</typeparam>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="stream">The current accelerator stream.</param>
        /// <param name="inputMatrix">The dense input matrix.</param>
        /// <param name="shapeView">The input shape view (pre allocated).</param>
        /// <param name="dataView">The sparse data view (pre allocated).</param>
        public static SparseMatrixView<T, TStride> ComputeSparseMatrixView<T, TStride>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView2D<T, TStride> inputMatrix,
            SparseMatrixShapeView<TStride> shapeView,
            ArrayView2D<T, TStride> dataView)
            where T : unmanaged
            where TStride : struct, IStride2D
        {
            // Get or create provider
            var converter = accelerator.CreateSparseMatrixConverter<T, TStride>();
            
            // Convert our dense matrix
            return converter(stream, inputMatrix, shapeView, dataView);
        }
    }
}
