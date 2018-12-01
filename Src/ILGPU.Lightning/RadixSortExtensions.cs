// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: RadixSortExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Implements a radix sort operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
    public interface IRadixSortMapper<T>
        where T : struct
    {
        /// <summary>
        /// Returns the number of radix sort bits.
        /// </summary>
        int NumRadixBits { get; }

        /// <summary>
        /// Converts the given value to a radix-sort compatible value.
        /// </summary>
        /// <param name="value">The value to map.</param>
        /// <param name="bitIndex">The bit index to check.</param>
        /// <returns>True, if the given bit index is true.</returns>
        bool ExtractRadixBits(T value, int bitIndex);
    }

    /// <summary>
    /// Represents the radix sort operation type.
    /// </summary>
    public enum RadixSortKind
    {
        /// <summary>
        /// An ascending sort operation.
        /// </summary>
        Ascending,

        /// <summary>
        /// A descending sort operation.
        /// </summary>
        Descending
    }

    /// <summary>
    /// A single threaded radix sort operation.
    /// </summary>
    /// <typeparam name="T">The underlying type of the radix sort operation.</typeparam>
    /// <typeparam name="TRadixSortMapper">The type of the radix sort operation.</typeparam>
    static class RadixSortImpl<T, TRadixSortMapper>
        where T : struct
        where TRadixSortMapper : IRadixSortMapper<T>
    {
        /// <summary>
        /// Represents a radix sort kernel method.
        /// </summary>
        public static readonly MethodInfo KernelMethod =
            typeof(RadixSortImpl<T, TRadixSortMapper>).GetMethod(
                nameof(RadixSortImpl<T, TRadixSortMapper>.Kernel),
                BindingFlags.NonPublic | BindingFlags.Static);

        internal static void Kernel(
            Index index,
            ArrayView<T> input,
            ArrayView<T> output,
            TRadixSortMapper mapper,
            ArrayView<Index> offsetCounter,
            ArrayView<Index> counter,
            int bitIdx,
            int trueOrFalse)
        {
            var stride = GridExtensions.GridStrideLoopStride;
            var offset = offsetCounter[0];

            var expectTrue = trueOrFalse == 1;
            for (var idx = index; idx < input.Length; idx += stride)
            {
                var element = input[idx];
                var extractedBit = mapper.ExtractRadixBits(element, bitIdx);
                if (extractedBit == expectTrue)
                {
                    // Append to the output buffer
                    var elementIdx = Atomic.Add(ref counter[0], Index.One);
                    output[elementIdx + offset] = element;
                }
            }
        }
    }

    #region Delegates

    /// <summary>
    /// Represents a radix sort operation using a shuffle and operation logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
    /// <typeparam name="IRadixSortMapper">The type of the radix sort operation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to sort.</param>
    /// <param name="output">The output view to store the sorted values.</param>
    /// <param name="temp">The temp view to store temporary results.</param>
    /// <param name="mapper">The radix sort mapper.</param>
    public delegate void RadixSort<T, IRadixSortMapper>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output,
        ArrayView<Index> temp,
        IRadixSortMapper mapper)
        where T : struct
        where IRadixSortMapper : struct, IRadixSortMapper<T>;

    /// <summary>
    /// Represents a radix sort operation using a shuffle and operation logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
    /// <typeparam name="IRadixSortMapper">The type of the radix sort operation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to sort.</param>
    /// <param name="output">The output view to store the sorted values.</param>
    /// <param name="mapper">The radix sort mapper.</param>
    public delegate void BufferedRadixSort<T, IRadixSortMapper>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output,
        IRadixSortMapper mapper)
        where T : struct
        where IRadixSortMapper : struct, IRadixSortMapper<T>;

    /// <summary>
    /// Represents a radix sort operation using a shuffle and operation logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to sort.</param>
    /// <param name="output">The output view to store the sorted values.</param>
    /// <param name="temp">The temp view to store temporary results.</param>
    public delegate void RadixSort<T>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output,
        ArrayView<Index> temp)
        where T : struct;

    /// <summary>
    /// Represents a radix sort operation using a shuffle and operation logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to sort.</param>
    /// <param name="output">The output view to store the sorted values.</param>
    public delegate void BufferedRadixSort<T>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output)
        where T : struct;

    #endregion

    /// <summary>
    /// Represents a radix-sort provider for a radix sort operation.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class RadixSortProvider : LightningObject
    {
        #region Instance

        private MemoryBufferCache bufferCache;

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
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="input">The input view.</param>
        /// <returns>The allocated temporary view.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ArrayView<Index> AllocateTempRadixSortView<T>(ArrayView<T> input)
            where T : struct
        {
            var tempSize = Accelerator.ComputeRadixSortTempStorageSize(input.Length);
            if (tempSize < 1)
                throw new ArgumentOutOfRangeException(nameof(input));
            return bufferCache.Allocate<Index>(tempSize);
        }

        /// <summary>
        /// Creates a new radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortMapper">The type of the radix sort mapper.</typeparam>
        /// <param name="kind">The radix sort kind.</param>
        /// <returns>The created radix sort handler.</returns>
        public BufferedRadixSort<T, TRadixSortMapper> CreateRadixSort<T, TRadixSortMapper>(RadixSortKind kind)
            where T : struct
            where TRadixSortMapper : struct, IRadixSortMapper<T>
        {
            var radixSort = Accelerator.CreateRadixSort<T, TRadixSortMapper>(kind);
            return (stream, input, output, mapper) =>
            {
                var tempView = AllocateTempRadixSortView(input);
                radixSort(stream, input, output, tempView, mapper);
            };
        }

        /// <summary>
        /// Creates a new radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortMapper">The type of the radix sort mapper.</typeparam>
        /// <param name="kind">The radix sort kind.</param>
        /// <param name="mapper">The radix sort mapper.</param>
        /// <returns>The created radix sort handler.</returns>
        public BufferedRadixSort<T> CreateRadixSort<T, TRadixSortMapper>(
            RadixSortKind kind,
            TRadixSortMapper mapper)
            where T : struct
            where TRadixSortMapper : struct, IRadixSortMapper<T>
        {
            var radixSort = Accelerator.CreateRadixSort<T, TRadixSortMapper>(kind, mapper);
            return (stream, input, output) =>
            {
                var tempView = AllocateTempRadixSortView(input);
                radixSort(stream, input, output, tempView);
            };
        }

        /// <summary>
        /// Creates a new ascending radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortMapper">The type of the radix sort mapper.</typeparam>
        /// <returns>The created radix sort handler.</returns>
        public BufferedRadixSort<T, TRadixSortMapper> CreateAscendingRadixSort<T, TRadixSortMapper>()
            where T : struct
            where TRadixSortMapper : struct, IRadixSortMapper<T> =>
            CreateRadixSort<T, TRadixSortMapper>(RadixSortKind.Ascending);

        /// <summary>
        /// Creates a new ascending radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortMapper">The type of the radix sort mapper.</typeparam>
        /// <param name="mapper">The radix sort mapper.</param>
        /// <returns>The created radix sort handler.</returns>
        public BufferedRadixSort<T> CreateAscendingRadixSort<T, TRadixSortMapper>(
            TRadixSortMapper mapper)
            where T : struct
            where TRadixSortMapper : struct, IRadixSortMapper<T> =>
            CreateRadixSort<T, TRadixSortMapper>(RadixSortKind.Ascending, mapper);

        /// <summary>
        /// Creates a new descending radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortMapper">The type of the radix sort mapper.</typeparam>
        /// <returns>The created radix sort handler.</returns>
        public BufferedRadixSort<T, TRadixSortMapper> CreateDescendingRadixSort<T, TRadixSortMapper>()
            where T : struct
            where TRadixSortMapper : struct, IRadixSortMapper<T> =>
            CreateRadixSort<T, TRadixSortMapper>(RadixSortKind.Descending);

        /// <summary>
        /// Creates a new descending radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortMapper">The type of the radix sort mapper.</typeparam>
        /// <param name="mapper">The radix sort mapper.</param>
        /// <returns>The created radix sort handler.</returns>
        public BufferedRadixSort<T> CreateDescendingRadixSort<T, TRadixSortMapper>(
            TRadixSortMapper mapper)
            where T : struct
            where TRadixSortMapper : struct, IRadixSortMapper<T> =>
            CreateRadixSort<T, TRadixSortMapper>(RadixSortKind.Descending, mapper);


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
    /// Contains extension methods for radix-sort operations.
    /// </summary>
    public static partial class RadixSortExtensions
    {
        #region RadixSort Helpers

        /// <summary>
        /// Computes the required number of temp-storage elements for a radix sort operation and the given data length.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="dataLength">The number of data elements to sort.</param>
        /// <returns>The required number of temp-storage elements.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Index ComputeRadixSortTempStorageSize(
            this Accelerator accelerator,
            Index dataLength) => 2;

        #endregion

        #region RadixSort

        /// <summary>
        /// Creates a new radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortMapper">The type of the radix sort mapper.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="kind">The radix sort kind.</param>
        /// <returns>The created radix sort handler.</returns>
        public static RadixSort<T, TRadixSortMapper> CreateRadixSort<T, TRadixSortMapper>(
            this Accelerator accelerator,
            RadixSortKind kind)
            where T : struct
            where TRadixSortMapper : struct, IRadixSortMapper<T>
        {
            var ascending = kind == RadixSortKind.Ascending;
            var initializer = accelerator.CreateInitializer<Index>();
            var radixSort = accelerator.LoadAutoGroupedKernel<
                Action<AcceleratorStream, Index, ArrayView<T>, ArrayView<T>, TRadixSortMapper, ArrayView<Index>, ArrayView<Index>, int, int>>(
                RadixSortImpl<T, TRadixSortMapper>.KernelMethod,
                out int groupSize, out int minGridSize);
            var minDataSize = groupSize * minGridSize;

            return (stream, input, output, counterView, mapper) =>
            {
                if (!input.IsValid)
                    throw new ArgumentNullException(nameof(input));
                if (!output.IsValid)
                    throw new ArgumentNullException(nameof(output));
                if (!counterView.IsValid)
                    throw new ArgumentNullException(nameof(counterView));
                var tempSize = ComputeRadixSortTempStorageSize(accelerator, input.Length);
                if (counterView.Length < tempSize)
                    throw new ArgumentOutOfRangeException(nameof(counterView));

                var dimension = Math.Min(minDataSize, input.Length);

                var leftCounterView = counterView.GetSubView(0, 1);
                var rightCounterView = counterView.GetSubView(1, 1);

                int leftTrue = ascending ? 0 : 1;
                int rightTrue = ascending ? 1 : 0;

                var endBitIdx = mapper.NumRadixBits;
                if ((endBitIdx & 1) != 0)
                    throw new ArgumentOutOfRangeException(nameof(mapper));

                for (int bitIdx = 0; bitIdx < endBitIdx; ++bitIdx)
                {
                    initializer(stream, counterView, Index.Zero);

                    // Sort to buckets
                    radixSort(stream, dimension, input, output, mapper, rightCounterView, leftCounterView, bitIdx, leftTrue);
                    radixSort(stream, dimension, input, output, mapper, leftCounterView, rightCounterView, bitIdx, rightTrue);

                    Utilities.Swap(ref input, ref output);
                }
            };
        }

        /// <summary>
        /// Creates a new radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortMapper">The type of the radix sort mapper.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="kind">The radix sort kind.</param>
        /// <param name="mapper">The radix sort mapper.</param>
        /// <returns>The created radix sort handler.</returns>
        public static RadixSort<T> CreateRadixSort<T, TRadixSortMapper>(
            this Accelerator accelerator,
            RadixSortKind kind,
            TRadixSortMapper mapper)
            where T : struct
            where TRadixSortMapper : struct, IRadixSortMapper<T>
        {
            var radixSort = CreateRadixSort<T, TRadixSortMapper>(accelerator, kind);
            return (stream, input, output, temp) =>
                radixSort(stream, input, output, temp, mapper);
        }

        /// <summary>
        /// Creates a new ascending radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortMapper">The type of the radix sort mapper.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created radix sort handler.</returns>
        public static RadixSort<T, TRadixSortMapper> CreateAscendingRadixSort<T, TRadixSortMapper>(
            this Accelerator accelerator)
            where T : struct
            where TRadixSortMapper : struct, IRadixSortMapper<T> =>
            CreateRadixSort<T, TRadixSortMapper>(accelerator, RadixSortKind.Ascending);

        /// <summary>
        /// Creates a new ascending radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortMapper">The type of the radix sort mapper.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="mapper">The radix sort mapper.</param>
        /// <returns>The created radix sort handler.</returns>
        public static RadixSort<T> CreateAscendingRadixSort<T, TRadixSortMapper>(
            this Accelerator accelerator,
            TRadixSortMapper mapper)
            where T : struct
            where TRadixSortMapper : struct, IRadixSortMapper<T> =>
            CreateRadixSort<T, TRadixSortMapper>(accelerator, RadixSortKind.Ascending, mapper);

        /// <summary>
        /// Creates a new descending radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortMapper">The type of the radix sort mapper.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created radix sort handler.</returns>
        public static RadixSort<T, TRadixSortMapper> CreateDescendingRadixSort<T, TRadixSortMapper>(
            this Accelerator accelerator)
            where T : struct
            where TRadixSortMapper : struct, IRadixSortMapper<T> =>
            CreateRadixSort<T, TRadixSortMapper>(accelerator, RadixSortKind.Descending);

        /// <summary>
        /// Creates a new descending radix sort operation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the sort operation.</typeparam>
        /// <typeparam name="TRadixSortMapper">The type of the radix sort mapper.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="mapper">The radix sort mapper.</param>
        /// <returns>The created radix sort handler.</returns>
        public static RadixSort<T> CreateDescendingRadixSort<T, TRadixSortMapper>(
            this Accelerator accelerator,
            TRadixSortMapper mapper)
            where T : struct
            where TRadixSortMapper : struct, IRadixSortMapper<T> =>
            CreateRadixSort<T, TRadixSortMapper>(accelerator, RadixSortKind.Descending, mapper);

        /// <summary>
        /// Creates a new specialized radix-sort provider that has its own cache.
        /// Note that the resulting provider has to be disposed manually.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created provider.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This is a construction method")]
        public static RadixSortProvider CreateRadixSortProvider(this Accelerator accelerator) =>
            new RadixSortProvider(accelerator);

        #endregion
    }
}
