// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: SharedMemory.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.Runtime.CPU;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Containts methods to allocate and managed shared memory.
    /// </summary>
    public static class SharedMemory
    {
        /// <summary>
        /// Allocates a single element in shared memory.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <returns>An allocated element in shared memory.</returns>
        [SharedMemoryIntrinsic(SharedMemoryIntrinsicKind.AllocateElement)]
        public static ref T Allocate<T>()
            where T : struct
        {
            return ref Allocate<T>(1)[0];
        }

        /// <summary>
        /// Allocates a chunk of shared memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated region of shared memory.</returns>
        [SharedMemoryIntrinsic(SharedMemoryIntrinsicKind.Allocate)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView<T> Allocate<T>(int extent)
            where T : struct
        {
            return CPURuntimeGroupContext.Current.AllocateSharedMemory<T>(extent);
        }

        /// <summary>
        /// Allocates a chunk of shared memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated region of shared memory.</returns>
        public static ArrayView<T, TIndex> Allocate<T, TIndex>(TIndex extent)
            where T : struct
            where TIndex : struct, IIndex, IGenericIndex<TIndex>
        {
            var baseView = Allocate<T>(extent.Size);
            return new ArrayView<T, TIndex>(baseView, extent);
        }

        /// <summary>
        /// Allocates a 1D chunk of shared memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="length">The number of elements to allocate.</param>
        /// <returns>An allocated region of shared memory.</returns>
        public static ArrayView<T> Allocate<T>(Index length)
            where T : struct
        {
            return Allocate<T, Index>(length).AsLinearView();
        }

        /// <summary>
        /// Allocates a 2D chunk of shared memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="width">The width of the 2D buffer.</param>
        /// <param name="height">The height of the 2D buffer.</param>
        /// <returns>An allocated region of shared memory.</returns>
        public static ArrayView2D<T> Allocate2D<T>(Index width, Index height)
            where T : struct
        {
            return Allocate2D<T>(new Index2(width, height));
        }

        /// <summary>
        /// Allocates a 2D chunk of shared memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated region of shared memory.</returns>
        public static ArrayView2D<T> Allocate2D<T>(Index2 extent)
            where T : struct
        {
            return new ArrayView2D<T>(Allocate<T, Index2>(extent));
        }

        /// <summary>
        /// Allocates a 3D chunk of shared memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="width">The width of the 3D buffer.</param>
        /// <param name="height">The height of the 3D buffer.</param>
        /// <param name="depth">The depth of the 3D buffer.</param>
        /// <returns>An allocated region of shared memory.</returns>
        public static ArrayView3D<T> Allocate3D<T>(Index width, Index height, Index depth)
            where T : struct
        {
            return Allocate3D<T>(new Index3(width, height, depth));
        }

        /// <summary>
        /// Allocates a 3D chunk of shared memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated region of shared memory.</returns>
        public static ArrayView3D<T> Allocate3D<T>(Index3 extent)
            where T : struct
        {
            return new ArrayView3D<T>(Allocate<T, Index3>(extent));
        }
    }
}
