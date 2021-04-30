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
    partial struct Index1D
    {
        /// <summary>
        /// Converts the given 64-bit index into its 32-bit representation.
        /// </summary>
        /// <param name="index">The long index value.</param>
        /// <remarks>Required due to backwards compatibility.</remarks>
        public static explicit operator Index1D(long index) =>
            new LongIndex1D(index).ToIntIndex();
    }

    partial struct Index2D
    {
        /// <summary>
        /// Converts the given 64-bit index into its 32-bit representation.
        /// </summary>
        /// <param name="index">The long index value.</param>
        /// <remarks>Required due to backwards compatibility.</remarks>
        public static explicit operator Index2D(LongIndex2D index) =>
            index.ToIntIndex();
    }

    partial struct Index3D
    {
        /// <summary>
        /// Constructs a new index.
        /// </summary>
        /// <param name="xy">The x and y values.</param>
        /// <param name="z">The z value.</param>
        public Index3D(Index2D xy, int z)
            : this(xy.X, xy.Y, z)
        { }

        /// <summary>
        /// Constructs a new index.
        /// </summary>
        /// <param name="x">The x value.</param>
        /// <param name="yz">The x and y values.</param>
        public Index3D(int x, Index2D yz)
            : this(x, yz.X, yz.Y)
        { }

        /// <summary>
        /// Returns the XY components.
        /// </summary>
        public readonly Index2D XY => new Index2D(X, Y);

        /// <summary>
        /// Returns the YZ components.
        /// </summary>
        public readonly Index2D YZ => new Index2D(Y, Z);

        /// <summary>
        /// Converts the given 64-bit index into its 32-bit representation.
        /// </summary>
        /// <param name="index">The long index value.</param>
        /// <remarks>Required due to backwards compatibility.</remarks>
        public static explicit operator Index3D(LongIndex3D index) =>
            index.ToIntIndex();
    }

    partial struct LongIndex3D
    {
        /// <summary>
        /// Constructs a new index.
        /// </summary>
        /// <param name="xy">The x and y values.</param>
        /// <param name="z">The z value.</param>
        public LongIndex3D(LongIndex2D xy, long z)
            : this(xy.X, xy.Y, z)
        { }

        /// <summary>
        /// Constructs a new index.
        /// </summary>
        /// <param name="x">The x value.</param>
        /// <param name="yz">The x and y values.</param>
        public LongIndex3D(long x, LongIndex2D yz)
            : this(x, yz.X, yz.Y)
        { }

        /// <summary>
        /// Returns the XY components.
        /// </summary>
        public readonly LongIndex2D XY => new LongIndex2D(X, Y);

        /// <summary>
        /// Returns the YZ components.
        /// </summary>
        public readonly LongIndex2D YZ => new LongIndex2D(Y, Z);
    }
}
