// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: TileInfo.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms
{
    /// <summary>
    /// Contains information about a single scan tile.
    /// </summary>
    public readonly struct TileInfo
    {
        /// <summary>
        /// Constructs a new tile information instance.
        /// </summary>
        /// <param name="inputLength">The input length.</param>
        /// <param name="numIterationsPerGroup">
        /// The number of iterations per group to compute the tile size.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TileInfo(int inputLength, Index1D numIterationsPerGroup)
        {
            TileSize = Group.DimX * numIterationsPerGroup;
            StartIndex = Grid.IdxX * TileSize + Group.IdxX;
            EndIndex = (Grid.IdxX + Index1D.One) * TileSize;
            MaxLength = XMath.Min(inputLength, EndIndex);
        }

        /// <summary>
        /// Returns the tile size.
        /// </summary>
        public Index1D TileSize { get; }

        /// <summary>
        /// Returns the start index of the current thread (inclusive).
        /// </summary>
        public Index1D StartIndex { get; }

        /// <summary>
        /// Returns the end index of all threads in the group (exclusive).
        /// </summary>
        public Index1D EndIndex { get; }

        /// <summary>
        /// Returns the maximum data length to avoid out-of-bounds accesses.
        /// </summary>
        public Index1D MaxLength { get; }
    }
}
