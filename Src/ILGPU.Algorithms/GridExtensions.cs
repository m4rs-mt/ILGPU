// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                Copyright(c) 2016-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: GridExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
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
        void Execute(Index linearIndex);
    }

    /// <summary>
    /// Represents a functional grid-stride-loop body.
    /// </summary>
    /// <typeparam name="T">The type of the intermediate values inside the loop.</typeparam>
    public interface IGridStrideLoopBody<T>
        where T : struct
    {
        /// <summary>
        /// Executes this loop body using the given index.
        /// </summary>
        /// <param name="linearIndex">The current global element index.</param>
        /// <param name="input">The intermediate input value.</param>
        /// <returns>The resulting intermediate value for the next iteration.</returns>
        T Execute(Index linearIndex, T input);
    }

    /// <summary>
    /// Contains extensions for thread grids
    /// </summary>
    public static class GridExtensions
    {
        /// <summary>
        /// Returns the loop stride for a grid-stride loop.
        /// </summary>
        public static Index GridStrideLoopStride => Grid.Dimension.X * Group.Dimension.X;

        /// <summary>
        /// Performs a grid-stride loop.
        /// </summary>
        /// <typeparam name="TLoopBody">The type of the loop body.</typeparam>
        /// <param name="index">The global start index.</param>
        /// <param name="length">The global length.</param>
        /// <param name="loopBody">The loop body.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GridStrideLoop<TLoopBody>(
            Index index,
            Index length,
            ref TLoopBody loopBody)
            where TLoopBody : struct, IGridStrideLoopBody
        {
            var stride = Grid.Dimension.X * Group.Dimension.X;
            for (var idx = index; idx < length; idx += stride)
                loopBody.Execute(idx);
        }

        /// <summary>
        /// Performs a grid-stride loop.
        /// </summary>
        /// <typeparam name="TLoopBody">The type of the loop body.</typeparam>
        /// <param name="index">The global start index.</param>
        /// <param name="length">The global length.</param>
        /// <param name="loopBody">The loop body.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GridStrideLoop<TLoopBody>(
            GroupedIndex index,
            Index length,
            ref TLoopBody loopBody)
            where TLoopBody : struct, IGridStrideLoopBody
        {
            GridStrideLoop(
                index.ComputeGlobalIndex(),
                length,
                ref loopBody);
        }

        /// <summary>
        /// Performs a functional grid-stride loop.
        /// </summary>
        /// <typeparam name="T">The element type of the intermediate values.</typeparam>
        /// <typeparam name="TLoopBody">The type of the loop body.</typeparam>
        /// <param name="index">The global start index.</param>
        /// <param name="length">The global length.</param>
        /// <param name="input">The initial input value.</param>
        /// <param name="loopBody">The loop body.</param>
        /// <returns>The last intermediate value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GridStrideLoop<T, TLoopBody>(
            Index index,
            Index length,
            T input,
            TLoopBody loopBody)
            where T : struct
            where TLoopBody : struct, IGridStrideLoopBody<T>
        {
            var stride = Grid.Dimension.X * Group.Dimension.X;
            for (var idx = index; idx < length; idx += stride)
                input = loopBody.Execute(idx, input);
            return input;
        }

        /// <summary>
        /// Performs a functional grid-stride loop.
        /// </summary>
        /// <typeparam name="T">The element type of the intermediate values.</typeparam>
        /// <typeparam name="TLoopBody">The type of the loop body.</typeparam>
        /// <param name="index">The global start index.</param>
        /// <param name="length">The global length.</param>
        /// <param name="input">The initial input value.</param>
        /// <param name="loopBody">The loop body.</param>
        /// <returns>The last intermediate value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GridStrideLoop<T, TLoopBody>(
            GroupedIndex index,
            Index length,
            T input,
            TLoopBody loopBody)
            where T : struct
            where TLoopBody : struct, IGridStrideLoopBody<T>
        {
            return GridStrideLoop(
                index.ComputeGlobalIndex(),
                length,
                input,
                loopBody);
        }

        /// <summary>
        /// Returns a kernel extent (a grouped index) with the maximum number of groups using the
        /// maximum number of threads per group to launch common grid-stride loop kernels.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="numDataElements">The number of parallel data elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GroupedIndex ComputeGridStrideLoopExtent(this Accelerator accelerator, Index numDataElements) =>
            accelerator.ComputeGridStrideLoopExtent(
                numDataElements,
                out var _);

        /// <summary>
        /// Returns a kernel extent (a grouped index) with the maximum number of groups using the
        /// maximum number of threads per group to launch common grid-stride loop kernels.
        /// </summary>
        /// <param name="accelerator">The accelerator.</param>
        /// <param name="numDataElements">The number of parallel data elements to process.</param>
        /// <param name="numIterationsPerGroup">The number of loop iterations per group.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GroupedIndex ComputeGridStrideLoopExtent(
            this Accelerator accelerator,
            Index numDataElements,
            out int numIterationsPerGroup)
        {
            GroupedIndex extent = accelerator.MaxNumGroupsExtent;

            var numParallelGroups = XMath.DivRoundUp(numDataElements, extent.GroupIdx);
            var dimension = XMath.Min(extent.GridIdx, numParallelGroups);

            numIterationsPerGroup = XMath.DivRoundUp(numDataElements, dimension * extent.GroupIdx);

            return new GroupedIndex(dimension, extent.GroupIdx);
        }
    }
}
