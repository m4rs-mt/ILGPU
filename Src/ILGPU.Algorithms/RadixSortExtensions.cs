// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright(c) 2016-2018 ILGPU Lightning Project
//                                    www.ilgpu.net
//
// File: RadixSortExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.RadixSortOperations;
using ILGPU.Algorithms.Resources;
using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    #region Delegates

    /// <summary>
    /// Represents a radix sort operation using a shuffle and operation logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The elements to sort.</param>
    /// <param name="temp">The temp view to store temporary results.</param>
    /// <remarks>The view buffer will be changed during the sorting operation.</remarks>
    public delegate void RadixSort<T>(
        AcceleratorStream stream,
        ArrayView<T> view,
        ArrayView<int> temp)
        where T : unmanaged;

    /// <summary>
    /// Represents a radix sort operation using a shuffle and operation logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="view">The elements to sort.</param>
    /// <remarks>The view buffer will be changed during the sorting operation.</remarks>
    public delegate void BufferedRadixSort<T>(AcceleratorStream stream, ArrayView<T> view)
        where T : unmanaged;

    #endregion

    /// <summary>
    /// Represents a radix-sort provider for a radix sort operation.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class RadixSortProvider : AlgorithmObject
    {
        #region Instance

        private readonly MemoryBufferCache bufferCache;

        internal RadixSortProvider(Accelerator accelerator)
            : base(accelerator)
        {
            bufferCache = new MemoryBufferCache(accelerator);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Allocates a temporary memory view.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortOperation">
        /// The type of the radix-sort operation.
        /// </typeparam>
        /// <param name="input">The input view.</param>
        /// <returns>The allocated temporary view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ArrayView<int> AllocateTempRadixSortView<T, TRadixSortOperation>(
            ArrayView<T> input)
            where T : unmanaged
            where TRadixSortOperation : struct, IRadixSortOperation<T>
        {
            var tempSize = Accelerator.ComputeRadixSortTempStorageSize<
                T,
                TRadixSortOperation>(
                input.Length);
            return bufferCache.Allocate<int>(tempSize);
        }

        /// <summary>
        /// Creates a new radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortOperation">
        /// The type of the radix-sort operation.
        /// </typeparam>
        /// <returns>The created radix sort handler.</returns>
        public BufferedRadixSort<T> CreateRadixSort<T, TRadixSortOperation>()
            where T : unmanaged
            where TRadixSortOperation : struct, IRadixSortOperation<T>
        {
            var radixSort = Accelerator.CreateRadixSort<T, TRadixSortOperation>();
            return (stream, input) =>
            {
                var tempView = AllocateTempRadixSortView<T, TRadixSortOperation>(input);
                radixSort(stream, input, tempView);
            };
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                bufferCache.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// Contains extension methods for radix-sort operations.
    /// </summary>
    public static class RadixSortExtensions
    {
        #region RadixSort Helpers

        /// <summary>
        /// A pass delegate for the first pass.
        /// </summary>
        private delegate void Pass1KernelDelegate<T>(
            AcceleratorStream stream,
            KernelConfig config,
            ArrayView<T> view,
            ArrayView<int> counter,
            SpecializedValue<int> groupSize,
            int numGroups,
            int paddedLength,
            int shift)
            where T : unmanaged;

        /// <summary>
        /// A pass delegate for the first pass.
        /// </summary>
        private delegate void CPUPass1KernelDelegate<T>(
            AcceleratorStream stream,
            KernelConfig config,
            ArrayView<T> input,
            ArrayView<T> output,
            ArrayView<int> counter,
            int numGroups,
            int numIterationsPerGroup,
            int shift)
            where T : unmanaged;

        /// <summary>
        /// A pass delegate for the second pass.
        /// </summary>
        private delegate void Pass2KernelDelegate<T>(
            AcceleratorStream stream,
            KernelConfig config,
            ArrayView<T> input,
            ArrayView<T> output,
            ArrayView<int> counter,
            int numGroups,
            int paddedLength,
            int shift)
            where T : unmanaged;

        /// <summary>
        /// A pass delegate for the second pass.
        /// </summary>
        private delegate void CPUPass2KernelDelegate<T>(
            AcceleratorStream stream,
            KernelConfig config,
            ArrayView<T> input,
            ArrayView<T> output,
            ArrayView<int> counter,
            int numGroups,
            int numIterationsPerGroup,
            int shift)
            where T : unmanaged;

        /// <summary>
        /// Computes the required number of temp-storage elements for a radix sort
        /// operation and the given data length.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortOperation">
        /// The type of the radix-sort operation.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="dataLength">The number of data elements to sort.</param>
        /// <returns>
        /// The required number of temp-storage elements in 32 bit ints.
        /// </returns>
        public static Index1 ComputeRadixSortTempStorageSize<T, TRadixSortOperation>(
            this Accelerator accelerator,
            Index1 dataLength)
            where T : unmanaged
            where TRadixSortOperation : struct, IRadixSortOperation<T>
        {
            LongIndex1 tempScanMemoryLong =
                accelerator.ComputeScanTempStorageSize<T>(dataLength);
            IndexTypeExtensions.AssertIntIndexRange(tempScanMemoryLong);
            Index1 tempScanMemory = tempScanMemoryLong.ToIntIndex();

            int numGroups;
            if (accelerator.AcceleratorType == AcceleratorType.CPU)
                numGroups = accelerator.MaxNumThreads;
            else
            {
                var (gridDim, _) = accelerator.ComputeGridStrideLoopExtent(
                    dataLength,
                    out int numIterationsPerGroup);
                numGroups = gridDim * numIterationsPerGroup;
            }

            long numIntTElementsLong = Interop.ComputeRelativeSizeOf<int, T>(dataLength);
            IndexTypeExtensions.AssertIntIndexRange(numIntTElementsLong);
            int numIntTElements = (int)numIntTElementsLong;

            const int unrollFactor = 4;
            return numGroups * unrollFactor * 2 + numIntTElements + tempScanMemory;
        }

        #endregion

        #region RadixSort Implementation

        private static readonly MethodInfo CPURadixSortKernel1Method =
            typeof(RadixSortExtensions).GetMethod(
                nameof(CPURadixSortKernel1),
                BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo CPURadixSortKernel2Method =
            typeof(RadixSortExtensions).GetMethod(
                nameof(CPURadixSortKernel2),
                BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo RadixSortKernel1Method =
            typeof(RadixSortExtensions).GetMethod(
                nameof(RadixSortKernel1),
                BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo RadixSortKernel2Method =
            typeof(RadixSortExtensions).GetMethod(
                nameof(RadixSortKernel2),
                BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Represents a single specialization.
        /// </summary>
        internal interface IRadixSortSpecialization
        {
            /// <summary>
            /// Returns the associated constant unroll factor.
            /// </summary>
            int UnrollFactor { get; }

            /// <summary>
            /// Returns the number of bits to increment for the
            /// next radix-sort iteration.
            /// </summary>
            int BitIncrement { get; }
        }

        /// <summary>
        /// Performs the first radix-sort pass.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TOperation">The radix-sort operation.</typeparam>
        /// <typeparam name="TSpecialization">The specialization type.</typeparam>
        /// <param name="view">The input view to use.</param>
        /// <param name="counter">The global counter view.</param>
        /// <param name="groupSize">The number of threads in the group.</param>
        /// <param name="numGroups">The number of virtually launched groups.</param>
        /// <param name="paddedLength">The padded length of the input view.</param>
        /// <param name="shift">The bit shift to use.</param>
        internal static void RadixSortKernel1<T, TOperation, TSpecialization>(
            ArrayView<T> view,
            ArrayView<int> counter,
            SpecializedValue<int> groupSize,
            int numGroups,
            int paddedLength,
            int shift)
            where T : unmanaged
            where TOperation : struct, IRadixSortOperation<T>
            where TSpecialization : struct, IRadixSortSpecialization
        {
            TSpecialization specialization = default;
            var scanMemory = SharedMemory.Allocate<int>(
                groupSize * specialization.UnrollFactor);

            int gridIdx = Grid.IdxX;
            for (
                int i = Grid.GlobalIndex.X;
                i < paddedLength;
                i += GridExtensions.GridStrideLoopStride)
            {
                bool inRange = i < view.Length;

                // Read value from global memory
                TOperation operation = default;
                T value = operation.DefaultValue;
                if (inRange)
                    value = view[i];
                var bits = operation.ExtractRadixBits(
                    value,
                    shift,
                    specialization.UnrollFactor - 1);

                for (int j = 0; j < specialization.UnrollFactor; ++j)
                    scanMemory[Group.IdxX + groupSize * j] = 0;
                if (inRange)
                    scanMemory[Group.IdxX + groupSize * bits] = 1;
                Group.Barrier();

                for (int j = 0; j < specialization.UnrollFactor; ++j)
                {
                    var address = Group.IdxX + groupSize * j;
                    scanMemory[address] =
                        GroupExtensions.ExclusiveScan<int, AddInt32>(scanMemory[address]);
                }
                Group.Barrier();

                if (Group.IdxX == Group.DimX - 1)
                {
                    // Write counters to global memory
                    for (int j = 0; j < specialization.UnrollFactor; ++j)
                    {
                        ref var newOffset = ref scanMemory[Group.IdxX + groupSize * j];
                        newOffset += Utilities.Select(inRange & j == bits, 1, 0);
                        counter[j * numGroups + gridIdx] = newOffset;
                    }
                }
                Group.Barrier();

                var gridSize = gridIdx * Group.DimX;
                Index1 pos = gridSize + scanMemory[Group.IdxX + groupSize * bits] -
                    Utilities.Select(inRange & Group.IdxX == Group.DimX - 1, 1, 0);
                for (int j = 1; j <= bits; ++j)
                {
                    pos += scanMemory[groupSize * j - 1] +
                        Utilities.Select(j - 1 == bits, 1, 0);
                }

                // Pre-sort the current value into the corresponding segment
                if (inRange)
                    view[pos] = value;
                Group.Barrier();

                gridIdx += Grid.DimX;
            }
        }

        /// <summary>
        /// Performs the first radix-sort pass on the CPU.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TOperation">The radix-sort operation.</typeparam>
        /// <typeparam name="TSpecialization">The specialization type.</typeparam>
        /// <param name="input">The input view to use.</param>
        /// <param name="output">The input view to use.</param>
        /// <param name="counter">The global counter view.</param>
        /// <param name="numGroups">The number of virtually launched groups.</param>
        /// <param name="numIterationsPerGroup">
        /// The number of iterations per group.
        /// </param>
        /// <param name="shift">The bit shift to use.</param>
        internal static void CPURadixSortKernel1<T, TOperation, TSpecialization>(
            ArrayView<T> input,
            ArrayView<T> output,
            ArrayView<int> counter,
            int numGroups,
            int numIterationsPerGroup,
            int shift)
            where T : unmanaged
            where TOperation : struct, IRadixSortOperation<T>
            where TSpecialization : struct, IRadixSortSpecialization
        {
            TSpecialization specialization = default;
            var scanMemory = SharedMemory.Allocate<int>(specialization.UnrollFactor);
            var addMemory = SharedMemory.Allocate<int>(specialization.UnrollFactor);

            for (int j = 0; j < specialization.UnrollFactor; ++j)
            {
                scanMemory[j] = 0;
                addMemory[j] = 0;
            }

            var tileInfo = new TileInfo<T>(input, numIterationsPerGroup);

            // Compute local segment information
            for (Index1 i = tileInfo.StartIndex; i < tileInfo.MaxLength; ++i)
            {
                // Read value from global memory
                TOperation operation = default;
                T value = input[i];
                var bits = operation.ExtractRadixBits(
                    value,
                    shift,
                    specialization.UnrollFactor - 1);
                ++scanMemory[bits];
            }

            // Store global counter
            for (int j = 0; j < specialization.UnrollFactor; ++j)
                counter[numGroups * j + Grid.IdxX] = scanMemory[j];

            int scanned = 0;
            int previous = scanMemory[0];
            for (int j = 1; j < specialization.UnrollFactor; ++j)
            {
                scanned += previous;
                previous = scanMemory[j];
                scanMemory[j] = scanned;
            }
            scanMemory[0] = 0;

            // Pre-sort the current value into the corresponding segment
            for (Index1 i = tileInfo.StartIndex; i < tileInfo.MaxLength; ++i)
            {
                // Read value from global memory
                TOperation operation = default;
                T value = input[i];
                var bits = operation.ExtractRadixBits(
                    value,
                    shift,
                    specialization.UnrollFactor - 1);

                Index1 pos = tileInfo.StartIndex;
                pos += addMemory[bits]++;
                pos += scanMemory[bits];
                output[pos] = value;
            }
        }

        /// <summary>
        /// Resolves the exclusive scan-value from the given counter view.
        /// </summary>
        /// <param name="index">The current index.</param>
        /// <param name="counter">The counter view.</param>
        /// <returns>The exclusive sum.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetExclusiveCount(Index1 index, ArrayView<int> counter) =>
            index < Index1.One ? 0 : counter[index - Index1.One];

        /// <summary>
        /// Performs the second radix-sort pass.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TOperation">The radix-sort operation.</typeparam>
        /// <typeparam name="TSpecialization">The specialization type.</typeparam>
        /// <param name="input">The input view to use.</param>
        /// <param name="output">The output view to use.</param>
        /// <param name="counter">The global counter view.</param>
        /// <param name="numGroups">The number of virtually launched groups.</param>
        /// <param name="paddedLength">The padded length of the input view.</param>
        /// <param name="shift">The bit shift to use.</param>
        internal static void RadixSortKernel2<T, TOperation, TSpecialization>(
            ArrayView<T> input,
            ArrayView<T> output,
            ArrayView<int> counter,
            int numGroups,
            int paddedLength,
            int shift)
            where T : unmanaged
            where TOperation : struct, IRadixSortOperation<T>
            where TSpecialization : struct, IRadixSortSpecialization
        {
            var gridIdx = Grid.IdxX;

            for (
                int i = Grid.GlobalIndex.X;
                i < paddedLength;
                i += GridExtensions.GridStrideLoopStride)
            {
                bool inRange = i < input.Length;

                // Read value from global memory
                TOperation operation = default;
                T value = operation.DefaultValue;
                if (inRange)
                    value = input[i];

                TSpecialization specialization = default;
                var bits = operation.ExtractRadixBits(
                    value,
                    shift,
                    specialization.UnrollFactor - 1);

                int offset = 0;
                int pos = GetExclusiveCount(bits * numGroups + gridIdx, counter) +
                    Group.IdxX;

                for (int w = 0; w < bits; ++w)
                {
                    var address = w * numGroups + gridIdx;

                    int baseCounter = counter[address];
                    int negativeOffset = GetExclusiveCount(address, counter);

                    offset += baseCounter - negativeOffset;
                }

                pos -= offset;

                if (inRange)
                    output[pos] = value;

                gridIdx += Grid.DimX;
            }
        }

        /// <summary>
        /// Performs the second radix-sort pass.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TOperation">The radix-sort operation.</typeparam>
        /// <typeparam name="TSpecialization">The specialization type.</typeparam>
        /// <param name="input">The input view to use.</param>
        /// <param name="output">The output view to use.</param>
        /// <param name="counter">The global counter view.</param>
        /// <param name="numGroups">The number of virtually launched groups.</param>
        /// <param name="numIterationsPerGroup">
        /// The number of iterations per group.
        /// </param>
        /// <param name="shift">The bit shift to use.</param>
        internal static void CPURadixSortKernel2<T, TOperation, TSpecialization>(
            ArrayView<T> input,
            ArrayView<T> output,
            ArrayView<int> counter,
            int numGroups,
            int numIterationsPerGroup,
            int shift)
            where T : unmanaged
            where TOperation : struct, IRadixSortOperation<T>
            where TSpecialization : struct, IRadixSortSpecialization
        {
            TSpecialization specialization = default;
            var tileInfo = new TileInfo<T>(input, numIterationsPerGroup);

            for (Index1 i = tileInfo.StartIndex; i < tileInfo.MaxLength; ++i)
            {
                // Read value from global memory
                TOperation operation = default;
                T value = input[i];

                var bits = operation.ExtractRadixBits(
                    value,
                    shift,
                    specialization.UnrollFactor - 1);

                int offset = 0;
                int pos = GetExclusiveCount(bits * numGroups + Grid.IdxX, counter) +
                    i - tileInfo.StartIndex;

                // Compute offset
                for (int w = 0; w < bits; ++w)
                {
                    var address = w * numGroups + Grid.IdxX;

                    int baseCounter = counter[address];
                    int negativeOffset = GetExclusiveCount(address, counter);

                    offset += baseCounter - negativeOffset;
                }

                pos -= offset;

                // Move value
                output[pos] = value;
            }
        }

        #endregion

        #region RadixSort Specializations

        /// <summary>
        /// A specialization with unroll factor 4.
        /// </summary>
        readonly struct Specialization4 : IRadixSortSpecialization
        {
            /// <summary cref="IRadixSortSpecialization.UnrollFactor"/>
            public int UnrollFactor => 4;

            /// <summary cref="IRadixSortSpecialization.BitIncrement"/>
            public int BitIncrement => 2;
        }

        #endregion

        #region RadixSort

        /// <summary>
        /// Creates a new radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortOperation">
        /// The type of the radix-sort operation.
        /// </typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created radix sort handler.</returns>
        public static RadixSort<T> CreateRadixSort<T, TRadixSortOperation>(
            this Accelerator accelerator)
            where T : unmanaged
            where TRadixSortOperation : struct, IRadixSortOperation<T>
        {
            var initializer = accelerator.CreateInitializer<int>();
            var inclusiveScan = accelerator.CreateInclusiveScan<int, AddInt32>();

            var specializationType = typeof(Specialization4);
            var specialization = new Specialization4();

            if (accelerator.AcceleratorType == AcceleratorType.CPU)
            {
                var pass1Kernel = accelerator.LoadKernel<CPUPass1KernelDelegate<T>>(
                    CPURadixSortKernel1Method.MakeGenericMethod(
                        typeof(T),
                        typeof(TRadixSortOperation), specializationType));
                var pass2Kernel = accelerator.LoadKernel<CPUPass2KernelDelegate<T>>(
                    CPURadixSortKernel2Method.MakeGenericMethod(
                        typeof(T),
                        typeof(TRadixSortOperation), specializationType));

                return (stream, input, tempView) =>
                {
                    if (input.Length > int.MaxValue)
                    {
                        throw new NotSupportedException(
                            ErrorMessages.NotSupportedArrayView64);
                    }

                    var (gridDim, groupDim) = (accelerator.MaxNumThreads, 1);
                    int numVirtualGroups = gridDim;
                    long numIterationsPerGroupLong =
                        XMath.DivRoundUp(input.Length, gridDim);
                    IndexTypeExtensions.AssertIntIndexRange(numIterationsPerGroupLong);
                    int numIterationsPerGroup = (int)numIterationsPerGroupLong;

                    VerifyArguments<T, TRadixSortOperation>(
                        accelerator,
                        input,
                        tempView,
                        specialization.UnrollFactor,
                        numVirtualGroups,
                        out var counterView,
                        out var counterView2,
                        out var tempScanView,
                        out var tempOutputView);

                    TRadixSortOperation radixSortOperation = default;
                    for (
                        int bitIdx = 0;
                        bitIdx < radixSortOperation.NumBits;
                        bitIdx += specialization.BitIncrement)
                    {
                        initializer(stream, counterView, 0);
                        pass1Kernel(
                            stream,
                            (gridDim, groupDim),
                            input,
                            tempOutputView,
                            counterView,
                            numVirtualGroups,
                            numIterationsPerGroup,
                            bitIdx);

                        inclusiveScan(
                            stream,
                            counterView,
                            counterView2,
                            tempScanView);
                        pass2Kernel(
                            stream,
                            (gridDim, groupDim),
                            tempOutputView,
                            input,
                            counterView2,
                            numVirtualGroups,
                            numIterationsPerGroup,
                            bitIdx);
                    }
                };
            }
            else
            {
                var pass1Kernel = accelerator.LoadKernel<Pass1KernelDelegate<T>>(
                    RadixSortKernel1Method.MakeGenericMethod(
                        typeof(T),
                        typeof(TRadixSortOperation),
                        specializationType));
                var pass2Kernel = accelerator.LoadKernel<Pass2KernelDelegate<T>>(
                    RadixSortKernel2Method.MakeGenericMethod(
                        typeof(T),
                        typeof(TRadixSortOperation),
                        specializationType));

                return (stream, input, tempView) =>
                {
                    var (gridDim, groupDim) = accelerator.ComputeGridStrideLoopExtent(
                        input.Length,
                        out int numIterationsPerGroup);
                    int numVirtualGroups = gridDim * numIterationsPerGroup;
                    int lengthInformation = XMath.DivRoundUp(input.Length, groupDim) *
                        groupDim;

                    VerifyArguments<T, TRadixSortOperation>(
                        accelerator,
                        input,
                        tempView,
                        specialization.UnrollFactor,
                        numVirtualGroups,
                        out var counterView,
                        out var counterView2,
                        out var tempScanView,
                        out var tempOutputView);

                    TRadixSortOperation radixSortOperation = default;
                    for (
                        int bitIdx = 0;
                        bitIdx < radixSortOperation.NumBits;
                        bitIdx += specialization.BitIncrement)
                    {
                        initializer(stream, counterView, 0);
                        pass1Kernel(
                            stream,
                            (gridDim, groupDim),
                            input,
                            counterView,
                            SpecializedValue.New<int>(groupDim),
                            numVirtualGroups,
                            lengthInformation,
                            bitIdx);

                        inclusiveScan(
                            stream,
                            counterView,
                            counterView2,
                            tempScanView);
                        pass2Kernel(
                            stream,
                            (gridDim, groupDim),
                            input,
                            tempOutputView,
                            counterView2,
                            numVirtualGroups,
                            lengthInformation,
                            bitIdx);

                        Utilities.Swap(ref input, ref tempOutputView);
                    }
                };
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void VerifyArguments<T, TRadixSortOperation>(
            Accelerator accelerator,
            ArrayView<T> input,
            ArrayView<int> tempView,
            int unrollFactor,
            int numVirtualGroups,
            out ArrayView<int> counterView,
            out ArrayView<int> counterView2,
            out ArrayView<int> tempScanView,
            out ArrayView<T> tempOutputView)
            where T : unmanaged
            where TRadixSortOperation : struct, IRadixSortOperation<T>
        {
            if (!input.IsValid)
                throw new ArgumentNullException(nameof(input));

            var viewManager = new TempViewManager(tempView, nameof(tempView));

            int counterOffset = numVirtualGroups * unrollFactor;
            long tempScanMemorySize =
                accelerator.ComputeScanTempStorageSize<T>(counterOffset);

            tempOutputView = viewManager.Allocate<T>(input.Length);
            counterView = viewManager.Allocate<int>(counterOffset);
            counterView2 = viewManager.Allocate<int>(counterOffset);
            tempScanView = viewManager.Allocate<int>(tempScanMemorySize);
        }

        /// <summary>
        /// Creates a new specialized radix-sort provider that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created provider.</returns>
        public static RadixSortProvider CreateRadixSortProvider(
            this Accelerator accelerator) =>
            new RadixSortProvider(accelerator);

        #endregion
    }
}
