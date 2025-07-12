// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelConfig.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILGPU;

/// <summary>
/// A single kernel configuration for an explicitly grouped kernel in int32 space.
/// </summary>
/// <param name="Dimension">The overall kernel dimension.</param>
/// <param name="SharedMemoryBytes">
/// The optional number of bytes to dynamically allocate in shared memory.
/// </param>
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public readonly record struct KernelConfig(
    KernelSize Dimension,
    int SharedMemoryBytes = 0)
{
    #region Instance

    /// <summary>
    /// Constructs a new kernel configuration.
    /// </summary>
    /// <param name="gridDim">The grid dimension to use.</param>
    /// <param name="groupDim">The group dimension to use.</param>
    /// <param name="sharedMemoryBytes">
    /// The dynamic shared memory configuration.
    /// </param>
    public KernelConfig(
        Index1D gridDim,
        Index1D groupDim,
        int sharedMemoryBytes = 0)
        : this(new(gridDim, groupDim), sharedMemoryBytes)
    { }

    /// <summary>
    /// Constructs a new kernel configuration.
    /// </summary>
    /// <param name="gridDim">The grid dimension to use.</param>
    /// <param name="groupDim">The group dimension to use.</param>
    /// <param name="sharedMemoryBytes">
    /// The dynamic shared memory configuration.
    /// </param>
    public KernelConfig(
        Index2D gridDim,
        Index2D groupDim,
        int sharedMemoryBytes = 0)
        : this(gridDim.LongSize, groupDim.Size, sharedMemoryBytes)
    { }

    /// <summary>
    /// Constructs a new kernel configuration.
    /// </summary>
    /// <param name="gridDim">The grid dimension to use.</param>
    /// <param name="groupDim">The group dimension to use.</param>
    /// <param name="sharedMemoryBytes">
    /// The dynamic shared memory configuration.
    /// </param>
    public KernelConfig(
        Index3D gridDim,
        Index3D groupDim,
        int sharedMemoryBytes = 0)
        : this(gridDim.LongSize, groupDim.Size, sharedMemoryBytes)
    { }

    /// <summary>
    /// Constructs a new kernel configuration.
    /// </summary>
    /// <param name="gridDim">The grid dimension to use.</param>
    /// <param name="groupDim">The group dimension to use.</param>
    /// <param name="sharedMemoryBytes">
    /// The dynamic shared memory configuration.
    /// </param>
    public KernelConfig(
        LongIndex1D gridDim,
        Index1D groupDim,
        int sharedMemoryBytes = 0)
        : this(new(gridDim, groupDim), sharedMemoryBytes)
    { }

    /// <summary>
    /// Constructs a new kernel configuration.
    /// </summary>
    /// <param name="gridDim">The grid dimension to use.</param>
    /// <param name="groupDim">The group dimension to use.</param>
    /// <param name="sharedMemoryBytes">
    /// The dynamic shared memory configuration.
    /// </param>
    public KernelConfig(
        LongIndex2D gridDim,
        Index2D groupDim,
        int sharedMemoryBytes = 0)
        : this(gridDim.Size, groupDim.Size, sharedMemoryBytes)
    { }

    /// <summary>
    /// Constructs a new kernel configuration.
    /// </summary>
    /// <param name="gridDim">The grid dimension to use.</param>
    /// <param name="groupDim">The group dimension to use.</param>
    /// <param name="sharedMemoryBytes">
    /// The dynamic shared memory configuration.
    /// </param>
    public KernelConfig(
        LongIndex3D gridDim,
        Index3D groupDim,
        int sharedMemoryBytes = 0)
        : this(gridDim.Size, groupDim.Size, sharedMemoryBytes)
    { }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the global grid dimension.
    /// </summary>
    public long GridSize => Dimension.GridSize;

    /// <summary>
    /// Returns the global group dimension of each group.
    /// </summary>
    public int GroupSize => Dimension.GroupSize;

    /// <summary>
    /// Returns true if the current configuration uses dynamic shared memory.
    /// </summary>
    public bool NeedsAutomaticGrouping => GroupSize < 1;

    /// <summary>
    /// Returns true if the current configuration uses dynamic shared memory.
    /// </summary>
    public bool UsesSharedMemory => SharedMemoryBytes > 0;

    /// <summary>
    /// Returns true if this configuration is a valid launch configuration.
    /// </summary>
    public bool IsValid => Bitwise.And(GridSize > 0, GroupSize >= 0);

    /// <summary>
    /// Returns the total launch size in terms of number of threads.
    /// </summary>
    public long NumThreads => Dimension.NumThreads;

    #endregion

    #region Methods

    /// <summary>
    /// Specializes grid launch size.
    /// </summary>
    /// <param name="gridSize">Updated grid size information.</param>
    /// <returns>Specialized launch dimensions for actual kernel dispatch.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KernelConfig WithGridSize(long gridSize) =>
        this with { Dimension = Dimension with { GridSize = gridSize } };

    /// <summary>
    /// Specializes group launch size.
    /// </summary>
    /// <param name="groupSize">Updated group size information.</param>
    /// <returns>Specialized launch dimensions for actual kernel dispatch.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KernelConfig WithGroupSize(int groupSize) =>
        this with { Dimension = Dimension with { GroupSize = groupSize } };

    /// <summary>
    /// Specializes launch dimensions.
    /// </summary>
    /// <param name="autoGroupSize">Automatic group size information.</param>
    /// <returns>Specialized launch dimensions for actual kernel dispatch.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KernelConfig WithAutoGroupSize(int autoGroupSize)
    {
        var dimension = NeedsAutomaticGrouping
            ? new(XMath.DivRoundUp(GridSize, autoGroupSize), autoGroupSize)
            : Dimension;
        return new(dimension, SharedMemoryBytes);
    }

    /// <summary>
    /// Specifies shared memory to be used on top of statically allocated shared memory.
    /// </summary>
    /// <param name="bytesInSharedMemory">The number of bytes to allocate.</param>
    /// <returns>The updated kernel configuration.</returns>
    /// <remarks>
    /// <paramref name="bytesInSharedMemory"/> can be less or equal to zero. In this case
    /// the amount of shared memory will not be changed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KernelConfig WithSharedMemory(int bytesInSharedMemory)
    {
        bytesInSharedMemory = Math.Max(bytesInSharedMemory, 0);
        return new(Dimension, SharedMemoryBytes + bytesInSharedMemory);
    }

    /// <summary>
    /// Specifies shared memory to be used on top of statically allocated shared memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="length">The number of elements to allocate.</param>
    /// <returns>The updated kernel configuration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public KernelConfig WithSharedMemory<T>(int length) where T : unmanaged =>
        WithSharedMemory(Interop.SizeOf<T>() * length);

    /// <summary>
    /// Implements a specialized hash code version for kernel configs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() =>
        HashCode.Combine(Dimension.GetHashCode(), SharedMemoryBytes);

    /// <summary>
    /// Converts the current instance into a dimension tuple.
    /// </summary>
    /// <returns>A dimension tuple representing this kernel configuration.</returns>
    public (LongIndex1D, LongIndex1D) ToDimensions() => (GridSize, GroupSize);

    /// <summary>
    /// Converts the current instance into a value tuple.
    /// </summary>
    /// <returns>A value tuple representing this kernel configuration.</returns>
    public (LongIndex1D, LongIndex1D, int) ToValueTuple() =>
        (GridSize, GroupSize, SharedMemoryBytes);

    /// <summary>
    /// Deconstructs the current instance into a dimension tuple.
    /// </summary>
    /// <param name="gridDim">The grid dimension.</param>
    /// <param name="groupDim">The group dimension.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out LongIndex1D gridDim, out LongIndex1D groupDim)
    {
        gridDim = GridSize;
        groupDim = GroupSize;
    }

    /// <summary>
    /// Deconstructs the current instance into a value tuple.
    /// </summary>
    /// <param name="gridDim">The grid dimension.</param>
    /// <param name="groupDim">The group dimension.</param>
    /// <param name="sharedMemoryBytes">
    /// Bytes explicitly allocated in shared memory
    /// .</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(
        out LongIndex1D gridDim,
        out LongIndex1D groupDim,
        out int sharedMemoryBytes)
    {
        gridDim = GridSize;
        groupDim = GroupSize;
        sharedMemoryBytes = SharedMemoryBytes;
    }

    #endregion

    #region Operators

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig((Index1D, Index1D) dimensions) =>
        new(dimensions.Item1, dimensions.Item2);

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig((Index3D, Index1D) dimensions) =>
        new(dimensions.Item1, new Index3D(dimensions.Item2, 1, 1));

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig((Index3D, Index2D) dimensions) =>
        new(dimensions.Item1, new Index3D(dimensions.Item2, 1));

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig((Index2D, Index2D) dimensions) =>
        new(dimensions.Item1, dimensions.Item2);

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig((Index3D, Index3D) dimensions) =>
        new(dimensions.Item1, dimensions.Item2);

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig(
        (Index1D, Index1D, int) dimensions) =>
        new(dimensions.Item1, dimensions.Item2, dimensions.Item3);

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig(
        (Index2D, Index2D, int) dimensions) =>
        new(dimensions.Item1, dimensions.Item2, dimensions.Item3);

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig(
        (Index3D, Index3D, int) dimensions) =>
        new(dimensions.Item1, dimensions.Item2, dimensions.Item3);

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig((LongIndex1D, Index1D) dimensions) =>
        new(dimensions.Item1, dimensions.Item2);

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig((LongIndex3D, Index1D) dimensions) =>
        new(dimensions.Item1, new Index3D((int)dimensions.Item2, 1, 1));

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig((LongIndex3D, Index2D) dimensions) =>
        new(dimensions.Item1, new Index3D(dimensions.Item2, 1));

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig((LongIndex2D, Index2D) dimensions) =>
        new(dimensions.Item1, dimensions.Item2);

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig((LongIndex3D, Index3D) dimensions) =>
        new(dimensions.Item1, dimensions.Item2);

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig(
        (LongIndex1D, Index1D, int) dimensions) =>
        new(dimensions.Item1, dimensions.Item2, dimensions.Item3);

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig(
        (LongIndex2D, Index2D, int) dimensions) =>
        new(dimensions.Item1, dimensions.Item2, dimensions.Item3);

    /// <summary>
    /// Converts the given dimension tuple into an equivalent kernel configuration.
    /// </summary>
    /// <param name="dimensions">The kernel dimensions.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KernelConfig(
        (LongIndex3D, Index3D, int) dimensions) =>
        new(dimensions.Item1, dimensions.Item2, dimensions.Item3);

    /// <summary>
    /// Converts the given kernel configuration into an equivalent dimension tuple.
    /// </summary>
    /// <param name="config">The kernel configuration to convert.</param>
    public static implicit operator (LongIndex1D, LongIndex1D)(KernelConfig config) =>
        config.ToDimensions();

    /// <summary>
    /// Converts the given kernel configuration into an equivalent value tuple.
    /// </summary>
    /// <param name="config">The kernel configuration to convert.</param>
    public static implicit operator (LongIndex1D, LongIndex1D, int)(
        KernelConfig config) =>
        config.ToValueTuple();

    #endregion
}
