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
        /// <param name="input">The input view.</param>
        /// <param name="numIterationsPerGroup">The number of iterations per group to compute the tile size.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TileInfo(ArrayView<T> input, Index1 numIterationsPerGroup)
        {
            TileSize = Group.DimX * numIterationsPerGroup;
            StartIndex = Grid.IdxX * TileSize + Group.IdxX;
            EndIndex = (Grid.IdxX + Index1.One) * TileSize;
            MaxLength = XMath.Min(input.Length, EndIndex);
        }

        /// <summary>
        /// Returns the tile size.
        /// </summary>
        public Index1 TileSize { get; }

        /// <summary>
        /// Returns the start index of the current thread (inclusive).
        /// </summary>
        public Index1 StartIndex { get; }

        /// <summary>
        /// Returns the end index of all threads in the group (exclusive).
        /// </summary>
        public Index1 EndIndex { get; }

        /// <summary>
        /// Returns the maximum data length to avoid out-of-bounds accesses.
        /// </summary>
        public Index1 MaxLength { get; }
    }
}
