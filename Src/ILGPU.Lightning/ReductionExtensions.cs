// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ReductionExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.ReductionOperations;
using ILGPU.Runtime;
using ILGPU.ShuffleOperations;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;

namespace ILGPU.Lightning
{
    /// <summary>
    /// Represents an abstract interface for a atomic value reduction.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    public interface IAtomicReduction<T> : IReduction<T>
        where T : struct
    {
        /// <summary>
        /// Performs an atomic reduction of the form target = AtomicUpdate(target.Value, value).
        /// </summary>
        /// <param name="target">The target address to update.</param>
        /// <param name="value">The value.</param>
        void AtomicReduce(VariableView<T> target, T value);
    }

    #region Reduction Implementation

    /// <summary>
    /// Represents an abstract finalizer for reductions on the GPU.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    interface IReductionFinalizer<T, TReduction>
        where T : struct
        where TReduction : struct, IReduction<T>
    {
        /// <summary>
        /// Finalizes a reduction operation.
        /// </summary>
        /// <param name="index">The grouped thread index.</param>
        /// <param name="output">The output view.</param>
        /// <param name="reducedValue">The finally reduced value.</param>
        /// <param name="reduction">The reduction logic.</param>
        void Finalize(
            GroupedIndex index,
            ArrayView<T> output,
            T reducedValue,
            TReduction reduction);
    }

    /// <summary>
    /// Implements a reduction finalizer for a non-atomic two-pass reduction.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    struct DefaultReductionFinalizer<T, TReduction> : IReductionFinalizer<T, TReduction>
        where T : struct
        where TReduction : struct, IReduction<T>
    {
        /// <summary cref="IReductionFinalizer{T, TReduction}.Finalize(GroupedIndex, ArrayView{T}, T, TReduction)"/>
        public void Finalize(
            GroupedIndex index,
            ArrayView<T> output,
            T reducedValue,
            TReduction reduction)
        {
            if (index.GroupIdx.IsFirst)
                output[index.GridIdx] = reducedValue;
        }
    }

    /// <summary>
    /// Implements an atomic reduction finalizer.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    struct AtomicReductionFinalizer<T, TReduction> : IReductionFinalizer<T, TReduction>
        where T : struct
        where TReduction : struct, IAtomicReduction<T>
    {
        /// <summary cref="IReductionFinalizer{T, TReduction}.Finalize(GroupedIndex, ArrayView{T}, T, TReduction)"/>
        public void Finalize(
            GroupedIndex index,
            ArrayView<T> output,
            T reducedValue,
            TReduction reduction)
        {
            if (index.GroupIdx.IsFirst)
                reduction.AtomicReduce(output.GetVariableView(), reducedValue);
        }
    }

    /// <summary>
    /// Implements a reduction algorithm based on the one from:
    /// https://devblogs.nvidia.com/parallelforall/faster-parallel-reductions-kepler/
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <typeparam name="TReductionFinalizer">The type of the reduction finalizer.</typeparam>
    static class ReductionImpl<T, TShuffleDown, TReduction, TReductionFinalizer>
        where T : struct
        where TShuffleDown : struct, IShuffleDown<T>
        where TReduction : struct, IReduction<T>
        where TReductionFinalizer : struct, IReductionFinalizer<T, TReduction>
    {
        /// <summary>
        /// Represents a reduction kernel.
        /// </summary>
        public static readonly MethodInfo KernelMethod =
            typeof(ReductionImpl<T, TShuffleDown, TReduction, TReductionFinalizer>).GetMethod(
                nameof(Kernel),
                BindingFlags.NonPublic | BindingFlags.Static);

        private static void Kernel(
            GroupedIndex index,
            ArrayView<T> input,
            ArrayView<T> output,
            TShuffleDown shuffleDown,
            TReduction reduction,

            [SharedMemory(32)]
            ArrayView<T> sharedMemory)
        {
            var stride = GridExtensions.GridStrideLoopStride;
            var reduced = reduction.NeutralElement;
            for (var idx = index.ComputeGlobalIndex(); idx < input.Length; idx += stride)
                reduced = reduction.Reduce(reduced, input[idx]);

            reduced = GroupExtensions.Reduce(
                index.GroupIdx,
                reduced,
                shuffleDown,
                reduction,
                sharedMemory);

            var finalizer = default(TReductionFinalizer);
            finalizer.Finalize(index, output, reduced, reduction);
        }
    }

