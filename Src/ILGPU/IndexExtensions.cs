// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IndexExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

namespace ILGPU
{
    partial struct Index1
    {
        /// <summary>
        /// Converts the given 64-bit index into its 32-bit representation.
        /// </summary>
        /// <param name="index">The long index value.</param>
        /// <remarks>Required due to backwards compatibility.</remarks>
        public static implicit operator Index1(long index) =>
            new LongIndex1(index).ToIntIndex();
    }

    partial struct Index2
    {
        /// <summary>
        /// Converts the given 64-bit index into its 32-bit representation.
        /// </summary>
        /// <param name="index">The long index value.</param>
        /// <remarks>Required due to backwards compatibility.</remarks>
        public static implicit operator Index2(LongIndex2 index) =>
            index.ToIntIndex();
    }

    partial struct Index3
    {
        /// <summary>
        /// Constructs a new index.
        /// </summary>
        /// <param name="xy">The x and y values.</param>
        /// <param name="z">The z value.</param>
        public Index3(Index2 xy, int z)
            : this(xy.X, xy.Y, z)
        { }

        /// <summary>
        /// Constructs a new index.
        /// </summary>
        /// <param name="x">The x value.</param>
        /// <param name="yz">The x and y values.</param>
        public Index3(int x, Index2 yz)
            : this(x, yz.X, yz.Y)
        { }

        /// <summary>
        /// Returns the XY components.
        /// </summary>
        public readonly Index2 XY => new Index2(X, Y);

        /// <summary>
        /// Returns the YZ components.
        /// </summary>
        public readonly Index2 YZ => new Index2(Y, Z);

        /// <summary>
        /// Converts the given 64-bit index into its 32-bit representation.
        /// </summary>
        /// <param name="index">The long index value.</param>
        /// <remarks>Required due to backwards compatibility.</remarks>
        public static implicit operator Index3(LongIndex3 index) =>
            index.ToIntIndex();
    }

    partial struct LongIndex3
    {
        /// <summary>
        /// Constructs a new index.
        /// </summary>
        /// <param name="xy">The x and y values.</param>
        /// <param name="z">The z value.</param>
        public LongIndex3(LongIndex2 xy, long z)
            : this(xy.X, xy.Y, z)
        { }

        /// <summary>
        /// Constructs a new index.
        /// </summary>
        /// <param name="x">The x value.</param>
        /// <param name="yz">The x and y values.</param>
        public LongIndex3(long x, LongIndex2 yz)
            : this(x, yz.X, yz.Y)
        { }

        /// <summary>
        /// Returns the XY components.
        /// </summary>
        public readonly LongIndex2 XY => new LongIndex2(X, Y);

        /// <summary>
        /// Returns the YZ components.
        /// </summary>
        public readonly LongIndex2 YZ => new LongIndex2(Y, Z);
    }
}
