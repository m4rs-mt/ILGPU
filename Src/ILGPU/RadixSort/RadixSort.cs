// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: RadixSort.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using ILGPU.Initialization;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.ScanReduce;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.RadixSort;

/// <summary>
/// Implements radix sort extensions for accelerators.
/// </summary>
public static class RadixSorter
{
    #region Memory Buffers

    /// <summary>
    /// Adds a new buffer for scan operations.
    /// </summary>
    /// <typeparam name="TSpecialization">The specialization to use.</typeparam>
    /// <param name="allocationBuilder">The current allocation builder.</param>
    /// <param name="elementSize">The size of a single element.</param>
    /// <param name="length">The data length.</param>
    [NotInsideKernel]
    public static void AddRadixSortBuffer<TSpecialization>(
        this AllocationBuilder allocationBuilder,
        int elementSize,
        long length)
        where TSpecialization : struct, IRadixSortSpecialization
    {
        if (elementSize < 1)
            throw new ArgumentOutOfRangeException(nameof(elementSize));
        if (length < 1 || length > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(length));

        // Allocate temp buffer for all elements
        allocationBuilder.AddBuffer(elementSize, length);

        // Add temp buffer for intermediate data
        var kernelConfig = allocationBuilder.Stream.ComputeGridStrideKernelConfig(
            length,
            out int numIterationsPerGroup);
        long numVirtualGroups = kernelConfig.GridSize * numIterationsPerGroup;
        allocationBuilder.AddBuffer<uint>(
            numVirtualGroups * TSpecialization.UnrollFactor * 2);

        // Allocate scan memory
        allocationBuilder.AddScanBuffer(elementSize, length);
    }

    /// <summary>
    /// Adds a new buffer for radix sort operations.
    /// </summary>
    /// <typeparam name="T">The element type to sort.</typeparam>
    /// <typeparam name="TSpecialization">The specialization to use.</typeparam>
    /// <param name="allocationBuilder">The current allocation builder.</param>
    /// <param name="length">The data length.</param>
    [NotInsideKernel]
    public static void AddRadixSortBuffer<T, TSpecialization>(
        this AllocationBuilder allocationBuilder,
        long length)
        where T : unmanaged
        where TSpecialization : struct, IRadixSortSpecialization =>
        allocationBuilder.AddRadixSortBuffer<TSpecialization>(
            Interop.SizeOf<T>(),
            length);

