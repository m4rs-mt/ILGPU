// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.Runtime.CPU;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// Contains methods to allocate and managed shared memory.
    /// </summary>
    public static partial class SharedMemory
    {
        /// <summary>
        /// Allocates a single element in shared memory.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <returns>An allocated element in shared memory.</returns>
        [SharedMemoryIntrinsic(SharedMemoryIntrinsicKind.AllocateElement)]
        public static ref T Allocate<T>()
            where T : unmanaged =>
            ref Allocate<T>(1)[0];

        /// <summary>
        /// Allocates a chunk of shared memory with the specified number of elements.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="extent">The extent (number of elements to allocate).</param>
        /// <returns>An allocated region of shared memory.</returns>
        [SharedMemoryIntrinsic(SharedMemoryIntrinsicKind.Allocate)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView<T> Allocate<T>(int extent)
            where T : unmanaged =>
            CPURuntimeGroupContext.Current.AllocateSharedMemory<T>(extent);

        /// <summary>
        /// Gets a chunk of dynamically allocated shared memory as typed memory view
        /// with the element type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <returns>A view to a dynamically allocated region of shared memory.</returns>
        [SharedMemoryIntrinsic(SharedMemoryIntrinsicKind.AllocateDynamic)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayView<T> GetDynamic<T>()
            where T : unmanaged =>
            CPURuntimeGroupContext.Current.AllocateSharedMemoryDynamic<T>();
    }
}
