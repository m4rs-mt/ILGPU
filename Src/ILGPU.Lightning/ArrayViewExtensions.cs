// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ArrayViewExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents extension methods for array views.
    /// </summary>
    public static class ArrayViewExtensions
    {
        #region ArrayView

        /// <summary>
        /// Copies the contents of this view to the memory location of the given view.
        /// </summary>
        /// <param name="sourceView">The source view.</param>
        /// <param name="targetView">The target view.</param>
        /// <remarks>The target view must be accessible from the source view (e.g. same accelerator).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo<T>(this ArrayView<T> sourceView, ArrayView<T> targetView)
            where T : struct
        {
            Debug.Assert(sourceView.Extent <= targetView.Extent, "Target view out of range");
            for (Index i = 0; i < targetView.Extent; ++i)
                targetView[i] = sourceView[i];
        }

        /// <summary>
        /// Copies the contents of this view to the memory location of the given view using
        /// multiple threads in the scope of a group.
        /// </summary>
        /// <param name="sourceView">The source view.</param>
        /// <param name="targetView">The target view.</param>
        /// <param name="threadIdx">The relative thread index in the scope of a group [0, Group.Dimension.X - 1].</param>
        /// <remarks>The target view must be accessible from the this view (e.g. same accelerator).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyGroupedTo<T>(this ArrayView<T> sourceView, ArrayView<T> targetView, Index threadIdx)
            where T : struct
        {
            Debug.Assert(sourceView.Extent <= targetView.Extent, "Target view out of range");
            Debug.Assert(threadIdx >= Index.Zero && threadIdx < Group.Dimension.X, "Thread index out of range");
            for (Index i = threadIdx; i < sourceView.Extent; i += Group.Dimension.X)
                targetView[i] = sourceView[i];
        }

        #endregion

        #region ArrayView2D

        /// <summary>
        /// Copies the contents of this view to the memory location of the given view.
        /// </summary>
        /// <param name="sourceView">The source view.</param>
        /// <param name="targetView">The target view.</param>
        /// <remarks>The target view must be accessible from the this view (e.g. same accelerator).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyToT<T>(this ArrayView2D<T> sourceView, ArrayView2D<T> targetView)
            where T : struct
        {
            Debug.Assert(sourceView.Extent <= targetView.Extent, "Target view out of range");
            for (Index i = 0; i < sourceView.Extent.X; ++i)
                for (Index j = 0; j < sourceView.Extent.Y; ++j)
                {
                    var idx = new Index2(i, j);
                    targetView[idx] = sourceView[idx];
                }
        }

        /// <summary>
        /// Copies the contents of this view to the memory location of the given view using
        /// multiple threads in the scope of a group.
        /// </summary>
        /// <param name="sourceView">The source view.</param>
        /// <param name="targetView">The target view.</param>
        /// <param name="threadIdx">The relative thread index in the scope of a group [0, Group.Dimension.X - 1].</param>
        /// <remarks>The target view must be accessible from the this view (e.g. same accelerator).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyGroupedTo<T>(this ArrayView2D<T> sourceView, ArrayView2D<T> targetView, Index threadIdx)
            where T : struct
        {
            Debug.Assert(sourceView.Extent <= targetView.Extent, "Target view out of range");
            Debug.Assert(threadIdx.X >= Index.Zero && threadIdx.X < Group.Dimension.X, "X thread-index out of range");
            for (Index i = threadIdx.X; i < sourceView.Extent.X; i += Group.Dimension.X)
                for (Index j = 0; j < sourceView.Extent.Y; ++j)
                {
                    var idx = new Index2(i, j);
                    targetView[idx] = sourceView[idx];
                }
        }

        /// <summary>
        /// Copies the contents of this view to the memory location of the given view using
        /// multiple threads in the scope of a group.
        /// </summary>
        /// <param name="sourceView">The source view.</param>
        /// <param name="targetView">The target view.</param>
        /// <param name="threadIdx">The relative thread index in the scope of a group [(0, 0), (Group.Dimension.X - 1, Group.Dimension.Y - 1)].</param>
        /// <remarks>The target view must be accessible from the this view (e.g. same accelerator).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyGroupedTo<T>(this ArrayView2D<T> sourceView, ArrayView2D<T> targetView, Index2 threadIdx)
            where T : struct
        {
            Debug.Assert(sourceView.Extent <= targetView.Extent, "Target view out of range");
            Debug.Assert(threadIdx.X >= Index.Zero && threadIdx.X < Group.Dimension.X, "X thread-index out of range");
            Debug.Assert(threadIdx.Y >= Index.Zero && threadIdx.Y < Group.Dimension.Y, "Y thread-index out of range");
            for (Index i = threadIdx.X; i < sourceView.Extent.X; i += Group.Dimension.X)
                for (Index j = threadIdx.Y; j < sourceView.Extent.Y; j += Group.Dimension.Y)
                {
                    var idx = new Index2(i, j);
                    targetView[idx] = sourceView[idx];
                }
        }

        #endregion

        #region ArrayView3D


        /// <summary>
        /// Copies the contents of this view to the memory location of the given view.
        /// </summary>
        /// <param name="sourceView">The source view.</param>
        /// <param name="targetView">The target view.</param>
        /// <remarks>The target view must be accessible from the this view (e.g. same accelerator).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo<T>(this ArrayView3D<T> sourceView, ArrayView3D<T> targetView)
            where T : struct
        {
            Debug.Assert(sourceView.Extent <= targetView.Extent, "Target view out of range");
            for (Index i = 0; i < sourceView.Extent.X; ++i)
                for (Index j = 0; j < sourceView.Extent.Y; ++j)
                    for (Index k = 0; k < sourceView.Extent.Z; ++k)
                    {
                        var idx = new Index3(i, j, k);
                        targetView[idx] = sourceView[idx];
                    }
        }

        /// <summary>
        /// Copies the contents of this view to the memory location of the given view using
        /// multiple threads in the scope of a group.
        /// </summary>
        /// <param name="sourceView">The source view.</param>
        /// <param name="targetView">The target view.</param>
        /// <param name="threadIdx">The relative thread index in the scope of a group [0, Group.Dimension.X - 1].</param>
        /// <remarks>The target view must be accessible from the this view (e.g. same accelerator).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyGroupedTo<T>(this ArrayView3D<T> sourceView, ArrayView3D<T> targetView, Index threadIdx)
            where T : struct
        {
            Debug.Assert(sourceView.Extent <= targetView.Extent, "Target view out of range");
            Debug.Assert(threadIdx >= Index.Zero && threadIdx < Group.Dimension.X, "Thread index out of range");
            for (Index i = threadIdx; i < sourceView.Extent.X; i += Group.Dimension.X)
                for (Index j = 0; j < sourceView.Extent.Y; ++j)
                    for (Index k = 0; k < sourceView.Extent.Z; ++k)
                    {
                        var idx = new Index3(i, j, k);
                        targetView[idx] = sourceView[idx];
                    }
        }

        /// <summary>
        /// Copies the contents of this view to the memory location of the given view using
        /// multiple threads in the scope of a group.
        /// </summary>
        /// <param name="sourceView">The source view.</param>
        /// <param name="targetView">The target view.</param>
        /// <param name="threadIdx">The relative thread index in the scope of a group [(0, 0), (Group.Dimension.X - 1, Group.Dimension.Y - 1)].</param>
        /// <remarks>The target view must be accessible from the this view (e.g. same accelerator).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyGroupedTo<T>(this ArrayView3D<T> sourceView, ArrayView3D<T> targetView, Index2 threadIdx)
            where T : struct
        {
            Debug.Assert(sourceView.Extent <= targetView.Extent, "Target view out of range");
            Debug.Assert(threadIdx.X >= Index.Zero && threadIdx.X < Group.Dimension.X, "X thread-index out of range");
            Debug.Assert(threadIdx.Y >= Index.Zero && threadIdx.Y < Group.Dimension.Y, "Y thread-index out of range");
            for (Index i = threadIdx.X; i < sourceView.Extent.X; i += Group.Dimension.X)
                for (Index j = threadIdx.Y; j < sourceView.Extent.Y; j += Group.Dimension.Y)
                    for (Index k = 0; k < sourceView.Extent.Z; ++k)
                    {
                        var idx = new Index3(i, j, k);
                        targetView[idx] = sourceView[idx];
                    }
        }

        /// <summary>
        /// Copies the contents of this view to the memory location of the given view using
        /// multiple threads in the scope of a group.
        /// </summary>
        /// <param name="sourceView">The source view.</param>
        /// <param name="targetView">The target view.</param>
        /// <param name="threadIdx">The relative thread index in the scope of a group [(0, 0), Group.Dimension - 1].</param>
        /// <remarks>The target view must be accessible from the this view (e.g. same accelerator).</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyGroupedTo<T>(this ArrayView3D<T> sourceView, ArrayView3D<T> targetView, Index3 threadIdx)
            where T : struct
        {
            Debug.Assert(sourceView.Extent <= targetView.Extent, "Target view out of range");
            Debug.Assert(threadIdx.X >= Index.Zero && threadIdx.X < Group.Dimension.X, "X thread-index out of range");
            Debug.Assert(threadIdx.Y >= Index.Zero && threadIdx.Y < Group.Dimension.Y, "Y thread-index out of range");
            Debug.Assert(threadIdx.Z >= Index.Zero && threadIdx.Z < Group.Dimension.Z, "Z thread-index out of range");
            for (Index i = threadIdx.X; i < sourceView.Extent.X; i += Group.Dimension.X)
                for (Index j = threadIdx.Y; j < sourceView.Extent.Y; j += Group.Dimension.Y)
                    for (Index k = threadIdx.Z; k < sourceView.Extent.Z; k += Group.Dimension.Z)
                    {
                        var idx = new Index3(i, j, k);
                        targetView[idx] = sourceView[idx];
                    }
    }

    #endregion
}
}
