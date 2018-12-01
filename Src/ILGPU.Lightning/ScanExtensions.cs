// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ScanExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------;

using ILGPU.Runtime;
using ILGPU.ScanOperations;
using ILGPU.ShuffleOperations;
using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Lightning
{
    #region Scan Implementation

    /// <summary>
    /// A single threaded scan operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
    /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
    static class SingleThreadedScanImpl<T, TScanOperation>
        where T : struct
        where TScanOperation : IScanOperation<T>
    {
        /// <summary>
        /// Represents an inclusive scan kernel.
        /// </summary>
        public static readonly MethodInfo InclusiveKernelMethod =
            typeof(SingleThreadedScanImpl<T, TScanOperation>).GetMethod(
                nameof(SingleThreadedScanImpl<T, TScanOperation>.InclusiveKernel),
                BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Represents an exclusive scan kernel.
        /// </summary>
        public static readonly MethodInfo ExclusiveKernelMethod =
            typeof(SingleThreadedScanImpl<T, TScanOperation>).GetMethod(
                nameof(SingleThreadedScanImpl<T, TScanOperation>.ExclusiveKernel),
                BindingFlags.NonPublic | BindingFlags.Static);

        internal static void InclusiveKernel(
            Index index,
            ArrayView<T> input,
            ArrayView<T> output,
            TScanOperation scanOperation)
        {
            output[0] = input[0];
            for (int i = 1, e = input.Length; i < e; ++i)
                output[i] = scanOperation.Apply(output[i - 1], input[i]);
        }

        internal static void ExclusiveKernel(
            Index index,
            ArrayView<T> input,
            ArrayView<T> output,
            TScanOperation scanOperation)
        {
            output[0] = scanOperation.Identity;
            for (int i = 1, e = input.Length; i < e; ++i)
                output[i] = scanOperation.Apply(output[i - 1], input[i - 1]);
        }
    }

    /// <summary>
    /// A scan operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
    /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
    /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
    static class ScanImpl<T, TShuffleDown, TScanOperation>
        where T : struct
        where TShuffleDown : IShuffleDown<T>
        where TScanOperation : IScanOperation<T>
    {
        /// <summary>
        /// Represents a scan kernel.
        /// </summary>
        public static readonly MethodInfo KernelMethod =
            typeof(ScanImpl<T, TShuffleDown, TScanOperation>).GetMethod(
                nameof(Kernel),
                BindingFlags.NonPublic | BindingFlags.Static);

        private static void Kernel(
            GroupedIndex index,
            ArrayView<T> input,
            ArrayView<T> output,
            TShuffleDown shuffleDown,
            TScanOperation scanOperation)
        {
            // TODO: add final scan implementation
        }
    }

    #endregion

    #region Scan Delegates

    /// <summary>
    /// Represents a scan operation using a shuffle and operation logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to scan.</param>
    /// <param name="output">The output view to store the scanned values.</param>
    /// <param name="temp">The temp view to store temporary results.</param>
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes", Justification = "Required to realize a generic implementation of a reduction")]
    public delegate void Scan<T>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output,
        ArrayView<T> temp)
        where T : struct;

    /// <summary>
    /// Represents a scan operation using a shuffle and operation logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to scan.</param>
    /// <param name="output">The output view to store the scanned values.</param>
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes", Justification = "Required to realize a generic implementation of a reduction")]
    public delegate void BufferedScan<T>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output)
        where T : struct;

    /// <summary>
    /// Represents a scan operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
    /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
    /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to scan.</param>
    /// <param name="output">The output view to store the scanned values.</param>
    /// <param name="temp">The temp view to store temporary results.</param>
    /// <param name="shuffleDown">The shuffle logic.</param>
    /// <param name="scanOperation">The scan operation logic.</param>
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes", Justification = "Required to realize a generic implementation of a reduction")]
    public delegate void Scan<T, TShuffleDown, TScanOperation>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output,
        ArrayView<T> temp,
        TShuffleDown shuffleDown,
        TScanOperation scanOperation)
        where T : struct
        where TShuffleDown : struct, IShuffleDown<T>
        where TScanOperation : struct, IScanOperation<T>;

    /// <summary>
    /// Represents a scan operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
    /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
    /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to scan.</param>
    /// <param name="output">The output view to store the scanned values.</param>
    /// <param name="shuffleDown">The shuffle logic.</param>
    /// <param name="scanOperation">The scan operation logic.</param>
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes", Justification = "Required to realize a generic implementation of a reduction")]
    public delegate void BufferedScan<T, TShuffleDown, TScanOperation>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output,
        TShuffleDown shuffleDown,
        TScanOperation scanOperation)
        where T : struct
        where TShuffleDown : struct, IShuffleDown<T>
        where TScanOperation : struct, IScanOperation<T>;

    #endregion

    /// <summary>
    /// Represents a scan provider for a scan operation.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class ScanProvider : LightningObject
    {
        #region Instance

        private MemoryBufferCache bufferCache;

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
        private ArrayView<T> AllocateTempScanView<T>(ArrayView<T> input)
            where T : struct
        {
            var tempSize = Accelerator.ComputeScanTempStorageSize(input.Length);
            if (tempSize < 1)
                throw new ArgumentOutOfRangeException(nameof(input));
            return bufferCache.Allocate<T>(tempSize);
        }

        /// <summary>
        /// Creates a new buffered scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="kind">The scan kind.</param>
        /// <returns>The created scan handler.</returns>
        public BufferedScan<T, TShuffleDown, TScanOperation> CreateScan<T, TShuffleDown, TScanOperation>(
            ScanKind kind)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TScanOperation : struct, IScanOperation<T>
        {
            var scan = Accelerator.CreateScan<T, TShuffleDown, TScanOperation>(kind);
            return (stream, input, output, shuffleDown, scanOperation) =>
            {
                var tempView = AllocateTempScanView(input);
                scan(stream, input, output, tempView, shuffleDown, scanOperation);
            };
        }

        /// <summary>
        /// Creates a new buffered scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="kind">The scan kind.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="scanOperation">The scan logic.</param>
        /// <returns>The created scan handler.</returns>
        public BufferedScan<T> CreateScan<T, TShuffleDown, TScanOperation>(
            ScanKind kind,
            TShuffleDown shuffleDown,
            TScanOperation scanOperation)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TScanOperation : struct, IScanOperation<T>
        {
            var scan = Accelerator.CreateScan<T, TShuffleDown, TScanOperation>(kind, shuffleDown, scanOperation);
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
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <returns>The created inclusive scan handler.</returns>
        public BufferedScan<T, TShuffleDown, TScanOperation> CreateInclusiveScan<T, TShuffleDown, TScanOperation>()
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TScanOperation : struct, IScanOperation<T> =>
            CreateScan<T, TShuffleDown, TScanOperation>(ScanKind.Inclusive);

        /// <summary>
        /// Creates a new buffered inclusive scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="scanOperation">The scan logic.</param>
        /// <returns>The created inclusive scan handler.</returns>
        public BufferedScan<T> CreateInclusiveScan<T, TShuffleDown, TScanOperation>(
            TShuffleDown shuffleDown,
            TScanOperation scanOperation)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TScanOperation : struct, IScanOperation<T> =>
            CreateScan<T, TShuffleDown, TScanOperation>(ScanKind.Inclusive, shuffleDown, scanOperation);

        /// <summary>
        /// Creates a new buffered exclusive scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <returns>The created exclusive scan handler.</returns>
        public BufferedScan<T, TShuffleDown, TScanOperation> CreateExclusiveScan<T, TShuffleDown, TScanOperation>()
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TScanOperation : struct, IScanOperation<T> =>
            CreateScan<T, TShuffleDown, TScanOperation>(ScanKind.Exclusive);

        /// <summary>
        /// Creates a new buffered exclusive scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="scanOperation">The scan logic.</param>
        /// <returns>The created exclusive scan handler.</returns>
        public BufferedScan<T> CreateExclusiveScan<T, TShuffleDown, TScanOperation>(
            TShuffleDown shuffleDown,
            TScanOperation scanOperation)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TScanOperation : struct, IScanOperation<T> =>
            CreateScan<T, TShuffleDown, TScanOperation>(ScanKind.Exclusive, shuffleDown, scanOperation);

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "bufferCache", Justification = "Dispose method will be invoked by a helper method")]
        protected override void Dispose(bool disposing)
        {
            Dispose(ref bufferCache);
        }

        #endregion
    }

    /// <summary>
    /// Contains extension methods for scan operations.
    /// </summary>
    public static class ScanExtensions
    {
        #region Scan Helpers

        /// <summary>
        /// Computes the required number of temp-storage elements for a scan operation and the given data length.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="dataLength">The number of data elements to scan.</param>
        /// <returns>The required number of temp-storage elements.</returns>
        public static Index ComputeScanTempStorageSize(this Accelerator accelerator,  Index dataLength)
        {
            // TODO: implement proper approximation
            return 1;
        }

        #endregion

        #region Scan

        /// <summary>
        /// Creates a new scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="kind">The scan kind.</param>
        /// <returns>The created scan handler.</returns>
        public static Scan<T, TShuffleDown, TScanOperation> CreateScan<T, TShuffleDown, TScanOperation>(
            this Accelerator accelerator,
            ScanKind kind)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TScanOperation : struct, IScanOperation<T>
        {
            var inclusive = kind == ScanKind.Inclusive;
            if (accelerator.MaxNumThreadsPerGroup < 2)
            {
                var scan = accelerator.LoadAutoGroupedKernel<
                    Action<AcceleratorStream, Index, ArrayView<T>, ArrayView<T>, TScanOperation>>(
                    inclusive ?
                        SingleThreadedScanImpl<T, TScanOperation>.InclusiveKernelMethod :
                        SingleThreadedScanImpl<T, TScanOperation>.ExclusiveKernelMethod,
                    out int groupSize, out int minGridSize);
                var minDataSize = groupSize * minGridSize;
                return (stream, input, output, temp, shuffleDown, scanOperation) =>
                {
                    if (!input.IsValid)
                        throw new ArgumentNullException(nameof(input));
                    if (!output.IsValid)
                        throw new ArgumentNullException(nameof(output));
                    var dimension = Math.Min(minDataSize, input.Length);
                    scan(stream, dimension, input, output, scanOperation);
                };
            }
            else
            {
                // TODO: add final scan implementation
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Creates a new scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="kind">The scan kind.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="scanOperation">The scan logic.</param>
        /// <returns>The created scan handler.</returns>
        public static Scan<T> CreateScan<T, TShuffleDown, TScanOperation>(
            this Accelerator accelerator,
            ScanKind kind,
            TShuffleDown shuffleDown,
            TScanOperation scanOperation)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TScanOperation : struct, IScanOperation<T>
        {
            var scan = CreateScan<T, TShuffleDown, TScanOperation>(accelerator, kind);
            return (stream, input, output, temp) =>
                scan(stream, input, output, temp, shuffleDown, scanOperation);
        }

        /// <summary>
        /// Creates a new inclusive scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created inclusive scan handler.</returns>
        public static Scan<T, TShuffleDown, TScanOperation> CreateInclusiveScan<T, TShuffleDown, TScanOperation>(
            this Accelerator accelerator)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TScanOperation : struct, IScanOperation<T> =>
            CreateScan<T, TShuffleDown, TScanOperation>(accelerator, ScanKind.Inclusive);

        /// <summary>
        /// Creates a new inclusive scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="scanOperation">The scan logic.</param>
        /// <returns>The created inclusive scan handler.</returns>
        public static Scan<T> CreateInclusiveScan<T, TShuffleDown, TScanOperation>(
            this Accelerator accelerator,
            TShuffleDown shuffleDown,
            TScanOperation scanOperation)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TScanOperation : struct, IScanOperation<T> =>
            CreateScan<T, TShuffleDown, TScanOperation>(accelerator, ScanKind.Inclusive, shuffleDown, scanOperation);

        /// <summary>
        /// Creates a new exclusive scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created exclusive scan handler.</returns>
        public static Scan<T, TShuffleDown, TScanOperation> CreateExclusiveScan<T, TShuffleDown, TScanOperation>(
            this Accelerator accelerator)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TScanOperation : struct, IScanOperation<T> =>
            CreateScan<T, TShuffleDown, TScanOperation>(accelerator, ScanKind.Exclusive);

        /// <summary>
        /// Creates a new exclusive scan operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the scan operation.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TScanOperation">The type of the scan operation.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="scanOperation">The scan logic.</param>
        /// <returns>The created exclusive scan handler.</returns>
        public static Scan<T> CreateExclusiveScan<T, TShuffleDown, TScanOperation>(
            this Accelerator accelerator,
            TShuffleDown shuffleDown,
            TScanOperation scanOperation)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TScanOperation : struct, IScanOperation<T> =>
            CreateScan<T, TShuffleDown, TScanOperation>(accelerator, ScanKind.Exclusive, shuffleDown, scanOperation);

        /// <summary>
        /// Creates a new specialized scan provider that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created provider.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This is a construction method")]
        public static ScanProvider CreateScanProvider(this Accelerator accelerator) =>
            new ScanProvider(accelerator);

        #endregion
    }
}
