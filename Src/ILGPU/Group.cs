// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Group.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using ILGPU.Runtime;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU;

/// <summary>
/// Contains general grid functions.
/// </summary>
public static partial class Group
{
    #region Properties

    /// <summary>
    /// Returns the linear index withing the scheduled thread group.
    /// </summary>
    /// <returns>The linear group index.</returns>
    public static int Index
    {
        [GroupIntrinsic]
        get => throw new InvalidKernelOperationException();
    }

    /// <summary>
    /// Returns the dimension of the number of threads per group per grid element
    /// in the scheduled thread grid.
    /// </summary>
    /// <returns>The thread dimension for a single group.</returns>
    public static int Dimension
    {
        [GroupIntrinsic]
        get => throw new InvalidKernelOperationException();
    }

    /// <summary>
    /// Returns true if the current thread is the first in the group.
    /// </summary>
    public static bool IsFirstThread => Index == 0;

    /// <summary>
    /// Returns true if the current thread is the last in the group.
    /// </summary>
    public static bool IsLastThread => Index == Dimension - 1;

    /// <summary>
    /// Returns a thread bit mask including all threads in this group.
    /// </summary>
    public static int Mask => Dimension - 1;

    #endregion

    #region Barriers

    /// <summary>
    /// Executes a thread barrier.
    /// </summary>
    [GroupIntrinsic]
    public static void Barrier() => throw new InvalidKernelOperationException();

    /// <summary>
    /// Executes a thread barrier and returns the number of threads for which
    /// the predicate evaluated to true.
    /// </summary>
    /// <param name="predicate">The predicate to check.</param>
    /// <returns>
    /// The number of threads for which the predicate evaluated to true.
    /// </returns>
    [GroupIntrinsic]
    public static int BarrierPopCount(bool predicate) =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Executes a thread barrier and returns true if all threads in a block
    /// fulfills the predicate.
    /// </summary>
    /// <param name="predicate">The predicate to check.</param>
    /// <returns>True, if all threads in a block fulfills the predicate.</returns>
    [GroupIntrinsic]
    public static bool BarrierAnd(bool predicate) =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Executes a thread barrier and returns true if any thread in a block
    /// fulfills the predicate.
    /// </summary>
    /// <param name="predicate">The predicate to check.</param>
    /// <returns>True, if any thread in a block fulfills the predicate.</returns>
    [GroupIntrinsic]
    public static bool BarrierOr(bool predicate) =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Broadcast

    /// <summary>
    /// Performs a broadcast operation that broadcasts the given value from the first
    /// thread to all other threads in the group.
    /// </summary>
    /// <param name="value">The value to broadcast.</param>
    /// <remarks>
    /// Note that the group index must be the same for all threads in the group.
    /// </remarks>
    [GroupIntrinsic]
    public static T Broadcast<T>(FirstThreadValue<T> value) where T : unmanaged =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Performs a broadcast operation that broadcasts the given value from the last
    /// thread to all other threads in the group.
    /// </summary>
    /// <param name="value">The value to broadcast.</param>
    /// <remarks>
    /// Note that the group index must be the same for all threads in the group.
    /// </remarks>
    [GroupIntrinsic]
    public static T Broadcast<T>(LastThreadValue<T> value) where T : unmanaged =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Performs a broadcast operation that broadcasts the given value from the specified
    /// thread to all other threads in the group.
    /// </summary>
    /// <param name="value">The value to broadcast.</param>
    /// <param name="threadIndex">The thread index within the group to read from.</param>
    /// <remarks>
    /// Note that the group index must be the same for all threads in the group.
    /// </remarks>
    [GroupIntrinsic]
    public static T Broadcast<T>(T value, int threadIndex)
        where T : unmanaged =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Shared Memory

    /// <summary>
    /// Allocates a single element in shared memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>An allocated element in shared memory.</returns>
    [GroupIntrinsic]
    public static ref T GetSharedMemory<T>() where T : unmanaged =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Allocates a chunk of shared memory with the specified number of elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="extent">The extent (number of elements to allocate).</param>
    /// <returns>An allocated region of shared memory.</returns>
    [GroupIntrinsic]
    public static ArrayView<T> GetSharedMemory<T>(int extent) where T : unmanaged =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Allocates a chunk of shared memory with the specified number of elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="extent">The extent (number of elements to allocate).</param>
    /// <returns>An allocated region of shared memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayView2D<T, TStride> GetSharedMemory2D<T, TStride>(Index2D extent)
        where T : unmanaged
        where TStride : struct, IStride2D<TStride>
    {
        var stride = TStride.FromExtent(extent);
        int totalLength = stride.ComputeBufferLength(extent);
        var localPerThread = GetSharedMemory<T>(totalLength);
        return localPerThread.AsDense().As2DView(extent, stride);
    }

