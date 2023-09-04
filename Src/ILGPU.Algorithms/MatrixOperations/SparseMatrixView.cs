// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: SparseMatrixView.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.MatrixOperations
{
    /// <summary>
    /// Represents a sparse matrix in view pointing to data in GPU space
    /// </summary>
    public readonly struct SparseMatrixView<T, TStride> : ISparseMatrixShapeView<TStride>
        where T : unmanaged
        where TStride : struct, IStride2D
    {
        #region Instance

        /// <summary>
        /// Constructs a new view wrapping existing views that represent a sparse matrix.
        /// </summary>
        /// <param name="edgeWeights">A sparse shape.</param>
        /// <param name="shapeView">The values for all edges.</param>
        public SparseMatrixView(
            ArrayView2D<T, TStride> edgeWeights,
            SparseMatrixShapeView<TStride> shapeView)
        {
            EdgeWeights = edgeWeights;
            ShapeView = shapeView;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns a view pointing to all edge weights.
        /// </summary>
        public ArrayView2D<T, TStride> EdgeWeights { get; }

        /// <summary>
        /// Returns the associated shape view.
        /// </summary>
        public SparseMatrixShapeView<TStride> ShapeView { get; }

        /// <summary>
        /// Gets or sets a data element in the specified row and column. May be slow
        /// depending on caching. See <see cref="SparseMatrixView{T,TStride}"/> for more
        /// information.
        /// </summary>
        public T this[Index1D row, Index1D column]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!ShapeView.TryFindColumn(row, column, out var idx))
                    return default;
                return DirectAccess(row, idx);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (!ShapeView.TryFindColumn(row, column, out var idx))
                {
                    Trace.Assert(false, "Cannot store to non-sparse index");
                    return;
                }
                DirectAccess(row, idx) = value;
            }
        }

        #endregion

        #region ISparseMatrixShapeView

        /// <summary>
        /// NumRows x f matrix containing column indexes where non-zero values in matrix
        /// are for each row in [0:NumRows].
        /// </summary>
        public ArrayView2D<int, TStride> Neighbors => ShapeView.Neighbors;

        /// <summary>
        /// Vector with number of non-zero entries on each row of m_neighbors for all x,
        /// neighbors[x, numNeighbors[x]:MaxNonZeroEntries] may contain junk.
        /// </summary>
        public ArrayView<int> NumNeighbors => ShapeView.NumNeighbors;

        /// <summary>
        /// The number of rows.
        /// </summary>
        public Index1D NumRows => ShapeView.NumRows;

        /// <summary>
        /// The number of columns.
        /// </summary>
        public Index1D NumColumns => ShapeView.NumColumns;

        #endregion

        #region Methods

        /// <summary>
        /// Returns a memory reference to the desired data cell.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T DirectAccess(Index1D row, Index1D idx) =>
            ref EdgeWeights[row, idx];

        #endregion
    }
}
