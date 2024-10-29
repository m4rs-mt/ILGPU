// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: RandomProvider.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Random;
using ILGPU.Runtime;
using ILGPU.Runtime.Extensions;

namespace ILGPU.Intrinsic;

/// <summary>
/// A view to manage access to RNG providers for first warp lanes.
/// </summary>
/// <typeparam name="TRandom">The RNG type.</typeparam>
/// <param name="View">The underlying linear view.</param>
readonly record struct FirstLaneRandomProviderView<TRandom>(
    ArrayView<FirstLaneValue<TRandom>> View) :
    IIntrinsicProviderView<FirstLaneValue<TRandom>>
    where TRandom : unmanaged, IRandomProvider
{
    /// <summary>
    /// Access the i-th RNG value in this view.
    /// </summary>
    public ref FirstLaneValue<TRandom> this[long index] => ref View[index];

    /// <summary>
    /// Copies data from the CPU into this view.
    /// </summary>
    public void CopyFrom(AcceleratorStream stream, TRandom[] random) =>
        View.Cast<TRandom>().CopyFromCPU(stream, random);
}

sealed class FirstLaneRandomProvider<TRandom> :
    IIntrinsicProvider<FirstLaneValue<TRandom>, FirstLaneRandomProviderView<TRandom>>
    where TRandom : unmanaged, IRandomProvider<TRandom>
{
    /// <summary>
    /// Returns the for each first thread in warp.
    /// </summary>
    public static IntrinsicProviderKind ProviderKind =>
        IntrinsicProviderKind.ForEachFirstLane;

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
    public static MemoryBuffer Allocate(AcceleratorStream stream, long maxExtent) =>
        stream.Accelerator.Allocate1D<FirstLaneValue<TRandom>>(maxExtent);

    /// <summary>
    /// Initializes the given buffer using the <see cref="IRandomExtension"/> extension
    /// of the accelerator to initialize data.
    /// </summary>
    public static void Init(AcceleratorStream stream, MemoryBuffer buffer, long extent)
    {
        // Get random extension and initialize random number generators
        var randomExtension = stream.GetExtension<IRandomExtension>();
        var tempTarget = new TRandom[extent];
        randomExtension.Generate<TRandom>(tempTarget);

        // Copy data to initialize random number generators
        GetView(buffer, extent).CopyFrom(stream, tempTarget);
    }

    /// <summary>
    /// Returns a new <see cref="FirstLaneRandomProviderView{TRandom}"/>.
    /// </summary>
    public static FirstLaneRandomProviderView<TRandom> GetView(
        MemoryBuffer buffer,
        long extent)
    {
        var baseView = buffer.AsArrayView<FirstLaneValue<TRandom>>(
            0L,
            extent * Interop.SizeOf<FirstLaneValue<TRandom>>());
        return new(baseView);
    }
}
