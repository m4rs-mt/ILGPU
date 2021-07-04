// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright(c) 2016-2018 ILGPU Lightning Project
//                                    www.ilgpu.net
//
// File: ReductionExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

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
    /// <typeparam name="TStride">The 1D stride of the source view.</typeparam>
    /// <param name="stream">The accelerator stream.</param>
    /// <param name="input">The input elements to reduce.</param>
    /// <param name="output">The output view to store the reduced value.</param>
    public delegate void Reduction<T, TStride>(
        AcceleratorStream stream,
        ArrayView1D<T, TStride> input,
        ArrayView<T> output)
        where T : unmanaged
        where TStride : struct, IStride1D;

    #endregion

    /// <summary>
    /// Reduction functionality for accelerators.
    /// </summary>
    public static class ReductionExtensions
    {
        #region Reduction Implementation

        /// <summary>
        /// A actual raw reduction implementation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the source view.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction to use.</typeparam>
        internal struct ReductionImplementation<
            T,
            TStride,
            TReduction> : IGridStrideKernelBody
            where T : unmanaged
            where TStride : struct, IStride1D
            where TReduction : struct, IScanReduceOperation<T>
        {
            /// <summary>
            /// Creates a new reduction instance.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static TReduction GetReduction()
            {
                TReduction reduction = default;
                return reduction;
            }

            /// <summary>
            /// Creates a new reduction implementation.
            /// </summary>
            /// <param name="input">The input view.</param>
            /// <param name="output">The output view (1 element min).</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReductionImplementation(
                ArrayView1D<T, TStride> input,
                ArrayView<T> output)
            {
                Input = input;
                Output = output;
                ReducedValue = GetReduction().Identity;
            }

            /// <summary>
            /// Returns the source view.
            /// </summary>
            public ArrayView1D<T, TStride> Input { get; }

            /// <summary>
            /// Returns the output view.
            /// </summary>
            public ArrayView<T> Output { get; }

            /// <summary>
            /// Stores the current intermediate result of this thread.
            /// </summary>
            public T ReducedValue { get; private set; }

            /// <summary>
            /// Reduces each element in a grid-stride loop.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(LongIndex1D linearIndex)
            {
                if (linearIndex >= Input.Length)
                    return;

                ReducedValue = GetReduction().Apply(ReducedValue, Input[linearIndex]);
            }

            /// <summary>
            /// Finished a group-wide reduction operation using shuffles, shared memory
            /// and atomic operations.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Finish()
            {
                // Perform group wide reduction
                ReducedValue = GroupExtensions.Reduce<T, TReduction>(ReducedValue);

                if (Group.IsFirstThread)
                    GetReduction().AtomicApply(ref Output[0], ReducedValue);
            }
        }

        /// <summary>
        /// Creates a new instance of a reduction handler.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the source view.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <returns>The created reduction handler.</returns>
        public static Reduction<T, TStride> CreateReduction<T, TStride, TReduction>(
            this Accelerator accelerator)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TReduction : struct, IScanReduceOperation<T>
        {
            var initializer = accelerator.CreateInitializer<T, Stride1D.Dense>();
            var reductionKernel = accelerator.LoadGridStrideKernel<
                ReductionImplementation<T, TStride, TReduction>>();
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

                // Ensure a single element in the ouput view
                output = output.SubView(0, 1);

                TReduction reduction = default;
                initializer(stream, output, reduction.Identity);
                reductionKernel(
                    stream,
                    input.Length,
                    new ReductionImplementation<T, TStride, TReduction>(
                        input,
                        output));
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
            where T : unmanaged
            where TReduction : struct, IScanReduceOperation<T> =>
            accelerator.CreateReduction<T, Stride1D.Dense, TReduction>()(
                stream,
                input,
                output);

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <remarks>
        /// Uses the internal cache to realize a temporary output buffer.
        /// </remarks>
        /// <returns>The reduced value.</returns>
        public static T Reduce<T, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input)
            where T : unmanaged
            where TReduction : struct, IScanReduceOperation<T>
        {
            using var output = accelerator.Allocate1D<T>(1);
            accelerator.Reduce<T, TReduction>(stream, input, output.View);
            T result = default;
            output.View.CopyToCPU(stream, ref result, 1);
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
        /// <remarks>
        /// Uses the internal cache to realize a temporary output buffer.
        /// </remarks>
        /// <returns>The reduced value.</returns>
        public static Task<T> ReduceAsync<T, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView<T> input)
            where T : unmanaged
            where TReduction : struct, IScanReduceOperation<T> =>
            Task.Run(() => accelerator.Reduce<T, TReduction>(stream, input));

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the input view.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <param name="output">The output view to store the reduced value.</param>
        public static void Reduce<T, TStride, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView1D<T, TStride> input,
            ArrayView<T> output)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TReduction : struct, IScanReduceOperation<T> =>
            accelerator.CreateReduction<T, TStride, TReduction>()(
                stream,
                input,
                output);

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the input view.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <remarks>
        /// Uses the internal cache to realize a temporary output buffer.
        /// </remarks>
        /// <returns>The reduced value.</returns>
        public static T Reduce<T, TStride, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView1D<T, TStride> input)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TReduction : struct, IScanReduceOperation<T>
        {
            using var output = accelerator.Allocate1D<T>(1);
            accelerator.Reduce<T, TStride, TReduction>(stream, input, output.View);
            T result = default;
            output.View.CopyToCPU(stream, ref result, 1);
            return result;
        }

        /// <summary>
        /// Performs a reduction using a reduction logic.
        /// </summary>
        /// <typeparam name="T">The underlying type of the reduction.</typeparam>
        /// <typeparam name="TStride">The 1D stride of the input view.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="input">The input elements to reduce.</param>
        /// <remarks>
        /// Uses the internal cache to realize a temporary output buffer.
        /// </remarks>
        /// <returns>The reduced value.</returns>
        public static Task<T> ReduceAsync<T, TStride, TReduction>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            ArrayView1D<T, TStride> input)
            where T : unmanaged
            where TStride : struct, IStride1D
            where TReduction : struct, IScanReduceOperation<T> =>
            Task.Run(() => accelerator.Reduce<T, TStride, TReduction>(stream, input));
    }
}