    /// <summary>
    /// Adds a new buffer for radix sort operations.
    /// </summary>
    /// <typeparam name="TKey">The element key type to sort.</typeparam>
    /// <typeparam name="TValue">The element value type to sort.</typeparam>
    /// <typeparam name="TSpecialization">The specialization to use.</typeparam>
    /// <param name="allocationBuilder">The current allocation builder.</param>
    /// <param name="length">The data length.</param>
    [NotInsideKernel]
    public static void AddRadixSortPairsBuffer<TKey, TValue, TSpecialization>(
        this AllocationBuilder allocationBuilder,
        long length)
        where TKey : unmanaged
        where TValue : unmanaged
        where TSpecialization : struct, IRadixSortSpecialization
    {
        if (length < 1 || length > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(length));

        // Allocate temp buffer for all elements
        int elementSize = Interop.SizeOf<(TKey, TValue)>();
        allocationBuilder.AddBuffer(elementSize, length);
        allocationBuilder.AddRadixSortBuffer<TSpecialization>(elementSize, length);
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// Represents a wrapper operation that works on the key part.
    /// </summary>
    /// <typeparam name="TKey">The key type to sort.</typeparam>
    /// <typeparam name="TValue">The value type associated to each key.</typeparam>
    /// <typeparam name="TOperation">The underlying operation type.</typeparam>
    internal readonly struct RadixSortPairsOperation<TKey, TValue, TOperation> :
        IRadixSortOperation<(TKey, TValue)>
        where TKey : unmanaged
        where TValue : unmanaged
        where TOperation : struct, IRadixSortOperation<TKey>
    {
        /// <summary>
        /// Returns the number of bits of the parent
        /// <typeparamref name="TOperation"/>.
        /// </summary>
        public static int NumBits => TOperation.NumBits;

        /// <summary>
        /// Returns the default key-value pair based of the parent
        /// <typeparamref name="TOperation"/>.
        /// </summary>
        public static (TKey, TValue) DefaultValue =>
            (TOperation.DefaultValue, default);

        /// <summary>
        /// Extracts the bits from the key part of the given radix-sort pair using
        /// the parent <typeparamref name="TOperation"/>.
        /// </summary>
        public static int ExtractRadixBits(
            (TKey, TValue) value,
            int shift,
            int bitMask) =>
            TOperation.ExtractRadixBits(value.Item1, shift, bitMask);
    }

    #endregion

    #region Radix Sort

    /// <summary>
    /// Sorts all values in the given view using the radix sort algorithm.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRadixSortOperation">
    /// The type of the radix-sort operation.
    /// </typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The view to sort.</param>
    [NotInsideKernel, DelayCodeGeneration]
    public static void RadixSort<T, TRadixSortOperation>(
        this AcceleratorStream stream,
        ArrayView<T> view)
        where T : unmanaged
        where TRadixSortOperation : struct, IRadixSortOperation<T> =>
        stream.RadixSort<
            T,
            TRadixSortOperation,
            RadixSortSpecializations.Specialization4>(view);

    /// <summary>
    /// Sorts all values in the given view using the radix sort algorithm.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TRadixSortOperation">
    /// The type of the radix-sort operation.
    /// </typeparam>
    /// <typeparam name="TSpecialization">
    /// The specialization type to be used for unrolling and bit increments.
    /// </typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The view to sort.</param>
    [NotInsideKernel, DelayCodeGeneration]
    public static void RadixSort<T, TRadixSortOperation, TSpecialization>(
        this AcceleratorStream stream,
        ArrayView<T> view)
        where T : unmanaged
        where TRadixSortOperation : struct, IRadixSortOperation<T>
        where TSpecialization : struct, IRadixSortSpecialization =>
        stream.RadixSort<
            T,
            Stride1D.Dense,
            TRadixSortOperation,
            TSpecialization>(view.AsDense());

    /// <summary>
    /// Sorts all values in the given view using the radix sort algorithm.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The view stride.</typeparam>
    /// <typeparam name="TRadixSortOperation">
    /// The type of the radix-sort operation.
    /// </typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The view to sort.</param>
    [NotInsideKernel]
    public static void RadixSort<T, TStride, TRadixSortOperation>(
        this AcceleratorStream stream,
        ArrayView1D<T, TStride> view)
        where T : unmanaged
        where TStride : struct, IStride1D
        where TRadixSortOperation : struct, IRadixSortOperation<T> =>
        stream.RadixSort<
            T,
            TStride,
            TRadixSortOperation,
            RadixSortSpecializations.Specialization4>(view);

    /// <summary>
    /// Sorts all values in the given view using the radix sort algorithm.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TStride">The view stride.</typeparam>
    /// <typeparam name="TRadixSortOperation">
    /// The type of the radix-sort operation.
    /// </typeparam>
    /// <typeparam name="TSpecialization">
    /// The specialization type to be used for unrolling and bit increments.
    /// </typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The view to sort.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel, DelayCodeGeneration]
    public static void RadixSort<T, TStride, TRadixSortOperation, TSpecialization>(
        this AcceleratorStream stream,
        ArrayView1D<T, TStride> view)
        where T : unmanaged
        where TStride : struct, IStride1D
        where TRadixSortOperation : struct, IRadixSortOperation<T>
        where TSpecialization : struct, IRadixSortSpecialization
    {
        if (view.Length > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(view));
        if (TRadixSortOperation.NumBits < 1 ||
                TRadixSortOperation.NumBits % (TSpecialization.BitIncrement * 2) != 0)
        {
            throw new NotSupportedException(
                RuntimeErrorMessages.NotSupportedNumberOfRadixSortBits);
        }

        // Determine base configurations
        var config = stream.ComputeGridStrideKernelConfig(
            view.Extent,
            out int numIterationsPerGroup);
        long numVirtualGroups = config.GridSize * numIterationsPerGroup;
        long lengthInformation = XMath.PadToMultiple(view.Length, config.GroupSize);

        // Get temp output view
        using var tempData = stream.AllocateTemporary<T>(view.Length);
        var tempView = tempData.View.AsDense();

        // Get temp counter view
        long counterLength = numVirtualGroups * TSpecialization.UnrollFactor;
        using var counterData = stream.AllocateTemporary<uint>(counterLength * 2);
        var counterView = counterData.View.SubView(0, counterLength);
        var counterView2 = counterData.View.SubView(counterLength, counterLength);

        // Performs the first radix sort pass
        void RadixSortPhase1<TInputStride>(ArrayView1D<T, TInputStride> input, int shift)
            where TInputStride : struct, IStride1D
        {
            var scanMemory = Group.GetSharedMemory<uint>(
                Group.Dimension * TSpecialization.UnrollFactor);

            long gridIdx = Grid.Index;
            for (long i = Grid.GlobalThreadIndex;
                i < lengthInformation;
                i += Grid.GridStrideLoopStride)
            {
                bool inRange = i < input.Length;

                // Read value from global memory
                T value = TRadixSortOperation.DefaultValue;
                if (inRange)
                    value = input[i];
                var bits = TRadixSortOperation.ExtractRadixBits(
                    value,
                    shift,
                    TSpecialization.UnrollFactor - 1);

                for (int j = 0; j < TSpecialization.UnrollFactor; ++j)
                    scanMemory[Group.Index + Group.Dimension * j] = 0;
                if (inRange)
                    scanMemory[Group.Index + Group.Dimension * bits] = 1;
                Group.Barrier();

                for (int j = 0; j < TSpecialization.UnrollFactor; ++j)
                {
                    var address = Group.Index + Group.Dimension * j;
                    scanMemory[address] = Group.ExclusiveScan<uint, AddUInt32>(
                        scanMemory[address]);
                }
                Group.Barrier();

                if (Group.IsLastThread)
                {
                    // Write counters to global memory
                    for (int j = 0; j < TSpecialization.UnrollFactor; ++j)
                    {
                        ref uint newOffset = ref scanMemory[
                            Group.Index + Group.Dimension * j];
                        newOffset += Utilities.Select(
                            Bitwise.And(inRange, j == bits), 1U, 0U);
                        counterView[j * numVirtualGroups + gridIdx] = newOffset;
                    }
                }
                Group.Barrier();

                long gridSize = gridIdx * Group.Dimension;
                long pos = gridSize + scanMemory[
                    Group.Index + Group.Dimension * bits] -
                    Utilities.Select(
                        Bitwise.And(inRange, Group.IsLastThread), 1, 0);
                for (int j = 1; j <= bits; ++j)
                {
                    pos += scanMemory[Group.Dimension * j - 1] +
                        Utilities.Select(j - 1 == bits, 1, 0);
                }

                // Pre-sort the current value into the corresponding segment
                if (inRange)
                    input[pos] = value;
                Group.Barrier();

                gridIdx += Grid.Dimension;
            }
        }

        // Performs the second radix sort pass
        void RadixSortPhase2<TInputStride, TOutputStride>(
            ArrayView1D<T, TInputStride> input,
            ArrayView1D<T, TOutputStride> output,
            int shift)
            where TInputStride : struct, IStride1D
            where TOutputStride : struct, IStride1D
        {
            // Resolves the exclusive scan-value from the given counter view
            uint GetExclusiveCount(long index) =>
                index < 1L ? 0U : counterView2[index - 1L];

            long gridIdx = Grid.Index;
            for (long i = Grid.GlobalThreadIndex;
                i < lengthInformation;
                i += Grid.GridStrideLoopStride)
            {
                bool inRange = i < input.Length;

                // Read value from global memory
                T value = TRadixSortOperation.DefaultValue;
                if (inRange)
                    value = input[i];

                var bits = TRadixSortOperation.ExtractRadixBits(
                    value,
                    shift,
                    TSpecialization.UnrollFactor - 1);

                long offset = 0;
                long pos =
                    GetExclusiveCount(bits * numVirtualGroups + gridIdx) + Group.Index;

                for (int w = 0; w < bits; ++w)
                {
                    var address = w * numVirtualGroups + gridIdx;

                    uint baseCounter = counterView2[address];
                    uint negativeOffset = GetExclusiveCount(address);

                    offset += baseCounter - negativeOffset;
                }

                pos -= offset;

                if (inRange)
                    output[pos] = value;

                gridIdx += Grid.Dimension;
            }
        }

        // Use loop peeling to avoid swapping input and tempOutputView variables
        for (int bitIdx = 0; bitIdx < TRadixSortOperation.NumBits;)
        {
            // Phase 1: Prepare reordering
            stream.Initialize(counterView, 0U);
            stream.Launch(config, _ => RadixSortPhase1(view, bitIdx));
            stream.InclusiveScan<uint, AddUInt32>(counterView, counterView2);
            stream.Launch(config, _ => RadixSortPhase2(view, tempView, bitIdx));

            // Go ahead
            bitIdx += TSpecialization.BitIncrement;
            Debug.Assert(bitIdx < TRadixSortOperation.NumBits);

            // Phase2: Write to actual output view
            stream.Initialize(counterView, 0U);
            stream.Launch(config, _ => RadixSortPhase1(tempView, bitIdx));
            stream.InclusiveScan<uint, AddUInt32>(counterView, counterView2);
            stream.Launch(config, _ => RadixSortPhase2(tempView, view, bitIdx));

            // Go ahead
            bitIdx += TSpecialization.BitIncrement;
        }
    }

    #endregion

    #region Radix Sort Pairs

    /// <summary>
    /// Sorts all keys and values in the given view using the radix sort algorithm.
    /// </summary>
    /// <typeparam name="TKey">The key element type.</typeparam>
    /// <typeparam name="TValue">The value element type.</typeparam>
    /// <typeparam name="TRadixSortOperation">
    /// The type of the radix-sort operation.
    /// </typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="keys">The key view to sort.</param>
    /// <param name="values">The value view to sort.</param>
    [NotInsideKernel, DelayCodeGeneration]
    public static void RadixSortPairs<TKey, TValue, TRadixSortOperation>(
        this AcceleratorStream stream,
        ArrayView<TKey> keys,
        ArrayView<TValue> values)
        where TKey : unmanaged
        where TValue : unmanaged
        where TRadixSortOperation : struct, IRadixSortOperation<TKey> =>
        stream.RadixSortPairs<
            TKey,
            TValue,
            TRadixSortOperation,
            RadixSortSpecializations.Specialization4>(keys, values);

    /// <summary>
    /// Sorts all values in the given view using the radix sort algorithm.
    /// </summary>
    /// <typeparam name="TKey">The key element type.</typeparam>
    /// <typeparam name="TValue">The value element type.</typeparam>
    /// <typeparam name="TRadixSortOperation">
    /// The type of the radix-sort operation.
    /// </typeparam>
    /// <typeparam name="TSpecialization">
    /// The specialization type to be used for unrolling and bit increments.
    /// </typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="keys">The key view to sort.</param>
    /// <param name="values">The value view to sort.</param>
    [NotInsideKernel, DelayCodeGeneration]
    public static void RadixSortPairs<TKey, TValue, TRadixSortOperation, TSpecialization>(
        this AcceleratorStream stream,
        ArrayView<TKey> keys,
        ArrayView<TValue> values)
        where TKey : unmanaged
        where TValue : unmanaged
        where TRadixSortOperation : struct, IRadixSortOperation<TKey>
        where TSpecialization : struct, IRadixSortSpecialization =>
        stream.RadixSortPairs<
            TKey,
            TValue,
            Stride1D.Dense,
            Stride1D.Dense,
            TRadixSortOperation,
            TSpecialization>(keys.AsDense(), values.AsDense());

    /// <summary>
    /// Sorts all values in the given view using the radix sort algorithm.
    /// </summary>
    /// <typeparam name="TKey">The key element type.</typeparam>
    /// <typeparam name="TValue">The value element type.</typeparam>
    /// <typeparam name="TKeyStride">The view stride of all keys.</typeparam>
    /// <typeparam name="TValueStride">The view stride of all values.</typeparam>
    /// <typeparam name="TRadixSortOperation">
    /// The type of the radix-sort operation.
    /// </typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="keys">The key view to sort.</param>
    /// <param name="values">The value view to sort.</param>
    [NotInsideKernel]
    public static void RadixSortPairs<
        TKey,
        TValue,
        TKeyStride,
        TValueStride,
        TRadixSortOperation>(
        this AcceleratorStream stream,
        ArrayView1D<TKey, TKeyStride> keys,
        ArrayView1D<TValue, TValueStride> values)
        where TKey : unmanaged
        where TValue : unmanaged
        where TKeyStride : struct, IStride1D
        where TValueStride : struct, IStride1D
        where TRadixSortOperation : struct, IRadixSortOperation<TKey> =>
        stream.RadixSortPairs<
            TKey,
            TValue,
            TKeyStride,
            TValueStride,
            TRadixSortOperation,
            RadixSortSpecializations.Specialization4>(keys, values);

    /// <summary>
    /// Sorts all values in the given view using the radix sort algorithm.
    /// </summary>
    /// <typeparam name="TKey">The key element type.</typeparam>
    /// <typeparam name="TValue">The value element type.</typeparam>
    /// <typeparam name="TKeyStride">The view stride of all keys.</typeparam>
    /// <typeparam name="TValueStride">The view stride of all values.</typeparam>
    /// <typeparam name="TRadixSortOperation">
    /// The type of the radix-sort operation.
    /// </typeparam>
    /// <typeparam name="TSpecialization">
    /// The specialization type to be used for unrolling and bit increments.
    /// </typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="keys">The key view to sort.</param>
    /// <param name="values">The value view to sort.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [NotInsideKernel, DelayCodeGeneration]
    public static void RadixSortPairs<
        TKey,
        TValue,
        TKeyStride,
        TValueStride,
        TRadixSortOperation,
        TSpecialization>(
        this AcceleratorStream stream,
        ArrayView1D<TKey, TKeyStride> keys,
        ArrayView1D<TValue, TValueStride> values)
        where TKey : unmanaged
        where TValue : unmanaged
        where TKeyStride : struct, IStride1D
        where TValueStride : struct, IStride1D
        where TRadixSortOperation : struct, IRadixSortOperation<TKey>
        where TSpecialization : struct, IRadixSortSpecialization
    {
        if (keys.Length < 1)
            throw new ArgumentOutOfRangeException(nameof(keys));
        if (keys.Length != values.Length)
            throw new ArgumentOutOfRangeException(nameof(values));

        using var tempData = stream.AllocateTemporary<(TKey, TValue)>(keys.Length);
        var tempView = tempData.View;

        // Gather all key value pairs
        stream.Launch(keys.Length, index =>
        {
            var key = keys[index];
            var value = values[index];
            tempView[index] = (key, value);
        });

        // Sort all pairs
        stream.RadixSort<
            (TKey, TValue),
            RadixSortPairsOperation<TKey, TValue, TRadixSortOperation>,
            TSpecialization>(tempView);

        // Scatter results back
        stream.Launch(keys.Length, index =>
        {
            var (key, value) = tempView[index];
            keys[index] = key;
            values[index] = value;
        });
    }

    #endregion
}
