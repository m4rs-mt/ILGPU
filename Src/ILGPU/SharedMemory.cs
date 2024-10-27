// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: SharedMemory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;

namespace ILGPU;

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
    [SharedMemoryIntrinsic]
    public static ref T Allocate<T>() where T : unmanaged =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Allocates a chunk of shared memory with the specified number of elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="extent">The extent (number of elements to allocate).</param>
    /// <returns>An allocated region of shared memory.</returns>
    [SharedMemoryIntrinsic]
    public static ArrayView<T> Allocate<T>(int extent) where T : unmanaged =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Gets a chunk of dynamically allocated shared memory as typed memory view
    /// with the element type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>A view to a dynamically allocated region of shared memory.</returns>
    [SharedMemoryIntrinsic]
    public static ArrayView<T> GetDynamic<T>() where T : unmanaged =>
        throw new InvalidKernelOperationException();
}
