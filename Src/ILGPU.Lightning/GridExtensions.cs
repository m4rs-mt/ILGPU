// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: GridExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

namespace ILGPU.Lightning
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
    }
}
