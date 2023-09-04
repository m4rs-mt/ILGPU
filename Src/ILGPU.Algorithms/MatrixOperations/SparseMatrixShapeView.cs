// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: SparseMatrixShapeView.cs
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
    /// Represents an abstract 2D sparse matrix shape
    /// </summary>
    /// <typeparam name="TStride">The 2D neighbor stride.</typeparam>
    public interface ISparseMatrixShapeView<TStride>
        where TStride : struct, IStride2D
    {
        /// <summary>
        /// NumRows x f matrix containing column indexes where non-zero values in matrix
        /// are for each row in [0:NumRows].
        /// </summary>
        ArrayView2D<int, TStride> Neighbors { get; }

        /// <summary>
        /// Vector with number of non-zero entries on each row of m_neighbors for all x,
        /// neighbors[x, numNeighbors[x]:MaxNonZeroEntries] may contain junk.
        /// </summary>
        ArrayView<int> NumNeighbors { get; }

        /// <summary>
        /// The number of rows.
        /// </summary>
        Index1D NumRows { get; }

        /// <summary>
        /// The number of columns.
        /// </summary>
        Index1D NumColumns { get; }
    }

    /// <summary>
    /// A single sparse matrix shape view containing information about the shape of a
    /// sparse matrix without specifying its data.
    /// </summary>
    /// <typeparam name="TStride">The 2D stride.</typeparam>
    public readonly struct SparseMatrixShapeView<TStride>
        : ISparseMatrixShapeView<TStride>
        where TStride : struct, IStride2D
    {
        /// <summary>
        /// Constructs a new sparse matrix shape.
        /// </summary>
        /// <param name="neighbors">The neighbors indexing view.</param>
        /// <param name="numNeighbors">The number of neighbors per row.</param>
        /// <param name="numRows">The number of rows of the source matrix.</param>
        /// <param name="numColumns">The number of columns of the source matrix.</param>
        public SparseMatrixShapeView(
            ArrayView2D<int, TStride> neighbors,
            ArrayView<int> numNeighbors,
            Index1D numRows,
            Index1D numColumns)
        {
            Trace.Assert(
                numRows > 0 &
                neighbors.Extent.X >= numRows &
                numNeighbors.IntLength >= numRows,
                "Invalid number of rows");
            Trace.Assert(
                numColumns >= 0,
                "Invalid number of columns");

            Neighbors = neighbors;
            NumNeighbors = numNeighbors;
            NumRows = numRows;
            NumColumns = numColumns;
        }

        /// <summary>
        /// NumRows x f matrix containing column indexes where non-zero values in matrix
        /// are for each row in [0:NumRows].
        /// </summary>
        public ArrayView2D<int, TStride> Neighbors { get; }

        /// <summary>
        /// Vector with number of non-zero entries on each row of m_neighbors for all x,
        /// neighbors[x, numNeighbors[x]:MaxNonZeroEntries] may contain junk.
        /// </summary>
        public ArrayView<int> NumNeighbors { get; }

        /// <summary>
        /// The number of rows.
        /// </summary>
        public Index1D NumRows { get; }

        /// <summary>
        /// The number of columns.
        /// </summary>
        public Index1D NumColumns { get; }

        /// <summary>
        /// Finds the requested column from the original dense matrix in neighbors.
        /// </summary>
        /// <remarks>
        /// This method uses a single-threaded binary search algorithm which can be
        /// slow on GPUs if the current num-neighbor view is not loaded into cache.
        /// </remarks>
        /// <param name="row">The row used for searching.</param>
        /// <param name="column">The column to look for.</param>
        /// <param name="index">The output index (if any).</param>
        /// <returns>True if the column index could be found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindColumn(Index1D row, Index1D column, out Index1D index)
        {
            int nonZero = NumNeighbors[row];

            // Simple binary search algorithm
            int left = 0;
            int right = nonZero - 1;
            while (left <= right)
            {
                index = left + (right - left) / 2;
                switch (Neighbors[row, index].CompareTo(column))
                {
                    case -1: left = index + 1; break;
                    case 0: return true;
                    case 1: right = index - 1; break;
                }
            }
            index = Index1D.Invalid;
            return false;
        }
    }
}
