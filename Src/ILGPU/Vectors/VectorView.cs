// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VectorView.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#pragma warning disable CA1043 // Use integral or string argument for indexers
#pragma warning disable CA1000 // Do not declare static members on generic types
#pragma warning disable CA2225 // Provide named methods as alternative for operators

namespace ILGPU.Algorithms.Vectors
{
    /// <summary>
    /// Represents a single 64bit-addressed vector view in memory with a specific stride
    /// that accesses all values in a stride way using the following memory layout:
    /// (x1, x2, x3, x4, ... xN) | (y1, y2, y3, y4, ... yN),
    /// where N is the dimension of the vector and the stride is equal to the number of
    /// vectors.
    /// </summary>
    /// <typeparam name="T">The underlying element type.</typeparam>
    public readonly struct SingleVectorView<T>
        where T : unmanaged
    {
        private readonly ArrayView<T> dataView;

        /// <summary>
        /// Constructs a new vector view pointing to a single vector.
        /// </summary>
        /// <param name="vectorView">The source linear vector view.</param>
        /// <param name="numVectors">The number of vectors.</param>
        /// <param name="dimension">The vector dimension of each vector.</param>
        /// <param name="vectorIndex">The current vector index.</param>
        public SingleVectorView(
            ArrayView<T> vectorView,
            LongIndex1D numVectors,
            Index1D dimension,
            LongIndex1D vectorIndex)
        {
            dataView = vectorView;
            
            NumVectors = numVectors;
            Dimension = dimension;
            VectorIndex = vectorIndex;
        }
        
        /// <summary>
        /// Constructs a new vector view pointing to a single vector.
        /// </summary>
        /// <param name="vectorView">
        /// The source linear vector view using the stride information as the number of
        /// vectors this view points to.
        /// </param>
        /// <param name="dimension">The vector dimension of each vector.</param>
        /// <param name="vectorIndex">The current vector index.</param>
        public SingleVectorView(
            ArrayView1D<T, Stride1D.General> vectorView,
            Index1D dimension,
            LongIndex1D vectorIndex)
        {
            dataView = vectorView.BaseView;
            
            NumVectors = vectorView.Stride.StrideExtent;
            Dimension = dimension;
            VectorIndex = vectorIndex;
        }

        /// <summary>
        /// Returns true if this view points to a valid location.
        /// </summary>
        public bool IsValid => dataView.IsValid;

        /// <summary>
        /// Returns the generic stride of this single vector view.
        /// </summary>
        public LongIndex1D NumVectors { get; }
        
        /// <summary>
        /// Returns the general vector index of this vector view
        /// </summary>
        public LongIndex1D VectorIndex { get; }
        
        /// <summary>
        /// Returns the dimension of this vector.
        /// </summary>
        public Index1D Dimension { get; }

        /// <summary>
        /// Returns a reference to the i-th vector element.
        /// </summary>
        /// <param name="elementIndex">The element index.</param>
        public ref T this[Index1D elementIndex]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Trace.Assert(
                    elementIndex < Dimension,
                    "Element index out of range");
                return ref dataView[elementIndex * NumVectors + VectorIndex];
            }
        }

        /// <summary>
        /// Converts a general data view into a single vector view.
        /// </summary>
        /// <param name="dataView">The source data view to convert.</param>
        /// <returns>The converted 64bit addressed vector view.</returns>
        public static implicit operator SingleVectorView<T>(
            ArrayView1D<T, Stride1D.General> dataView) =>
            new(dataView, dataView.IntLength, 0);
    }

    /// <summary>
    /// Represents a 64bit-addressed vector view in memory with a specific stride
    /// that accesses all values in a stride way using the following memory layout:
    /// (x1, x2, x3, x4, ... xN) | (y1, y2, y3, y4, ... yN),
    /// where N is the dimension of the vector and the stride is equal to the number of
    /// vectors.
    /// </summary>
    /// <typeparam name="T">The underlying element type.</typeparam>
    public readonly struct VectorView<T>
        where T : unmanaged
    {
        /// <summary>
        /// Allocates a new buffer compatible with this vector view.
        /// </summary>
        /// <param name="accelerator">The accelerator to use.</param>
        /// <param name="numVectors">The number of vectors to allocate.</param>
        /// <param name="dimension">The vector dimension of each vector.</param>
        /// <returns>The allocated memory buffer.</returns>
        public static MemoryBuffer2D<T, Stride2D.DenseY> Allocate(
            Accelerator accelerator,
            LongIndex1D numVectors,
            Index1D dimension) =>
            accelerator.Allocate2DDenseY<T>(
                new LongIndex2D(dimension,
                numVectors));
        
        /// <summary>
        /// Constructs a multi-vector view from the given 2D dense array view.
        /// </summary>
        /// <param name="arrayView2D">The dense source array view.</param>
        public VectorView(ArrayView2D<T, Stride2D.DenseY> arrayView2D)
        {
            DataView = arrayView2D;
        }
        
        /// <summary>
        /// Returns true if this view points to a valid location.
        /// </summary>
        public bool IsValid => DataView.IsValid;

        /// <summary>
        /// Returns the dimension of the vector.
        /// </summary>
        public Index1D Dimension => (Index1D)DataView.Extent.X;

        /// <summary>
        /// Returns the number of the vectors included in this view.
        /// </summary>
        public LongIndex1D NumVectors => DataView.Extent.Y;
        
        /// <summary>
        /// Returns the underlying dense array view.
        /// </summary>
        public ArrayView2D<T, Stride2D.DenseY> DataView { get; }

        /// <summary>
        /// Returns a reference to the j-th vector element of the i-th vector.
        /// </summary>
        /// <param name="vectorIndex">The source vector index.</param>
        /// <param name="elementIndex">The element index.</param>
        public ref T this[LongIndex1D vectorIndex, Index1D elementIndex]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref DataView[elementIndex, vectorIndex];
        }

        /// <summary>
        /// Returns a view to a single vector.
        /// </summary>
        /// <param name="vectorIndex">The vector index.</param>
        /// <returns>The sliced view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SingleVectorView<T> SliceVector(LongIndex1D vectorIndex) =>
            new(DataView.AsContiguous(), NumVectors, Dimension, vectorIndex);

        /// <summary>
        /// Converts a general data view into a vector view.
        /// </summary>
        /// <param name="dataView">The source data view to convert.</param>
        /// <returns>The converted 64bit addressed vector view.</returns>
        public static implicit operator VectorView<T>(
            ArrayView2D<T, Stride2D.DenseY> dataView) =>
            new(dataView);
    }
}

#pragma warning restore CA2225
#pragma warning restore CA1000
#pragma warning restore CA1043
