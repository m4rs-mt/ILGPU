// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                Copyright(c) 2016-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: ReductionExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Runtime;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ILGPU.Algorithms
{
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

    #endregion

    /// <summary>
    /// Reduction functionality for accelerators.
    /// </summary>
    public static class ReductionExtensions
    {
        #region Reduction Implementation

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
        /// The actual reduction implementation.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="index">The current thread index.</param>
        /// <param name="input">The input view.</param>
        /// <param name="output">The output view.</param>
        internal static void ReductionKernel<T, TReduction>(
            GroupedIndex index,
            ArrayView<T> input,
            ArrayView<T> output)
            where T : struct
            where TReduction : struct, IScanReduceOperation<T>
        {
            var stride = GridExtensions.GridStrideLoopStride;

            TReduction reduction = default;

            var reduced = reduction.Identity;
            for (var idx = index.ComputeGlobalIndex(); idx < input.Length; idx += stride)
                reduced = reduction.Apply(reduced, input[idx]);

            reduced = GroupExtensions.Reduce<T, TReduction>(reduced);

            if (index.GroupIdx.IsFirst)
                reduction.AtomicApply(ref output[0], reduced);
        }

        /// <summary>
        /// Creates a new instance of a reduction handler.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created reduction handler.</returns>
        public static Reduction<T> CreateReduction<T, TReduction>(
            this Accelerator accelerator)
            where T : struct
            where TReduction : struct, IScanReduceOperation<T>
        {
            var initializer = accelerator.CreateInitializer<T>();
            var kernel = accelerator.LoadKernel<GroupedIndex, ArrayView<T>, ArrayView<T>>(ReductionKernel<T, TReduction>);
            return (stream, input, output) =>
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

                TReduction reduction = default;
                initializer(stream, output, reduction.Identity);

                kernel(stream, dimension, input, output);
            };
        }

        #endregion

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        public static void Reduce<T, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input,
            ArrayView<T> output)
            where T : struct
            where TReduction : struct, IScanReduceOperation<T>
        {
            accelerator.CreateReduction<T, TReduction>()(
                stream,
                input,
                output);
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        public static T Reduce<T, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input)
            where T : struct
            where TReduction : struct, IScanReduceOperation<T>
        {
            var output = accelerator.MemoryCache.Allocate<T>(1);
            accelerator.Reduce<T, TReduction>(stream, input, output);
            stream.Synchronize();
            accelerator.MemoryCache.CopyTo(stream, out T result, 0);
            return result;
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <remarks>Uses the internal cache to realize a temporary output buffer.</remarks>
        /// <returns>The reduced value.</returns>
        public static Task<T> ReduceAsync<T, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input)
            where T : struct
            where TReduction : struct, IScanReduceOperation<T>
        {
            return Task.Run(() =>
                accelerator.Reduce<T, TReduction>(stream, input));
        }
    }
}
