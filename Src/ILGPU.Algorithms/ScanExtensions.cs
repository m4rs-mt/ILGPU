// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                Copyright(c) 2016-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ScanExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Runtime.CompilerServices;
using static ILGPU.Algorithms.GroupExtensions;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Represents the scan operation type.
    /// </summary>
    public enum ScanKind
    {
        /// <summary>
        /// An inclusive scan operation.
        /// </summary>
        Inclusive,

        /// <summary>
        /// An exclusive scan operation.
        /// </summary>
        Exclusive
    }

    #region Scan Delegates

    /// <summary>
    /// Represents a scan operation using a shuffle and operation logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to scan.</param>
    /// <param name="output">The output view to store the scanned values.</param>
    /// <param name="temp">The temp view to store temporary results.</param>
    public delegate void Scan<T>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output,
        ArrayView<int> temp)
        where T : unmanaged;

    /// <summary>
    /// Represents a scan operation using a shuffle and operation logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to scan.</param>
    /// <param name="output">The output view to store the scanned values.</param>
    public delegate void BufferedScan<T>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output)
        where T : unmanaged;

    #endregion

    /// <summary>
    /// Represents a scan provider for a scan operation.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class ScanProvider : AlgorithmObject
    {
        #region Instance

        private readonly MemoryBufferCache bufferCache;

        internal ScanProvider(Accelerator accelerator)
            : base(accelerator)
        {
            bufferCache = new MemoryBufferCache(Accelerator);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Allocates a temporary memory view.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="input">The input view.</param>
        /// <returns>The allocated temporary view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ArrayView<int> AllocateTempScanView<T>(ArrayView<T> input)
            where T : unmanaged
        {
            var tempSize = Accelerator.ComputeScanTempStorageSize<T>(input.Length);
            return bufferCache.Allocate<int>(tempSize);
        }

        /// <summary>
        /// Creates a new buffered scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="kind">The scan kind.</param>
        /// <returns>The created scan handler.</returns>
        public BufferedScan<T> CreateScan<T, TScanOperation>(
            ScanKind kind)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            var scan = Accelerator.CreateScan<T, TScanOperation>(kind);
            return (stream, input, output) =>
            {
                var tempView = AllocateTempScanView(input);
                scan(stream, input, output, tempView);
            };
        }

        /// <summary>
        /// Creates a new buffered inclusive scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <returns>The created inclusive scan handler.</returns>
        public BufferedScan<T> CreateInclusiveScan<T, TScanOperation>()
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            CreateScan<T, TScanOperation>(ScanKind.Inclusive);

        /// <summary>
        /// Creates a new buffered exclusive scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <returns>The created exclusive scan handler.</returns>
        public BufferedScan<T> CreateExclusiveScan<T, TScanOperation>()
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            CreateScan<T, TScanOperation>(ScanKind.Exclusive);

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
    /// Contains extension methods for scan operations.
    /// </summary>
    public static partial class ScanExtensions
    {
        #region Scan Helpers

        /// <summary>
        /// Computes the required number of temp-storage elements of type <typeparamref name="T"/>
        /// for a scan operation and the given data length.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="dataLength">The number of data elements to scan.</param>
        /// <returns>The required number of temp-storage elements in 32 bit ints.</returns>
        public static Index1 ComputeScanTempStorageSize<T>(
            this Accelerator accelerator,
            Index1 dataLength)
            where T : unmanaged
        {
            return accelerator.AcceleratorType switch
            {
                AcceleratorType.CPU => 1,
                AcceleratorType.Cuda => ComputeNumIntElementsForSinglePassScan<T>(),
                _ => accelerator.MaxNumGroupsExtent.Item1 *
                    Interop.ComputeRelativeSizeOf<int, T>(),
            };
        }

        #endregion

        #region Scan Primitives

        /// <summary>
        /// An abstract scan implementation that implements required low-level scan functionality.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The scan operation.</typeparam>
        internal interface IScanImplementation<T, TScanOperation> : IGroupScan<T, TScanOperation>
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            /// <summary>
            /// Performs an all-reduction for all threads in the current group.
            /// </summary>
            /// <param name="value">The value from the current thread.</param>
            /// <returns>The reduced value.</returns>
            T AllReduce(T value);
        }

        /// <summary>
        /// An inclusive-scan implementation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The scan operation.</typeparam>
        readonly struct InclusiveScanImplementation<T, TScanOperation> : IScanImplementation<T, TScanOperation>
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            /// <summary cref="IGroupScan{T, TScanOperation}.Scan(T)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Scan(T value) => InclusiveScan<T, TScanOperation>(value);

            /// <summary cref="IGroupScan{T, TScanOperation}.Scan(T, out ScanBoundaries{T})"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Scan(T value, out ScanBoundaries<T> boundaries) =>
                InclusiveScanWithBoundaries<T, TScanOperation>(value, out boundaries);

            /// <summary cref="IScanImplementation{T, TScanOperation}.AllReduce(T)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T AllReduce(T value) =>
                AllReduce<T, TScanOperation>(value);
        }

        /// <summary>
        /// An exclusive-scan implementation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The scan operation.</typeparam>
        readonly struct ExclusiveScanImplementation<T, TScanOperation> : IScanImplementation<T, TScanOperation>
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            /// <summary cref="IGroupScan{T, TScanOperation}.Scan(T)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Scan(T value) => ExclusiveScan<T, TScanOperation>(value);

            /// <summary cref="IGroupScan{T, TScanOperation}.Scan(T, out ScanBoundaries{T})"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Scan(T value, out ScanBoundaries<T> boundaries) =>
                ExclusiveScanWithBoundaries<T, TScanOperation>(value, out boundaries);

            /// <summary cref="IScanImplementation{T, TScanOperation}.AllReduce(T)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T AllReduce(T value) =>
                AllReduce<T, TScanOperation>(value);
        }

        /// <summary>
        /// Computes the right tile boundary.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The scan-operation type.</typeparam>
        /// <typeparam name="TGroupScanImplementation">The group-scan implementation type.</typeparam>
        /// <param name="tileInfo">The current tile info.</param>
        /// <param name="input">The input view.</param>
        /// <returns>The resolved right boundary for all threads in the group.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T ComputeTileRightBoundary<T, TScanOperation, TGroupScanImplementation>(
            TileInfo<T> tileInfo,
            ArrayView<T> input)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
            where TGroupScanImplementation : struct, IScanImplementation<T, TScanOperation>
        {
            TScanOperation scanOperation = default;
            TGroupScanImplementation groupScan = default;

            T rightBoundary = tileInfo.StartIndex < tileInfo.MaxLength ?
                input[tileInfo.StartIndex] :
                scanOperation.Identity;

            // Perform a scan of all items in this group
            rightBoundary = groupScan.AllReduce(rightBoundary);

            // Perform a linear scan over all elements in the current tile
            for (int i = tileInfo.StartIndex + Group.DimX; i < tileInfo.EndIndex; i += Group.DimX)
            {
                var inputValue = i < tileInfo.MaxLength ? input[i] : scanOperation.Identity;

                var reduced = groupScan.AllReduce(inputValue);
                rightBoundary = scanOperation.Apply(rightBoundary, reduced);
            }
            return rightBoundary;
        }

        /// <summary>
        /// Computes a single scan within a single tile.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The scan-operation type.</typeparam>
        /// <typeparam name="TGroupScanImplementation">The group-scan implementation type.</typeparam>
        /// <param name="tileInfo">The current tile info.</param>
        /// <param name="input">The input view.</param>
        /// <param name="output">The output view.</param>
        /// <param name="leftBoundary">The left boundary (e.g. of the previous tile).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ComputeTileScan<T, TScanOperation, TGroupScanImplementation>(
            TileInfo<T> tileInfo,
            ArrayView<T> input,
            ArrayView<T> output,
            T leftBoundary)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
            where TGroupScanImplementation : struct, IScanImplementation<T, TScanOperation>
        {
            TScanOperation scanOperation = default;
            TGroupScanImplementation groupScan = default;

            // Fetch initial current value
            T current = tileInfo.StartIndex < tileInfo.MaxLength ?
                input[tileInfo.StartIndex] :
                scanOperation.Identity;

            // Perform a scan of all items in this group
            current = groupScan.Scan(current, out var localBoundaries);

            if (tileInfo.StartIndex < tileInfo.MaxLength)
                output[tileInfo.StartIndex] = scanOperation.Apply(leftBoundary, current);

            leftBoundary = scanOperation.Apply(leftBoundary, localBoundaries.RightBoundary);

            // Adjust all scan results according to the previously computed result
            for (int i = tileInfo.StartIndex + Group.DimX; i < tileInfo.EndIndex; i += Group.DimX)
            {
                var inputValue = i < tileInfo.MaxLength ? input[i] : scanOperation.Identity;

                var scanned = groupScan.Scan(inputValue, out localBoundaries);
                if (i < tileInfo.MaxLength)
                    output[i] = scanOperation.Apply(leftBoundary, scanned);

                leftBoundary = scanOperation.Apply(leftBoundary, localBoundaries.RightBoundary);
            }
        }

        #endregion

        #region Single-Group Scan

        /// <summary>
        /// Performs a scan operation within a single group only.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The scan operation.</typeparam>
        /// <typeparam name="TGroupScanImplementation">The actual group-scan implementation that provides the required group-level functionality.</typeparam>
        /// <param name="input">The input elements to scan.</param>
        /// <param name="output">The output view to store the scanned values.</param>
        internal static void SingleGroupScanKernel<T, TScanOperation, TGroupScanImplementation>(
            ArrayView<T> input,
            ArrayView<T> output)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
            where TGroupScanImplementation : struct, IScanImplementation<T, TScanOperation>
        {
            var tileInfo = new TileInfo<T>(input, XMath.DivRoundUp(input.Length, Group.DimX));

            TScanOperation scanOperation = default;

            ComputeTileScan<T, TScanOperation, TGroupScanImplementation>(
                tileInfo,
                input,
                output,
                scanOperation.Identity);
        }

        /// <summary>
        /// Creates a new single group scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The scan operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="kind">The scan kind.</param>
        /// <returns>The created scan operation.</returns>
        private static Scan<T> CreateSingleGroupScan<T, TScanOperation>(
            Accelerator accelerator,
            ScanKind kind)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            Action<AcceleratorStream, KernelConfig, ArrayView<T>, ArrayView<T>> kernel;
            if (kind == ScanKind.Inclusive)
                kernel = accelerator.LoadKernel<ArrayView<T>, ArrayView<T>>(
                    SingleGroupScanKernel<T, TScanOperation, InclusiveScanImplementation<T, TScanOperation>>);
            else
                kernel = accelerator.LoadKernel<ArrayView<T>, ArrayView<T>>(
                    SingleGroupScanKernel<T, TScanOperation, ExclusiveScanImplementation<T, TScanOperation>>);
            return (stream, input, output, temp) =>
            {
                if (!input.IsValid)
                    throw new ArgumentNullException(nameof(input));
                if (!output.IsValid)
                    throw new ArgumentNullException(nameof(output));
                kernel(stream, (1, accelerator.MaxNumThreadsPerGroup), input, output);
            };
        }

        #endregion

        #region Single-Pass Scan

        /// <summary>
        /// Performs a scan operation with a single pass.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The scan operation.</typeparam>
        /// <typeparam name="TGroupScanImplementation">The actual group-scan implementation that provides the required group-level functionality.</typeparam>
        /// <param name="input">The input elements to scan.</param>
        /// <param name="output">The output view to store the scanned values.</param>
        /// <param name="sequentialGroupExecutor">The sequential group executor to use.</param>
        /// <param name="boundaryValue">The boundary value target in global memory to share intermediate results.</param>
        /// <param name="numIterationsPerGroup">The number of iterations per group.</param>
        internal static void SinglePassScanKernel<T, TScanOperation, TGroupScanImplementation>(
            ArrayView<T> input,
            ArrayView<T> output,
            SequentialGroupExecutor sequentialGroupExecutor,
            VariableView<T> boundaryValue,
            Index1 numIterationsPerGroup)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
            where TGroupScanImplementation : struct, IScanImplementation<T, TScanOperation>
        {
            var tileInfo = new TileInfo<T>(input, numIterationsPerGroup);

            TScanOperation scanOperation = default;

            T leftBoundary = scanOperation.Identity;

            // Determine our right boundary and resolve our left boundary
            T rightBoundary = ComputeTileRightBoundary<T, TScanOperation, TGroupScanImplementation>(
                tileInfo,
                input);

            // Sync groups and wait for the current one to become active
            sequentialGroupExecutor.Wait();

            // Read the right boundary of the previous group (if possible)
            // This is our new left boundary (if required)
            if (Grid.IdxX > 0)
                leftBoundary = boundaryValue.Value;

            // Wait for all threads in the group to read the same boundary value
            Group.Barrier();

            // If we are the first thread in the group, update the boundary value for the next group
            if (Group.IsFirstThread)
                boundaryValue.Value = scanOperation.Apply(leftBoundary, rightBoundary);

            // Wait for all changes
            MemoryFence.DeviceLevel();
            Group.Barrier();

            sequentialGroupExecutor.Release();

            // Perform the final tile scan
            ComputeTileScan<T, TScanOperation, TGroupScanImplementation>(
                tileInfo,
                input,
                output,
                leftBoundary);
        }

        /// <summary>
        /// Computes the required number of elements of size <see cref="int"/>.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <returns>The required number of <see cref="int"/> elements in temporary memory.</returns>
        private static int ComputeNumIntElementsForSinglePassScan<T>()
            where T : unmanaged =>
            Interop.ComputeRelativeSizeOf<int, T>() + 1;

        /// <summary>
        /// Creates a new single pass scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The scan operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="kind">The scan kind.</param>
        /// <returns>The created scan operation.</returns>
        private static Scan<T> CreateSinglePassScan<T, TScanOperation>(
            Accelerator accelerator,
            ScanKind kind)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            var initializer = accelerator.CreateInitializer<int>();

            Action<
                AcceleratorStream,
                KernelConfig,
                ArrayView<T>,
                ArrayView<T>,
                SequentialGroupExecutor,
                VariableView<T>,
                Index1> kernel;
            if (kind == ScanKind.Inclusive)
                kernel = accelerator.LoadKernel<
                    ArrayView<T>,
                    ArrayView<T>,
                    SequentialGroupExecutor,
                    VariableView<T>,
                    Index1>(
                    SinglePassScanKernel<
                        T,
                        TScanOperation,
                        InclusiveScanImplementation<T,
                        TScanOperation>>);
            else
                kernel = accelerator.LoadKernel<
                    ArrayView<T>,
                    ArrayView<T>,
                    SequentialGroupExecutor,
                    VariableView<T>,
                    Index1>(
                    SinglePassScanKernel<
                        T,
                        TScanOperation,
                        ExclusiveScanImplementation<T,
                        TScanOperation>>);

            int numIntTElements = ComputeNumIntElementsForSinglePassScan<T>();

            return (stream, input, output, temp) =>
            {
                if (!input.IsValid)
                    throw new ArgumentNullException(nameof(input));
                if (!output.IsValid)
                    throw new ArgumentNullException(nameof(output));
                if (output.Length < input.Length)
                    throw new ArgumentOutOfRangeException(nameof(output));

                var viewManager = new TempViewManager(temp, nameof(temp));
                var executorView = viewManager.Allocate<int>();
                var tempView = viewManager.Allocate<T>();

                initializer(stream, temp.GetSubView(0, viewManager.NumInts), default);

                var extent = accelerator.ComputeGridStrideLoopExtent(
                    input.Length,
                    out int numIterationsPerGroup);

                kernel(
                    stream,
                    extent,
                    input,
                    output,
                    new SequentialGroupExecutor(executorView),
                    tempView,
                    numIterationsPerGroup);
            };
        }

        #endregion

        #region Multi-Pass Scan

        /// <summary>
        /// Performs the first pass in the scope of a multi-pass scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The scan operation.</typeparam>
        /// <typeparam name="TGroupScanImplementation">The actual group-scan implementation that provides the required group-level functionality.</typeparam>
        /// <param name="input">The input elements to scan.</param>
        /// <param name="rightBoundaries">The right boundaries to store.</param>
        /// <param name="numIterationsPerGroup">The number of iterations per group.</param>
        internal static void MultiPassScanKernel1<T, TScanOperation, TGroupScanImplementation>(
            ArrayView<T> input,
            ArrayView<T> rightBoundaries,
            Index1 numIterationsPerGroup)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
            where TGroupScanImplementation : struct, IScanImplementation<T, TScanOperation>
        {
            var tileInfo = new TileInfo<T>(input, numIterationsPerGroup);

            T rightBoundary = ComputeTileRightBoundary<T, TScanOperation, TGroupScanImplementation>(
                tileInfo,
                input);

            if (Group.IsFirstThread)
                rightBoundaries[Grid.IdxX] = rightBoundary;
        }

        /// <summary>
        /// Performs the second pass in the scope of a multi-pass scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The scan operation.</typeparam>
        /// <typeparam name="TGroupScanImplementation">The actual group-scan implementation that provides the required group-level functionality.</typeparam>
        /// <param name="input">The input elements to scan.</param>
        /// <param name="rightBoundaries">The right boundaries to use.</param>
        /// <param name="output">The scanned values.</param>
        /// <param name="numIterationsPerGroup">The number of iterations per group.</param>
        internal static void MultiPassScanKernel2<T, TScanOperation, TGroupScanImplementation>(
            ArrayView<T> input,
            ArrayView<T> rightBoundaries,
            ArrayView<T> output,
            Index1 numIterationsPerGroup)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
            where TGroupScanImplementation : struct, IScanImplementation<T, TScanOperation>
        {
            var tileInfo = new TileInfo<T>(input, numIterationsPerGroup);

            TScanOperation scanOperation = default;
            TGroupScanImplementation groupScan = default;

            T leftBoundary = scanOperation.Identity;

            if (Grid.IdxX > 0)
            {
                var localRightBoundary = Group.IdxX < rightBoundaries.Length ?
                    rightBoundaries[Group.IdxX] :
                    scanOperation.Identity;
                var scannedLeftBoundaries = groupScan.Scan(localRightBoundary);
                leftBoundary = Group.Broadcast(scannedLeftBoundaries, Grid.IdxX);
            }

            ComputeTileScan<T, TScanOperation, TGroupScanImplementation>(
                tileInfo,
                input,
                output,
                leftBoundary);
        }

        /// <summary>
        /// Creates a new multi pass scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The scan operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="kind">The scan kind.</param>
        /// <returns>The created scan operation.</returns>
        private static Scan<T> CreateMultiPassScan<T, TScanOperation>(
            Accelerator accelerator,
            ScanKind kind)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            var initializer = accelerator.CreateInitializer<T>();

            Action<AcceleratorStream, KernelConfig, ArrayView<T>, ArrayView<T>, Index1> pass1Kernel;
            Action<AcceleratorStream, KernelConfig, ArrayView<T>, ArrayView<T>, ArrayView<T>, Index1> pass2Kernel;
            if (kind == ScanKind.Inclusive)
            {
                pass1Kernel = accelerator.LoadKernel<ArrayView<T>, ArrayView<T>, Index1>(
                    MultiPassScanKernel1<T, TScanOperation, InclusiveScanImplementation<T, TScanOperation>>);
                pass2Kernel = accelerator.LoadKernel<ArrayView<T>, ArrayView<T>, ArrayView<T>, Index1>(
                    MultiPassScanKernel2<T, TScanOperation, InclusiveScanImplementation<T, TScanOperation>>);
            }
            else
            {
                pass1Kernel = accelerator.LoadKernel<ArrayView<T>, ArrayView<T>, Index1>(
                    MultiPassScanKernel1<T, TScanOperation, ExclusiveScanImplementation<T, TScanOperation>>);
                pass2Kernel = accelerator.LoadKernel<ArrayView<T>, ArrayView<T>, ArrayView<T>, Index1>(
                    MultiPassScanKernel2<T, TScanOperation, ExclusiveScanImplementation<T, TScanOperation>>);
            }

            return (stream, input, output, temp) =>
            {
                if (!input.IsValid)
                    throw new ArgumentNullException(nameof(input));
                if (!output.IsValid)
                    throw new ArgumentNullException(nameof(output));
                if (output.Length < input.Length)
                    throw new ArgumentOutOfRangeException(nameof(output));

                var (gridDim, groupDim) = accelerator.ComputeGridStrideLoopExtent(
                    input.Length,
                    out int numIterationsPerGroup);

                var viewManager = new TempViewManager(temp, nameof(temp));
                var tempView = viewManager.Allocate<T>(gridDim);

                TScanOperation scanOperation = default;
                initializer(stream, tempView, scanOperation.Identity);

                pass1Kernel(
                    stream,
                    (gridDim, groupDim),
                    input,
                    tempView,
                    numIterationsPerGroup);

                initializer(stream, tempView, scanOperation.Identity);
                pass2Kernel(
                    stream,
                    (gridDim, groupDim),
                    input,
                    tempView,
                    output,
                    numIterationsPerGroup);
            };
        }

        #endregion

        #region Scan

        /// <summary>
        /// Creates a new scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="kind">The scan kind.</param>
        /// <returns>The created scan handler.</returns>
        public static Scan<T> CreateScan<T, TScanOperation>(
            this Accelerator accelerator,
            ScanKind kind)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            return accelerator.AcceleratorType switch
            {
                // We use a single-grouped kernel
                AcceleratorType.CPU => CreateSingleGroupScan<T, TScanOperation>(accelerator, kind),
                AcceleratorType.OpenCL => CreateMultiPassScan<T, TScanOperation>(accelerator, kind),
                AcceleratorType.Cuda => CreateSinglePassScan<T, TScanOperation>(accelerator, kind),
                _ => throw new NotSupportedException(),
            };
        }

        /// <summary>
        /// Creates a new inclusive scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created inclusive scan handler.</returns>
        public static Scan<T> CreateInclusiveScan<T, TScanOperation>(
            this Accelerator accelerator)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            CreateScan<T, TScanOperation>(accelerator, ScanKind.Inclusive);

        /// <summary>
        /// Creates a new exclusive scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created exclusive scan handler.</returns>
        public static Scan<T> CreateExclusiveScan<T, TScanOperation>(
            this Accelerator accelerator)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            CreateScan<T, TScanOperation>(accelerator, ScanKind.Exclusive);

        /// <summary>
        /// Creates a new specialized scan provider that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created provider.</returns>
        public static ScanProvider CreateScanProvider(this Accelerator accelerator) =>
            new ScanProvider(accelerator);

        #endregion
    }
}
