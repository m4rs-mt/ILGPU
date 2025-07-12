// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Permutor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.RadixSort;
using ILGPU.Random;
using ILGPU.Runtime;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Permutation;


/// <summary>
/// Contains extension methods for permutation operations.
/// </summary>
public static class Permutor
{
    #region Memory Management

    /// <summary>
    /// Adds a new buffer for permutation operations.
    /// </summary>
    /// <param name="allocationBuilder">The current allocation builder.</param>
    /// <param name="elementSize">The size of a single element.</param>
    /// <param name="length">The data length.</param>
    [NotInsideKernel]
    public static void AddPermutationBuffer(
       this AllocationBuilder allocationBuilder,
       int elementSize,
       long length) =>
        allocationBuilder.AddBuffer(elementSize, length);

    /// <summary>
    /// Adds a new buffer for permutation operations.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="allocationBuilder">The current allocation builder.</param>
    /// <param name="length">The data length.</param>
    [NotInsideKernel]
    public static void AddPermutationBuffer<T>(
       this AllocationBuilder allocationBuilder,
       long length)
       where T : unmanaged =>
       allocationBuilder.AddPermutationBuffer(Interop.SizeOf<T>(), length);

    #endregion

    #region Entry Points

    /// <summary>
    /// Permutes all elements in a given buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRandomProvider">The random number provider type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The elements to permute.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [NotInsideKernel]
    public static void Permute<T, TRandomProvider>(
        this AcceleratorStream stream,
        ArrayView<T> view)
        where T : unmanaged
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider> =>
        stream.Permute<T, Stride1D.Dense, TRandomProvider>(view.AsDense());

    /// <summary>
    /// Permutes all elements in a given buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The stride of all values.</typeparam>
    /// <typeparam name="TRandomProvider">The random number provider type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The elements to permute.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel]
    public static void Permute<T, TStride, TRandomProvider>(
        this AcceleratorStream stream,
        ArrayView1D<T, TStride> view)
        where T : unmanaged
        where TStride : struct, IStride1D
        where TRandomProvider : unmanaged, IRandomProvider<TRandomProvider>
    {
        if (view.Length < 1)
            throw new ArgumentOutOfRangeException(nameof(view));

        // Determines a new unique group-wide RNG value to be used during permutation
        int GetRNGValue()
        {
            using var rngProvider = Group.GetRandom<TRandomProvider>();
            int rngValue = rngProvider.NextGroupThreadUnique();
            return Group.RadixSort<int, AscendingInt32>(rngValue);
        }

        // First pass of the permutation algorithm.
        void PermutePass1<TInputStride>(
            ArrayView1D<T, TInputStride> input)
            where TInputStride : struct, IStride1D
        {
            var elements = Group.GetSharedMemoryPerThread<T>();
            elements[Group.Index] = view[Grid.GlobalThreadIndex];
            int rngValue = GetRNGValue();
            Group.Barrier();

            input[Grid.GlobalThreadIndex] = elements[rngValue & Group.Mask];
        }

        // Second pass of the permutation algorithm shuffling data around.
        void PermutePass2<TInputStride, TOutputStride>(
            ArrayView1D<T, TInputStride> input,
            ArrayView1D<T, TOutputStride> output,
            int blockSize)
            where TInputStride : struct, IStride1D
            where TOutputStride : struct, IStride1D
        {
            int rngValue = GetRNGValue();

            for (int i = 0; i < blockSize; i++)
            {
                var targetIdx = (rngValue & Group.Mask) + Group.Dimension * Grid.Index;
                var sourceIdx = targetIdx * blockSize + i;
                output[
                    i
                    + Group.Index * blockSize * Grid.Dimension
                    + blockSize * Grid.Index] = input[sourceIdx];
            }
        }

        var kernelConfig = stream.ComputeKernelConfig(view.Length);
        int numWarpsPerGroup = stream.NumWarpsPerOptimalGroupSize;

        var numPass2Groups = kernelConfig.GridSize < stream.WarpSize ?
            kernelConfig.GridSize :
            numWarpsPerGroup * kernelConfig.GridSize / kernelConfig.GroupSize;
        var blockSize = kernelConfig.GridSize < stream.WarpSize ? 1 : stream.WarpSize;

        // Setup pass configurations and temp buffer
        var pass1Config = kernelConfig;
        var pass2Config = kernelConfig.WithGridSize(numPass2Groups);
        using var tempBuffer = stream.AllocateTemporary<T>(view.Length);
        var tempBufferView = tempBuffer.View.AsDense();

        // Perform first permutation round to use intrinsic provider data
        stream.Launch(pass1Config, _ => PermutePass1(view));
        stream.Launch(pass2Config, _ => PermutePass2(view, tempBufferView, blockSize));

        // Perform second permutation round to use intrinsic provider data
        stream.Launch(pass1Config, _ => PermutePass1(tempBufferView));
        stream.Launch(pass2Config, _ => PermutePass2(tempBufferView, view, blockSize));
    }

    #endregion
}
