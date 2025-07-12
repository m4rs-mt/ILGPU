// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: TileInfo.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace ILGPU;

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
    public TileInfo(long inputLength, int numIterationsPerGroup)
    {
        TileSize = Group.Dimension * numIterationsPerGroup;
        StartIndex = Grid.Index * TileSize + Group.Index;
        EndIndex = (Grid.Index + 1) * TileSize;
        MaxLength = XMath.Min(inputLength, EndIndex);
    }

    /// <summary>
    /// Returns the tile size.
    /// </summary>
    public int TileSize { get; }

    /// <summary>
    /// Returns the start index of the current thread (inclusive).
    /// </summary>
    public long StartIndex { get; }

    /// <summary>
    /// Returns the end index of all threads in the group (exclusive).
    /// </summary>
    public long EndIndex { get; }

    /// <summary>
    /// Returns the maximum data length to avoid out-of-bounds accesses.
    /// </summary>
    public long MaxLength { get; }
}
