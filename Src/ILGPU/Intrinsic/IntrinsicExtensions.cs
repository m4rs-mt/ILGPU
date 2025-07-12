// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: IntrinsicExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using ILGPU.Intrinsic.Providers;
using ILGPU.Random;
using System.Runtime.CompilerServices;

namespace ILGPU;

partial class Grid
{
    /// <summary>
    /// Gets a zero-initialized temporary variable in global device memory. The variable
    /// is initialized before each kernel launch to zero and there is exactly one
    /// variable per thread grid (per kernel launch).
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>
    /// A reference to exactly one initialized global variable per grid.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetInitializedTemporaryValue<T>() where T : unmanaged
    {
        var view = IntrinsicProvider.Provide<
            T,
            TempBufferProviderView<T>,
            TempBufferProvider<T, TempBufferTags.ZeroInitializedTempBufferTag>,
            IntrinsicProviderKinds.ForEachKernel>();
        return ref view.Item(0);
    }

    /// <summary>
    /// Gets a zero-initialized array in global device memory. The array is initialized
    /// before each kernel launch to zero and there is exactly one variable per thread.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>
    /// An array view containing exactly one element per thread in a group.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayView<T> GetInitializedTemporaryValuePerThread<T>()
        where T : unmanaged
    {
        var view = IntrinsicProvider.Provide<
            T,
            TempBufferProviderView<T>,
            TempBufferProvider<T, TempBufferTags.ZeroInitializedTempBufferTag>,
            IntrinsicProviderKinds.ForEachThread>();
        return view.View;
    }

    /// <summary>
    /// Gets a non initialized array in global device memory. The array is never
    /// initialized and main contain data from other kernel launches.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>
    /// An array view containing exactly one element per thread in a group.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayView<T> GetUnInitializedTemporaryValuePerThread<T>()
        where T : unmanaged
    {
        var view = IntrinsicProvider.Provide<
            T,
            TempBufferProviderView<T>,
            TempBufferProvider<T, TempBufferTags.UninitializedTempBufferTag>,
            IntrinsicProviderKinds.ForEachThread>();
        return view.View;
    }
}

partial class Group
{
    /// <summary>
    /// Gets a zero-initialized temporary variable in global device memory. The variable
    /// is initialized before each kernel launch to zero and there is exactly one
    /// variable per thread group.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>
    /// A reference to exactly one initialized global variable per group.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FirstThreadRef<T> GetInitializedTemporaryValue<T>() where T : unmanaged
    {
        var view = IntrinsicProvider.Provide<
            T,
            TempBufferProviderView<T>,
            TempBufferProvider<T, TempBufferTags.ZeroInitializedTempBufferTag>,
            IntrinsicProviderKinds.ForEachGroup>();
        return new(ref view.Item(Grid.Index));
    }

    /// <summary>
    /// Gets a zero-initialized array in global device memory. The array is initialized
    /// before each kernel launch to zero and there is exactly one variable per thread.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>
    /// An array view containing exactly one element per thread in a group.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayView<T> GetInitializedTemporaryValuePerThread<T>()
        where T : unmanaged
    {
        var view = IntrinsicProvider.Provide<
            T,
            TempBufferProviderView<T>,
            TempBufferProvider<T, TempBufferTags.ZeroInitializedTempBufferTag>,
            IntrinsicProviderKinds.ForEachThreadPerGroup>();
        return view.View;
    }

    /// <summary>
    /// Gets a non initialized array in global device memory. The array is never
    /// initialized and main contain data from other kernel launches.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>
    /// An array view containing exactly one element per thread in a group.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayView<T> GetUnInitializedTemporaryValuePerThread<T>()
        where T : unmanaged
    {
        var view = IntrinsicProvider.Provide<
            T,
            TempBufferProviderView<T>,
            TempBufferProvider<T, TempBufferTags.UninitializedTempBufferTag>,
            IntrinsicProviderKinds.ForEachThreadPerGroup>();
        return view.View;
    }
}

partial class Warp
{
    /// <summary>
    /// Gets a temporary random provider view for processing in each first lane.
    /// </summary>
    /// <typeparam name="TRandomProvider">The random provider type.</typeparam>
    /// <returns>A view to initialized RNG providers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static RandomProviderView<TRandomProvider> GetRandomProviderView<
        TRandomProvider>()
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider> =>
        IntrinsicProvider.Provide<
            TRandomProvider,
            RandomProviderView<TRandomProvider>,
            RandomProvider<TRandomProvider>,
            IntrinsicProviderKinds.ForEachWarp>();
}
