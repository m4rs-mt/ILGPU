// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                                www.ilgpu.net
//
// File: TileInfo.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Contains information about a single scan tile.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public readonly struct TileInfo<T>
        where T : struct
    {
        /// <summary>
        /// Constructs a new tile information instance.
        /// </summary>
        /// <param name="index">The current grouped index.</param>
        /// <param name="input">The input view.</param>
        /// <param name="numIterationsPerGroup">The number of iterations per group to compute the tile size.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TileInfo(GroupedIndex index, ArrayView<T> input, Index numIterationsPerGroup)
        {
            GroupIndex = index.GroupIdx;
            GroupDim = Group.DimensionX;
            TileSize = GroupDim * numIterationsPerGroup;
            StartIndex = index.GridIdx * TileSize + index.GroupIdx;
            EndIndex = (index.GridIdx + Index.One) * TileSize;
            MaxLength = XMath.Min(input.Length, EndIndex);
        }

        /// <summary>
        /// Returns the current group index.
        /// </summary>
        public Index GroupIndex { get; }

        /// <summary>
        /// Returns the current group dimension.
        /// </summary>
        public Index GroupDim { get; }

        /// <summary>
        /// Returns the tile size.
        /// </summary>
        public Index TileSize { get; }

        /// <summary>
        /// Returns the start index of the current thread (inclusive).
        /// </summary>
        public Index StartIndex { get; }

        /// <summary>
        /// Returns the end index of all threads in the group (exclusive).
        /// </summary>
        public Index EndIndex { get; }

        /// <summary>
        /// Returns the maximum data length to avoid out-of-bounds accesses.
        /// </summary>
        public Index MaxLength { get; }
    }
}
