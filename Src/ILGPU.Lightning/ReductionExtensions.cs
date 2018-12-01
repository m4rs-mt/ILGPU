// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                Copyright (c) 2017-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ReductionExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.ShuffleOperations;
using ILGPU.ReductionOperations;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ILGPU.Lightning
{
    #region Reduction Implementation

    /// <summary>
    /// Implements a fast reduction algorithm using warp-reductions, shared memory and atomics.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <typeparam name="TShuffleDown">The type of the shuffle logic.</typeparam>
    /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
    static class ReductionImpl<T, TShuffleDown, TReduction>
        where T : struct
        where TShuffleDown : struct, IShuffleDown<T>
        where TReduction : struct, IAtomicReduction<T>
    {
        /// <summary>
        /// Represents a reduction kernel.
        /// </summary>
        public static readonly MethodInfo KernelMethod =
            typeof(ReductionImpl<T, TShuffleDown, TReduction>).GetMethod(
                nameof(Kernel),
                BindingFlags.NonPublic | BindingFlags.Static);

        internal static void Kernel(
            GroupedIndex index,
            ArrayView<T> input,
            ArrayView<T> output,
            TShuffleDown shuffleDown,
            TReduction reduction)
        {
            var stride = GridExtensions.GridStrideLoopStride;
            var reduced = reduction.NeutralElement;
            for (var idx = index.ComputeGlobalIndex(); idx < input.Length; idx += stride)
                reduced = reduction.Reduce(reduced, input[idx]);

            reduced = GroupExtensions.FirstWarpReduce(
                index.GroupIdx,
                reduced,
                shuffleDown,
                reduction);

            if (index.GroupIdx.IsFirst)
                reduction.AtomicReduce(ref output[0], reduced);
        }
    }

    #endregion

    #region Reduction Delegates

    /// <summary>
    /// Represents a reduction using a reduction logic.
    /// </summary>
    /// <typeparam name="T">The underlying type of the reduction.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <param name="output">The output view to store the reduced value.</param>
    public delegate void Reduction<T>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output)
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
    /// <param name="shuffleDown">The shuffle logic.</param>
    /// <param name="reduction">The reduction logic.</param>
    [SuppressMessage("Microsoft.Design", "CA1005:AvoidExcessiveParametersOnGenericTypes", Justification = "Required to realize a generic implementation of a reduction")]
    public delegate void Reduction<T, TShuffleDown, TReduction>(
        AcceleratorStream stream,
        ArrayView<T> input,
        ArrayView<T> output,
        TShuffleDown shuffleDown,
        TReduction reduction)
        where T : struct
        where TShuffleDown : struct, IShuffleDown<T>
        where TReduction : struct, IAtomicReduction<T>;

    #endregion

    /// <summary>
    /// Reduction functionality for accelerators.
    /// </summary>
    public static class ReductionExtensions
    {
        /// <summary>
        /// Computes a group size for reduction-kernel dispatch.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The grouped reduction dimension for reduction-kernel dispatch.</returns>
        private static Index ComputeReductionGroupSize(Accelerator accelerator)
        {
            var warpSize = accelerator.WarpSize;
            return Math.Max(warpSize, (accelerator.MaxNumThreadsPerGroup / warpSize) * warpSize);
        }

        /// <summary>
        /// Computes a grouped reduction dimension for reduction-kernel dispatch.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="dataLength">The number of data elements to reduce.</param>
        /// <returns>The grouped reduction dimension for reduction-kernel dispatch.</returns>
        private static GroupedIndex ComputeReductionDimension(Accelerator accelerator, Index dataLength)
        {
            var groupSize = ComputeReductionGroupSize(accelerator);
            var gridSize = Math.Min((dataLength + groupSize - 1) / groupSize, groupSize);
            return new GroupedIndex(gridSize, groupSize);
        }

        /// <summary>
        /// Verifies reduction arguments.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void VerifyAtomicReductionArguments<T>(
            ArrayView<T> input,
            ArrayView<T> output)
            where T : struct
        {
            if (!input.IsValid)
                throw new ArgumentNullException(nameof(input));
            if (!output.IsValid)
                throw new ArgumentNullException(nameof(output));
        }

        /// <summary>
        /// Creates a new instance of a reduction handler.
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
            where TReduction : struct, IAtomicReduction<T>
        {
            var initializer = accelerator.CreateInitializer<T>();
            var kernel = accelerator.LoadKernel<Action<AcceleratorStream, GroupedIndex, ArrayView<T>, ArrayView<T>, TShuffleDown, TReduction>>(
                ReductionImpl<T, TShuffleDown, TReduction>.KernelMethod);
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
        /// Creates a new instance of a reduction handler using the provided
        /// shuffle-down and reduction logics.
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
            where TReduction : struct, IAtomicReduction<T>
        {
            var reductionDel = accelerator.CreateReduction<T, TShuffleDown, TReduction>();
            return (stream, input, output) =>
            {
                reductionDel(stream, input, output, shuffleDown, reduction);
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
        public static void Reduce<T, TShuffleDown, TReduction>(
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
            accelerator.CreateReduction<T, TShuffleDown, TReduction>()(
                stream,
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
            where TReduction : struct, IAtomicReduction<T>
        {
            var output = accelerator.MemoryCache.Allocate<T>(1);
            accelerator.Reduce(
                stream,
                input,
                output,
                shuffleDown,
                reduction);
            stream.Synchronize();
            accelerator.MemoryCache.CopyTo(stream, out T result, 0);
            return result;
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
            where TReduction : struct, IAtomicReduction<T>
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
    }
}
