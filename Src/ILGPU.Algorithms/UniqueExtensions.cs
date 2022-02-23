// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: UniqueExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.ComparisonOperations;
using ILGPU.Algorithms.Resources;
using ILGPU.Runtime;
using System;

namespace ILGPU.Algorithms
{
    #region Delegates

    /// <summary>
    /// Represents an operation to remove consecutive duplicate elements in a given view.
    /// </summary>
    /// <typeparam name="T">The input view element type.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input view.</param>
    /// <param name="output">The output view to store the new length.</param>
    /// <param name="temp">The temp view to store temporary results.</param>
    public delegate void Unique<T>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<long> output,
        ArrayView<int> temp)
        where T : unmanaged;

    #endregion

    /// <summary>
    /// Contains extension methods for unique operations.
    /// </summary>
    public static partial class UniqueExtensions
    {
        #region Implementation

        /// <summary>
        /// The actual unique kernel implementation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TComparisonOperation">The comparison operation.</typeparam>
        /// <param name="input">The input view.</param>
        /// <param name="output">The output view to store the new length.</param>
        /// <param name="sequentialGroupExecutor">
        /// The sequential group executor to use.
        /// </param>
        /// <param name="tileSize">The tile size.</param>
        /// <param name="numIterationsPerGroup">
        /// The number of iterations per group.
        /// </param>
        internal static void UniqueKernel<T, TComparisonOperation>(
            ArrayView<T> input,
            ArrayView<long> output,
            SequentialGroupExecutor sequentialGroupExecutor,
            SpecializedValue<int> tileSize,
            Index1D numIterationsPerGroup)
            where T : unmanaged
            where TComparisonOperation : struct, IComparisonOperation<T>
        {
            TComparisonOperation comparison = default;
            var isFirstGrid = Grid.IdxX == 0;
            var tileInfo = new TileInfo(input.IntLength, numIterationsPerGroup);

            // Sync groups and wait for the current one to become active
            sequentialGroupExecutor.Wait();

            var temp = SharedMemory.Allocate<bool>(tileSize);
            var startIdx = Grid.ComputeGlobalIndex(Grid.IdxX * numIterationsPerGroup, 0);

            for (
                int i = tileInfo.StartIndex;
                i < tileInfo.MaxLength;
                i += Group.DimX)
            {
                if (Group.IsFirstThread && i == tileInfo.StartIndex && isFirstGrid)
                {
                    temp[0] = true;
                }
                else
                {
                    var currIdx = i;
                    var prevIdx = Group.IsFirstThread && i == tileInfo.StartIndex
                        ? output[0] - 1
                        : currIdx - 1;

                    temp[currIdx - startIdx] =
                        comparison.Compare(input[currIdx], input[prevIdx]) != 0;
                }
            }
            Group.Barrier();

            if (Group.IsFirstThread)
            {
                var offset = isFirstGrid ? 0 : output[0];
                var maxLength =
                    XMath.Min(startIdx + temp.IntLength, tileInfo.MaxLength) - startIdx;

                for (var i = 0; i < maxLength; i++)
                {
                    if (temp[i])
                        input[offset++] = input[startIdx + i];
                }
                output[0] = offset;
            }

            MemoryFence.DeviceLevel();
            Group.Barrier();
            sequentialGroupExecutor.Release();
        }

        /// <summary>
        /// Computes the required number of temp-storage elements of type
        /// <typeparamref name="T"/> for a unique operation and the given data length.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="dataLength">The number of data elements to scan.</param>
        /// <returns>
        /// The required number of temp-storage elements in 32 bit ints.
        /// </returns>
        public static LongIndex1D ComputeUniqueTempStorageSize<T>(
            this Accelerator accelerator,
            LongIndex1D dataLength)
            where T : unmanaged
        {
            // 1 int for SequentialGroupExecutor.
            return 1;
        }

        /// <summary>
        /// Creates a kernel to remove consecutive duplicate elements in a given view.
        /// </summary>
        /// <typeparam name="T">The input view element type.</typeparam>
        /// <typeparam name="TComparisonOperation">The comparison operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created unique handler.</returns>
        public static Unique<T> CreateUnique<T, TComparisonOperation>(
            this Accelerator accelerator)
            where T : unmanaged
            where TComparisonOperation : struct, IComparisonOperation<T>
        {
            var initializer = accelerator.CreateInitializer<int, Stride1D.Dense>();
            var kernel = accelerator.LoadKernel<
                ArrayView<T>,
                ArrayView<long>,
                SequentialGroupExecutor,
                SpecializedValue<int>,
                Index1D>(
                UniqueKernel<T, TComparisonOperation>);

            return (stream, input, output, temp) =>
            {
                if (!input.IsValid)
                    throw new ArgumentNullException(nameof(input));
                if (!output.IsValid)
                    throw new ArgumentNullException(nameof(output));
                if (!temp.IsValid)
                    throw new ArgumentNullException(nameof(temp));
                if (output.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(output));
                if (temp.Length <
                    accelerator.ComputeUniqueTempStorageSize<T>(input.Length))
                {
                    throw new ArgumentOutOfRangeException(nameof(temp));
                }
                if (input.Length > int.MaxValue)
                {
                    throw new NotSupportedException(
                        ErrorMessages.NotSupportedArrayView64);
                }

                var viewManager = new TempViewManager(temp, nameof(temp));
                var executorView = viewManager.Allocate<int>();
                initializer(stream, temp.SubView(0, viewManager.NumInts), default);

                var (gridDim, groupDim) = accelerator.ComputeGridStrideLoopExtent(
                       input.IntLength,
                       out int numIterationsPerGroup);
                kernel(
                    stream,
                    (gridDim, groupDim),
                    input,
                    output,
                    new SequentialGroupExecutor(executorView),
                    new SpecializedValue<int>(groupDim * numIterationsPerGroup),
                    numIterationsPerGroup);
            };
        }

        /// <summary>
        /// Removes consecutive duplicate elements in a supplied input view.
        /// </summary>
        /// <typeparam name="T">The input view element type.</typeparam>
        /// <typeparam name="TComparisonOperation">The comparison operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input view.</param>
        /// <returns>The new/valid length of the input view.</returns>
        public static long Unique<T, TComparisonOperation>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input)
            where T : unmanaged
            where TComparisonOperation : struct, IComparisonOperation<T>
        {
            using var output = accelerator.Allocate1D<long>(1);
            using var temp = accelerator.Allocate1D<int>(
                accelerator.ComputeUniqueTempStorageSize<T>(input.Length));
            accelerator.CreateUnique<T, TComparisonOperation>()(
                stream,
                input,
                output.View,
                temp.View);
            long result = 0L;
            output.View.CopyToCPU(stream, ref result, 1);
            return result;
        }

        #endregion
    }
}
