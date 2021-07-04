// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright(c) 2016-2018 ILGPU Lightning Project
//                                    www.ilgpu.net
//
// File: GridExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Represents a grid-stride-loop body.
    /// </summary>
    public interface IGridStrideLoopBody
    {
        /// <summary>
        /// Executes this loop body using the given index.
        /// </summary>
        /// <param name="linearIndex">The current global element index.</param>
        void Execute(Index1D linearIndex);
    }

    /// <summary>
    /// Represents a functional grid-stride-loop body.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the intermediate values inside the loop.
    /// </typeparam>
    [Obsolete("Use IGridStrideKernelBody instead")]
    public interface IGridStrideLoopBody<T>
        where T : struct
    {
        /// <summary>
        /// Executes this loop body using the given index.
        /// </summary>
        /// <param name="linearIndex">The current global element index.</param>
        /// <param name="input">The intermediate input value.</param>
        /// <returns>The resulting intermediate value for the next iteration.</returns>
        T Execute(Index1D linearIndex, T input);
    }

    /// <summary>
    /// Represents a grid-stride-loop kernel body.
    /// </summary>
    public interface IGridStrideKernelBody
    {
        /// <summary>
        /// Executes this loop body using the given index.
        /// </summary>
        /// <param name="linearIndex">The current global element index.</param>
        /// <remarks>
        /// Note that the original number of data elements will be padded to be a
        /// multiple of the warp size. Hence, the linear index can become larger than
        /// the original number of elements. Bounds checks to underlying array views
        /// have to be managed by the user.
        /// </remarks>
        void Execute(LongIndex1D linearIndex);

        /// <summary>
        /// Finishes the processing loop.
        /// </summary>
        /// <remarks>
        /// Note that this method will be invoked for each thread once in each group.
        /// </remarks>
        void Finish();
    }

    /// <summary>
    /// Contains extensions for thread grids
    /// </summary>
    public static class GridExtensions
    {
        /// <summary>
        /// Returns the loop stride for a grid-stride loop.
        /// </summary>
        public static Index1D GridStrideLoopStride => Grid.DimX * Group.DimX;

        /// <summary>
        /// Performs a grid-stride loop.
        /// </summary>
        /// <typeparam name="TLoopBody">The type of the loop body.</typeparam>
        /// <param name="length">The global length.</param>
        /// <param name="loopBody">The loop body.</param>
        [Obsolete("Use GridStrideKernel instead")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GridStrideLoop<TLoopBody>(
            Index1D length,
            ref TLoopBody loopBody)
            where TLoopBody : struct, IGridStrideLoopBody
        {
            for (
                Index1D idx = Grid.GlobalIndex.X, stride = GridStrideLoopStride;
                idx < length;
                idx += stride)
            {
                loopBody.Execute(idx);
            }
        }

        /// <summary>
        /// Performs a functional grid-stride loop.
        /// </summary>
        /// <typeparam name="T">The element type of the intermediate values.</typeparam>
        /// <typeparam name="TLoopBody">The type of the loop body.</typeparam>
        /// <param name="length">The global length.</param>
        /// <param name="input">The initial input value.</param>
        /// <param name="loopBody">The loop body.</param>
        /// <returns>The last intermediate value.</returns>
        [Obsolete("Use GridStrideKernel instead")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GridStrideLoop<T, TLoopBody>(
            Index1D length,
            T input,
            TLoopBody loopBody)
            where T : struct
            where TLoopBody : struct, IGridStrideLoopBody<T>
        {
            for (
                Index1D idx = Grid.GlobalIndex.X, stride = GridStrideLoopStride;
                idx < length;
                idx += stride)
            {
                input = loopBody.Execute(idx, input);
            }
            return input;
        }

        /// <summary>
        /// Returns a kernel extent (a grouped index) with the maximum number of groups
        /// using the maximum number of threads per group to launch common grid-stride
        /// loop kernels.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="numDataElements">
        /// The number of parallel data elements to process.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Index1D, Index1D) ComputeGridStrideLoopExtent(
            this Accelerator accelerator,
            long numDataElements) =>
            accelerator.ComputeGridStrideLoopExtent(
                (int)Math.Min(numDataElements, int.MaxValue));

        /// <summary>
        /// Returns a kernel extent (a grouped index) with the maximum number of groups
        /// using the maximum number of threads per group to launch common grid-stride
        /// loop kernels.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="numDataElements">
        /// The number of parallel data elements to process.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Index1D, Index1D) ComputeGridStrideLoopExtent(
            this Accelerator accelerator,
            int numDataElements) =>
            accelerator.ComputeGridStrideLoopExtent(
                numDataElements,
                out var _);

        /// <summary>
        /// Returns a kernel extent (a grouped index) with the maximum number of groups
        /// using the maximum number of threads per group to launch common grid-stride
        /// loop kernels.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="numDataElements">
        /// The number of parallel data elements to process.
        /// </param>
        /// <param name="numIterationsPerGroup">
        /// The number of loop iterations per group.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Index1D, Index1D) ComputeGridStrideLoopExtent(
            this Accelerator accelerator,
            Index1D numDataElements,
            out int numIterationsPerGroup)
        {
            var (gridDim, groupDim) = accelerator.MaxNumGroupsExtent;

            var numParallelGroups = XMath.DivRoundUp(numDataElements, groupDim);
            var dimension = XMath.Min(gridDim, numParallelGroups);

            numIterationsPerGroup =
                XMath.DivRoundUp(numDataElements, dimension * groupDim);

            return (dimension, groupDim);
        }

        /// <summary>
        /// Represents a full grid-stride loop kernel implementation.
        /// </summary>
        /// <typeparam name="TBody">The body type.</typeparam>
        /// <param name="paddedNumDataElements">The padded number of elements.</param>
        /// <param name="body">The body instance.</param>
        public static void GridStrideLoopKernel<TBody>(
            LongIndex1D paddedNumDataElements,
            TBody body)
            where TBody : struct, IGridStrideKernelBody
        {
            Trace.Assert(
                paddedNumDataElements % Warp.WarpSize == 0L,
                "Invalidly padded number of elements");

            // Note that this kernel does not use GridStrideLoop<TLoopBody> because
            // the reference capture can cause the compiler to skip several
            // optimizations. In addition, this implementation ensures that it is
            // padded by the warp size and uses longs to perform the grid stride loop.

            for (
                LongIndex1D idx = Grid.GlobalIndex.X;
                idx < paddedNumDataElements;
                idx += GridStrideLoopStride)
            {
                body.Execute(idx);
            }

            body.Finish();
        }

        /// <summary>
        /// Determines a grid-stride kernel configuration.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <param name="numDataElements">The number of data elements.</param>
        /// <param name="paddedNumElements">The padded number of elements.</param>
        /// <returns>The kernel configuration.</returns>
        private static KernelConfig GetGridStrideKernelConfig(
            Accelerator accelerator,
            LongIndex1D numDataElements,
            out LongIndex1D paddedNumElements)
        {
            paddedNumElements =
                XMath.DivRoundUp(numDataElements, accelerator.WarpSize) *
                accelerator.WarpSize;
            return ComputeGridStrideLoopExtent(
                accelerator,
                (int)Math.Min(numDataElements, int.MaxValue));
        }

        /// <summary>
        /// Loads a grid-stride kernel.
        /// </summary>
        /// <typeparam name="TBody">The body type.</typeparam>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <returns>The loaded grid-stride kernel.</returns>
        public static Action<AcceleratorStream, LongIndex1D, TBody>
            LoadGridStrideKernel<TBody>(
            this Accelerator accelerator)
            where TBody : struct, IGridStrideKernelBody
        {
            var kernel = accelerator.LoadKernel<LongIndex1D, TBody>(GridStrideLoopKernel);
            return (stream, numDataElements, body) =>
            {
                if (stream is null)
                    throw new ArgumentNullException(nameof(stream));
                if (numDataElements < 0)
                    throw new ArgumentOutOfRangeException(nameof(numDataElements));
                if (numDataElements < 1)
                    return;

                var config = GetGridStrideKernelConfig(
                    stream.Accelerator,
                    numDataElements,
                    out LongIndex1D paddedNumElements);
                kernel(stream, config, paddedNumElements, body);
            };
        }

        /// <summary>
        /// Launches a grid-stride kernel.
        /// </summary>
        /// <typeparam name="TBody">The body type.</typeparam>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <param name="numDataElements">The number of data elements.</param>
        /// <param name="body">The body instance.</param>
        public static void LaunchGridStride<TBody>(
            this Accelerator accelerator,
            LongIndex1D numDataElements,
            in TBody body)
            where TBody : struct, IGridStrideKernelBody =>
            accelerator.LaunchGridStride(
                accelerator.DefaultStream,
                numDataElements,
                body);

        /// <summary>
        /// Launches a grid-stride kernel.
        /// </summary>
        /// <typeparam name="TBody">The body type.</typeparam>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <param name="stream">The current stream.</param>
        /// <param name="numDataElements">The number of data elements.</param>
        /// <param name="body">The body instance.</param>
        public static void LaunchGridStride<TBody>(
            this Accelerator accelerator,
            AcceleratorStream stream,
            LongIndex1D numDataElements,
            in TBody body)
            where TBody : struct, IGridStrideKernelBody
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));
            if (numDataElements < 0)
                throw new ArgumentOutOfRangeException(nameof(numDataElements));
            if (numDataElements < 1)
                return;

            var config = GetGridStrideKernelConfig(
                accelerator,
                numDataElements,
                out LongIndex1D paddedNumElements);
            accelerator.Launch(
                GridStrideLoopKernel,
                stream,
                config,
                paddedNumElements,
                body);
        }
    }
}