    #endregion

    #region Reduction Delegates

    /// <summary>
    /// Represents a reduction using an atomic reduction logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <param name="output">The output view to store the reduced value.</param>
    public delegate void AtomicReduction<T>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output)
        where T : struct;

    /// <summary>
    /// Represents a reduction using an atomic reduction logic.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TShuffleDown"></typeparam>
    /// <typeparam name="TReduction"></typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <param name="output">The output view to store the reduced value.</param>
    /// <param name="shuffleDown">The shuffle logic.</param>
    /// <param name="reduction">The reduction logic.</param>
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes", Justification = "Required to realize a generic implementation of a reduction")]
    public delegate void AtomicReduction<T, TShuffleDown, TReduction>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output,
        TShuffleDown shuffleDown,
        TReduction reduction)
        where T : struct
        where TShuffleDown : struct, IShuffleDown<T>
        where TReduction : struct, IAtomicReduction<T>;

    /// <summary>
    /// Represents a reduction using a reduction logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <param name="output">The output view to store the reduced value.</param>
    /// <param name="temp">The temp view to store temporary results.</param>
    public delegate void Reduction<T>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output,
        ArrayView<T> temp)
        where T : struct;

    /// <summary>
    /// Represents a reduction using a reduction logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <param name="output">The output view to store the reduced value.</param>
    /// <param name="temp">The temp view to store temporary results.</param>
    /// <param name="shuffleDown">The shuffle logic.</param>
    /// <param name="reduction">The reduction logic.</param>
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes", Justification = "Required to realize a generic implementation of a reduction")]
    public delegate void Reduction<T, TShuffleDown, TReduction>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output,
        ArrayView<T> temp,
        TShuffleDown shuffleDown,
        TReduction reduction)
        where T : struct
        where TShuffleDown : struct, IShuffleDown<T>
        where TReduction : struct, IReduction<T>;

    #endregion

    /// <summary>
    /// Reduction functionality for accelerators.
    /// </summary>
    public static class ReductionExtensions
    {
        #region Reduction Helpers

        /// <summary>
        /// Computes a grouped reduction dimension for reduction-kernel dispatch.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="dataLength">The number of data elements to reduce.</param>
        /// <returns>The grouped reduction dimension for reduction-kernel dispatch.</returns>
        private static GroupedIndex ComputeReductionDimension(Accelerator accelerator, Index dataLength)
        {
            var warpSize = accelerator.WarpSize;
            var groupSize = Math.Max(warpSize, (accelerator.MaxNumThreadsPerGroup / warpSize) * warpSize);
            if (groupSize < 2)
                throw new NotSupportedException("The given accelerator does not support reductions");
            var gridSize = Math.Min((dataLength + groupSize - 1) / groupSize, groupSize);
            return new GroupedIndex(gridSize, groupSize);
        }

        /// <summary>
        /// Computes the required number of temp-storage elements for the a reduction and the given data length.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="dataLength">The number of data elements to reduce.</param>
        /// <returns>The required number of temp-storage elements.</returns>
        public static Index ComputeReductionTempStorageSize(this Accelerator accelerator, Index dataLength)
        {
            return ComputeReductionDimension(accelerator, dataLength).GroupIdx;
        }

        #endregion

        #region Atomic Reduction

        /// <summary>
        /// Creates a new instance of an atomic reduction handler.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created reduction handler.</returns>
        public static AtomicReduction<T, TShuffleDown, TReduction> CreateAtomicReduction<T, TShuffleDown, TReduction>(
            this Accelerator accelerator)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            var kernel = accelerator.LoadKernel<Action<AcceleratorStream, GroupedIndex, ArrayView<T>, ArrayView<T>, TShuffleDown, TReduction>>(
                ReductionImpl<T, TShuffleDown, TReduction, AtomicReductionFinalizer<T, TReduction>>.KernelMethod);
            var initializer = accelerator.CreateInitializer<T>();
            return (stream, input, output, shuffleDown, reduction) =>
            {
                if (!input.IsValid)
                    throw new ArgumentNullException(nameof(input));
                if (input.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(input));
                if (!output.IsValid)
                    throw new ArgumentNullException(nameof(output));
                if (output.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(output));
                var dimension = ComputeReductionDimension(accelerator, input.Length);
                initializer(stream, output, reduction.NeutralElement);
                kernel(stream, dimension, input, output, shuffleDown, reduction);
            };
        }

        /// <summary>
        /// Creates a new instance of an atomic reduction handler using the provided
        /// shuffle-down and reduction logics.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>The created reduction handler.</returns>
        public static AtomicReduction<T> CreateAtomicReduction<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            var reductionDel = accelerator.CreateAtomicReduction<T, TShuffleDown, TReduction>();
            return (stream, input, output) =>
            {
                reductionDel(stream, input, output, shuffleDown, reduction);
            };
        }

        /// <summary>
        /// Performs a reduction using an atomic reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        public static void AtomicReduce<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input,
            ArrayView<T> output,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            accelerator.CreateAtomicReduction<T, TShuffleDown, TReduction>()(
                stream,
                input,
                output,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using an atomic reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        public static void AtomicReduce<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            ArrayView<T> input,
            ArrayView<T> output,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            accelerator.AtomicReduce(
                accelerator.DefaultStream,
                input,
                output,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using an atomic reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        public static T AtomicReduce<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            var output = accelerator.MemoryCache.Allocate<T>(1);
            accelerator.AtomicReduce(
                stream,
                input,
                output,
                shuffleDown,
                reduction);
            stream.Synchronize();
            accelerator.MemoryCache.CopyTo(out T result, 0);
            return result;
        }

        /// <summary>
        /// Performs a reduction using an atomic reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        public static T AtomicReduce<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            return accelerator.AtomicReduce(
                accelerator.DefaultStream,
                input,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using an atomic reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        public static Task<T> AtomicReduceAsync<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            return Task.Run(() =>
            {
                return accelerator.AtomicReduce(
                    stream,
                    input,
                    shuffleDown,
                    reduction);
            });
        }

        /// <summary>
        /// Performs a reduction using an atomic reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        public static Task<T> AtomicReduceAsync<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IAtomicReduction<T>
        {
            return accelerator.AtomicReduceAsync(
                accelerator.DefaultStream,
                input,
                shuffleDown,
                reduction);
        }

        #endregion

        #region Reduction

        /// <summary>
        /// Creates a new instance of a atomic reduction handler.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created reduction handler.</returns>
        public static Reduction<T, TShuffleDown, TReduction> CreateReduction<T, TShuffleDown, TReduction>(
            this Accelerator accelerator)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            var kernel = accelerator.LoadKernel<Action<AcceleratorStream, GroupedIndex, ArrayView<T>, ArrayView<T>, TShuffleDown, TReduction>>(
                ReductionImpl<T, TShuffleDown, TReduction, DefaultReductionFinalizer<T, TReduction>>.KernelMethod);
            var initializer = accelerator.CreateInitializer<T>();
            return (stream, input, output, temp, shuffleDown, reduction) =>
            {
                if (!input.IsValid)
                    throw new ArgumentNullException(nameof(input));
                if (input.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(input));
                if (!output.IsValid)
                    throw new ArgumentNullException(nameof(output));
                if (output.Length < 1)
                    throw new ArgumentOutOfRangeException(nameof(output));
                if (!temp.IsValid)
                    throw new ArgumentNullException(nameof(temp));
                var dimension = ComputeReductionDimension(accelerator, input.Length);
                if (temp.Length < dimension.GroupIdx)
                    throw new ArgumentOutOfRangeException(nameof(temp), $"Temp space must be at least {dimension.GroupIdx} elements large");
                initializer(stream, temp, reduction.NeutralElement);
                kernel(stream, dimension, input, temp, shuffleDown, reduction);
                kernel(stream, new GroupedIndex(1, dimension.GroupIdx), temp, output, shuffleDown, reduction);
            };
        }

        /// <summary>
        /// Creates a new instance of a atomic reduction handler.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <returns>The created reduction handler.</returns>
        public static Reduction<T> CreateReduction<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            var reductionDel = accelerator.CreateReduction<T, TShuffleDown, TReduction>();
            return (stream, input, output, temp) =>
            {
                reductionDel(stream, input, output, temp, shuffleDown, reduction);
            };
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        public static void Reduce<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input,
            ArrayView<T> output,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            if (!input.IsValid)
                throw new ArgumentNullException(nameof(input));
            if (input.Length < 1)
                throw new ArgumentOutOfRangeException(nameof(input));
            var tempStorageSize = accelerator.ComputeReductionTempStorageSize(input.Length);
            var temp = accelerator.MemoryCache.Allocate<T>(tempStorageSize);
            accelerator.CreateReduction<T, TShuffleDown, TReduction>()(
                stream,
                input,
                output,
                temp,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        public static void Reduce<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            ArrayView<T> input,
            ArrayView<T> output,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            accelerator.Reduce(
                accelerator.DefaultStream,
                input,
                output,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        /// <param name="temp">The temporary view to store the temporary values.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        public static void Reduce<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input,
            ArrayView<T> output,
            ArrayView<T> temp,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            accelerator.CreateReduction<T, TShuffleDown, TReduction>()(
                stream,
                input,
                output,
                temp,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        /// <param name="temp">The temporary view to store the temporary values.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        public static void Reduce<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            ArrayView<T> input,
            ArrayView<T> output,
            ArrayView<T> temp,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            accelerator.Reduce(
                accelerator.DefaultStream,
                input,
                output,
                temp,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        public static T Reduce<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            if (!input.IsValid)
                throw new ArgumentNullException(nameof(input));
            if (input.Length < 1)
                throw new ArgumentOutOfRangeException(nameof(input));
            var tempStorageSize = accelerator.ComputeReductionTempStorageSize(input.Length);
            var storage = accelerator.MemoryCache.Allocate<T>(tempStorageSize + 1);
            var output = storage.GetSubView(0, 1);
            var temp = storage.GetSubView(1);
            accelerator.Reduce(
                stream,
                input,
                output,
                temp,
                shuffleDown,
                reduction);
            stream.Synchronize();
            accelerator.MemoryCache.CopyTo(out T result, 0);
            return result;
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        public static T Reduce<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            return accelerator.Reduce(
                accelerator.DefaultStream,
                input,
                shuffleDown,
                reduction);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        public static Task<T> ReduceAsync<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            return Task.Run(() =>
            {
                return accelerator.Reduce(
                    stream,
                    input,
                    shuffleDown,
                    reduction);
            });
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="shuffleDown">The shuffle logic.</param>
        /// <param name="reduction">The reduction logic.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        public static Task<T> ReduceAsync<T, TShuffleDown, TReduction>(
            this Accelerator accelerator,
            ArrayView<T> input,
            TShuffleDown shuffleDown,
            TReduction reduction)
            where T : struct
            where TShuffleDown : struct, IShuffleDown<T>
            where TReduction : struct, IReduction<T>
        {
            return accelerator.ReduceAsync(
                accelerator.DefaultStream,
                input,
                shuffleDown,
                reduction);
        }

        #endregion
    }
}