    /// <summary>
    /// Allocates a chunk of shared memory with the specified number of elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="extent">The extent (number of elements to allocate).</param>
    /// <returns>An allocated region of shared memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayView3D<T, TStride> GetSharedMemory3D<T, TStride>(Index3D extent)
        where T : unmanaged
        where TStride : struct, IStride3D<TStride>
    {
        var stride = TStride.FromExtent(extent);
        int totalLength = stride.ComputeBufferLength(extent);
        var localPerThread = GetSharedMemory<T>(totalLength);
        return localPerThread.AsDense().As3DView(extent, stride);
    }

    /// <summary>
    /// Allocates a single element of the given element type for each thread in the
    /// current group.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>
    /// An allocated region of shared memory holding a single element per thread.
    /// </returns>
    [GroupIntrinsic]
    public static ArrayView<T> GetSharedMemoryPerThread<T>() where T : unmanaged =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Allocates a set of elements of the given element type for each thread in the
    /// current group.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="multiplier">The multiplier to use per element.</param>
    /// <returns>
    /// An allocated region of shared memory holding a set of elements per thread.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayView<T> GetSharedMemoryPerThread<T>(int multiplier)
        where T : unmanaged
    {
        Trace.Assert(multiplier > 0, "Invalid shared memory multiplier");
        return GetSharedMemoryPerThreadMultiplier<T>(multiplier);
    }

    /// <summary>
    /// Allocates a set of elements of the given element type for each thread in the
    /// current group.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="multiplier">The multiplier to use per element.</param>
    /// <returns>
    /// An allocated region of shared memory holding a set of elements per thread.
    /// </returns>
    [GroupIntrinsic]
    private static ArrayView<T> GetSharedMemoryPerThreadMultiplier<T>(int multiplier)
        where T : unmanaged =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Allocates a single element of the given element type for each warp in the
    /// current group.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>
    /// An allocated region of shared memory holding a single element per warp.
    /// </returns>
    [GroupIntrinsic]
    public static ArrayView<T> GetSharedMemoryPerWarp<T>() where T : unmanaged =>
        throw new InvalidKernelOperationException();

    #endregion

    #region Local Memory

    /// <summary>
    /// Allocates a sequence of elements in local memory per thread.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="extent">The extent (number of elements to allocate).</param>
    /// <returns>
    /// An allocated region of local memory for each thread in the group.
    /// </returns>
    [GroupIntrinsic]
    public static ArrayView<T> GetLocalMemoryPerThread<T>(int extent)
        where T : unmanaged =>
        throw new InvalidKernelOperationException();

    /// <summary>
    /// Allocates a 2D sequence of elements in local memory per thread.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The stride type.</typeparam>
    /// <param name="extent">The extent (number of elements to allocate).</param>
    /// <returns>
    /// An allocated region of local memory for each thread in the group.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayView2D<T, TStride> GetLocalMemoryPerThread2D<T, TStride>(
        Index2D extent)
        where T : unmanaged
        where TStride : struct, IStride2D<TStride>
    {
        var stride = TStride.FromExtent(extent);
        int totalLength = stride.ComputeBufferLength(extent);
        var localPerThread = GetLocalMemoryPerThread<T>(totalLength);
        return localPerThread.AsDense().As2DView(extent, stride);
    }

    /// <summary>
    /// Allocates a 3D sequence of elements in local memory per thread.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The stride type.</typeparam>
    /// <param name="extent">The extent (number of elements to allocate).</param>
    /// <returns>
    /// An allocated region of local memory for each thread in the group.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayView3D<T, TStride> GetLocalMemoryPerThread3D<T, TStride>(
        Index3D extent)
        where T : unmanaged
        where TStride : struct, IStride3D<TStride>
    {
        var stride = TStride.FromExtent(extent);
        int totalLength = stride.ComputeBufferLength(extent);
        var localPerThread = GetLocalMemoryPerThread<T>(totalLength);
        return localPerThread.AsDense().As3DView(extent, stride);
    }

    #endregion

    #region Memory Fence

    /// <summary>
    /// A memory fence at the group level.
    /// </summary>
    [GroupIntrinsic]
    public static void MemoryFence() => throw new InvalidKernelOperationException();

    #endregion
}
