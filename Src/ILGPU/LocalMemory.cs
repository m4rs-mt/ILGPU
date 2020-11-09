// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: LocalMemory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.Runtime;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Contains methods to allocate and manage local memory.
    /// </summary>
    public static class LocalMemory
    {
        /// <summary>
        /// A readonly reference to the <see cref="AllocateZero{T}(long)"/> method.
        /// </summary>
        private static readonly MethodInfo AllocateZeroMethod =
            typeof(LocalMemory).GetMethod(
                nameof(AllocateZero),
                BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Creates a typed <see cref="AllocateZero{T}(long)"/> method instance to invoke.
        /// </summary>
        /// <param name="elementType">The array element type.</param>
        /// <returns>The typed method instance.</returns>
        internal static MethodInfo GetAllocateZeroMethod(Type elementType) =>
            AllocateZeroMethod.MakeGenericMethod(elementType);

        /// <summary>
        /// Allocates a chunk of local memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated region of local memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ArrayView<T> AllocateZero<T>(long extent)
            where T : unmanaged
        {
            Trace.Assert(extent >= 0, "Invalid extent");
            var view = Allocate<T>(extent);
            for (long i = 0; i < extent; ++i)
                view[i] = default;
            return view;
        }

        /// <summary>
        /// Allocates a single element in local memory.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <returns>An allocated element in local memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LocalMemoryIntrinsic(LocalMemoryIntrinsicKind.Allocate)]
        public static ArrayView<T> Allocate<T>(long extent)
            where T : unmanaged
        {
            Trace.Assert(extent >= 0, "Invalid extent");
            return new ArrayView<T>(
                UnmanagedMemoryViewSource.Create(
                    Interop.SizeOf<T>() * extent),
                0,
                extent);
        }

        /// <summary>
        /// Allocates a chunk of local memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated region of local memory.</returns>
        public static ArrayView<T, TIndex> Allocate<T, TIndex>(TIndex extent)
            where T : unmanaged
            where TIndex : unmanaged, IIntIndex, IGenericIndex<TIndex>
        {
            var baseView = Allocate<T>((long)extent.Size);
            return new ArrayView<T, TIndex>(baseView, extent);
        }

        /// <summary>
        /// Allocates a 1D chunk of local memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="length">The number of elements to allocate.</param>
        /// <returns>An allocated region of local memory.</returns>
        public static ArrayView<T> Allocate<T>(Index1 length)
            where T : unmanaged =>
            Allocate<T, Index1>(length).AsLinearView();

        /// <summary>
        /// Allocates a 2D chunk of local memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="width">The width of the 2D buffer.</param>
        /// <param name="height">The height of the 2D buffer.</param>
        /// <returns>An allocated region of local memory.</returns>
        public static ArrayView2D<T> Allocate2D<T>(Index1 width, Index1 height)
            where T : unmanaged =>
            Allocate2D<T>(new Index2(width, height));

        /// <summary>
        /// Allocates a 2D chunk of local memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated region of local memory.</returns>
        public static ArrayView2D<T> Allocate2D<T>(Index2 extent)
            where T : unmanaged =>
            Allocate<T, Index2>(extent);

        /// <summary>
        /// Allocates a 3D chunk of local memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="width">The width of the 3D buffer.</param>
        /// <param name="height">The height of the 3D buffer.</param>
        /// <param name="depth">The depth of the 3D buffer.</param>
        /// <returns>An allocated region of local memory.</returns>
        public static ArrayView3D<T> Allocate3D<T>(
            Index1 width,
            Index1 height,
            Index1 depth)
            where T : unmanaged =>
            Allocate3D<T>(new Index3(width, height, depth));

        /// <summary>
        /// Allocates a 3D chunk of local memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated region of local memory.</returns>
        public static ArrayView3D<T> Allocate3D<T>(Index3 extent)
            where T : unmanaged =>
            Allocate<T, Index3>(extent);
    }
}
