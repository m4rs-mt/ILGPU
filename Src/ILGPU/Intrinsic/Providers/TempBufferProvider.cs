// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: TempBufferProvider.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System.Runtime.CompilerServices;

namespace ILGPU.Intrinsic.Providers;

/// <summary>
/// A view to manage access to RNG providers for first warp lanes.
/// </summary>
/// <param name="View">The underlying linear view.</param>
public readonly record struct TempBufferProviderView<T>(ArrayView<T> View) :
    IIntrinsicProviderView<T>
    where T : unmanaged
{
    /// <summary>
    /// Access the i-th element using a specific type.
    /// </summary>
    /// <param name="index">THe element index to use.</param>
    /// <remarks>Note that temporary buffer elements are aligned to 64 bits.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Item(int index) => ref View[index];

    /// <summary>
    /// Access the i-th element using a specific type.
    /// </summary>
    /// <param name="index">THe element index to use.</param>
    /// <remarks>Note that temporary buffer elements are aligned to 64 bits.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Item(long index) => ref View[index];

    /// <summary>
    /// Access the i-th element using a specific type.
    /// </summary>
    /// <typeparam name="TOther">The element type to access.</typeparam>
    /// <param name="index">THe element index to use.</param>
    /// <remarks>Note that temporary buffer elements are aligned to 64 bits.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TOther Item<TOther>(int index) where TOther : unmanaged =>
        ref View.Cast<TOther>()[index];

    /// <summary>
    /// Access the i-th element using a specific type.
    /// </summary>
    /// <typeparam name="TOther">The element type to access.</typeparam>
    /// <param name="index">THe element index to use.</param>
    /// <remarks>Note that temporary buffer elements are aligned to 64 bits.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TOther Item<TOther>(long index) where TOther : unmanaged =>
        ref View.Cast<TOther>()[index];
}

/// <summary>
/// Tags for temp buffers to control initialization semantics.
/// </summary>
public static class TempBufferTags
{
    /// <summary>
    /// Defines an abstract buffer tag contract.
    /// </summary>
    public interface IIsInitializedTempBufferTag
    {
        /// <summary>
        /// Returns true if this buffer will be initialized or not.
        /// </summary>
        public static abstract bool IsInitialized { get; }
    }

    /// <summary>
    /// A buffer tag representing uninitialized buffers.
    /// </summary>
    public readonly struct UninitializedTempBufferTag : IIsInitializedTempBufferTag
    {
        /// <summary>
        /// Returns false to indicate that buffers with this tag will be uninitialized.
        /// </summary>
        public static bool IsInitialized => false;
    }

    /// <summary>
    /// A buffer tag representing zero-initialized buffers.
    /// </summary>
    public readonly struct ZeroInitializedTempBufferTag : IIsInitializedTempBufferTag
    {
        /// <summary>
        /// Returns true to indicate that buffers with this tag will be zero initialized.
        /// </summary>
        public static bool IsInitialized => true;
    }
}

/// <summary>
/// Represents a temporary not initialized buffer that can be requested from within
/// kernels to store temporary data in global memory.
/// </summary>
public class TempBufferProvider<T, TTag>
    : IIntrinsicProvider<T, TempBufferProviderView<T>>
    where T : unmanaged
    where TTag : struct, TempBufferTags.IIsInitializedTempBufferTag
{
    /// <summary>
    /// Returns true.
    /// </summary>
    public static bool ViewCanBeReused => true;

    /// <summary>
    /// Returns true.
    /// </summary>
    public static bool ValueCanBeReused => true;

    /// <summary>
    /// Allocates a simple 1D buffer to hold all RNGs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryBuffer Allocate(AcceleratorStream stream, long maxExtent) =>
        stream.Allocate1D<int>(maxExtent);

    /// <summary>
    /// Performs a nop since temp buffers are not initialized by definition.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Init(AcceleratorStream stream, MemoryBuffer buffer, long extent)
    {
        if (TTag.IsInitialized)
            GetView(buffer, extent).View.MemSetToZero(stream);
    }

    /// <summary>
    /// Returns a new <see cref="TempBufferProviderView{T}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TempBufferProviderView<T> GetView(MemoryBuffer buffer, long extent) =>
        new(buffer.AsArrayView<T>(0L, extent));
}
