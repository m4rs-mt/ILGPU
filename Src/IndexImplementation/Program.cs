// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2019 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IndexImplementation
{
    /// <summary>
    /// Implements a custom 4D indexing scheme
    /// </summary>
    /// <remarks>
    /// Note that an internal type requires all internals to be visible to the ILGPU runtime.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    readonly struct MyIndex4 : IIndex, IGenericIndex<MyIndex4>
    {
        #region Static

        /// <summary>
        /// Reconstructs a 4D index from a linear index.
        /// </summary>
        /// <param name="linearIndex">The lienar index.</param>
        /// <param name="dimension">The 4D dimension for reconstruction.</param>
        /// <returns>The reconstructed 4D index.</returns>
        public static MyIndex4 ReconstructIndex(int linearIndex, MyIndex4 dimension)
        {
            var x = linearIndex % dimension.X;
            var yzw = linearIndex / dimension.X;
            var y = yzw % dimension.Y;
            var zw = yzw / dimension.Y;
            var z = zw % dimension.Z;
            var w = zw / dimension.Z;
            return new MyIndex4(x, y, z, w);
        }

        #endregion

        #region Instance

        private readonly int x;
        private readonly int y;
        private readonly int z;
        private readonly int w;

        /// <summary>
        /// Constructs a new index.
        /// </summary>
        /// <param name="x">The x index.</param>
        /// <param name="y">The y index.</param>
        /// <param name="z">The z index.</param>
        /// <param name="w">The w index.</param>
        public MyIndex4(int x, int y, int z, int w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the x index.
        /// </summary>
        public int X => x;

        /// <summary>
        /// Returns the y index.
        /// </summary>
        public int Y => y;

        /// <summary>
        /// Returns the z index.
        /// </summary>
        public int Z => z;

        /// <summary>
        /// Returns the w index.
        /// </summary>
        public int W => w;

        /// <summary>
        /// Returns the size represented by this index (x * y * z * w).
        /// </summary>
        public int Size => X * Y * Z * w;

        #endregion

        #region IGenericIndex

        /// <summary cref="IGenericIndex{TIndex}.InBounds(TIndex)"/>
        public bool InBounds(MyIndex4 dimension)
        {
            return x >= 0 && x < dimension.x &&
                y >= 0 && y < dimension.y &&
                z >= 0 && z < dimension.z &&
                z >= 0 && w < dimension.w;
        }

        /// <summary cref="IGenericIndex{TIndex}.InBoundsInclusive(TIndex)"/>
        public bool InBoundsInclusive(MyIndex4 dimension)
        {
            return x >= 0 && x <= dimension.x &&
                y >= 0 && y <= dimension.y &&
                z >= 0 && z <= dimension.z &&
                w >= 0 && w <= dimension.w;
        }

        /// <summary>
        /// Computes the linear index of this 4D index by using the provided 4D dimension.
        /// </summary>
        /// <param name="dimension">The dimension for index computation.</param>
        /// <returns>The computed linear index of this 4D index.</returns>
        public int ComputeLinearIndex(MyIndex4 dimension)
        {
            return ((((W * dimension.Z) + Z) * dimension.Y) + Y) * dimension.X + X;
        }

        /// <summary>
        /// Reconstructs a 4D index from a linear index.
        /// </summary>
        /// <param name="linearIndex">The lienar index.</param>
        /// <returns>The reconstructed 4D index.</returns>
        public MyIndex4 ReconstructIndex(int linearIndex)
        {
            return ReconstructIndex(linearIndex, this);
        }

        /// <summary cref="IGenericIndex{TIndex}.Add(TIndex)"/>
        public MyIndex4 Add(MyIndex4 rhs)
        {
            return new MyIndex4(
                x + rhs.x,
                y + rhs.y,
                z + rhs.z,
                w + rhs.w);
        }

        /// <summary cref="IGenericIndex{TIndex}.Subtract(TIndex)"/>
        public MyIndex4 Subtract(MyIndex4 rhs)
        {
            return new MyIndex4(
                x - rhs.x,
                y - rhs.y,
                z - rhs.z,
                w - rhs.w);
        }

        /// <summary cref="IGenericIndex{TIndex}.ComputedCastedExtent(TIndex, int, int)"/>
        public MyIndex4 ComputedCastedExtent(MyIndex4 extent, int elementSize, int newElementSize)
        {
            var wExtent = (extent.W * elementSize) / newElementSize;
            Debug.Assert(wExtent > 0, "OutOfBounds cast");
            return new MyIndex4(extent.X, extent.Y, extent.Z, wExtent);
        }

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true iff the given index is equal to the current index.
        /// </summary>
        /// <param name="other">The other index.</param>
        /// <returns>True, iff the given index is equal to the current index.</returns>
        public bool Equals(MyIndex4 other)
        {
            return this == other;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true iff the given object is equal to the current index.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to the current index.</returns>
        public override bool Equals(object obj)
        {
            if (obj is MyIndex4)
                return Equals((MyIndex4)obj);
            return false;
        }

        /// <summary>
        /// Returns the hash code of this index.
        /// </summary>
        /// <returns>The hash code of this index.</returns>
        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode() ^ W.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of this index.
        /// </summary>
        /// <returns>The string representation of this index.</returns>
        public override string ToString()
        {
            return $"({X}, {Y}, {Z}, {W})";
        }

        #endregion

        #region IComparable

        /// <summary cref="IComparable{T}.CompareTo(T)"/>
        public int CompareTo(MyIndex4 other)
        {
            if (this < other)
                return -1;
            else if (this > other)
                return 1;
            return 0;
        }

        #endregion

        #region Operators

        /// <summary>
        /// Returns true iff the first and second index are the same.
        /// </summary>
        /// <param name="first">The first object.</param>
        /// <param name="second">The second object.</param>
        /// <returns>True, iff the first and second index are the same.</returns>
        public static bool operator ==(MyIndex4 first, MyIndex4 second)
        {
            return first.X == second.X && first.Y == second.Y && first.Z == second.Z && first.W == second.W;
        }

        /// <summary>
        /// Returns true iff the first and second index are not the same.
        /// </summary>
        /// <param name="first">The first object.</param>
        /// <param name="second">The second object.</param>
        /// <returns>True, iff the first and second index are not the same.</returns>
        public static bool operator !=(MyIndex4 first, MyIndex4 second)
        {
            return first.X != second.X || first.Y != second.Y || first.Z != second.Z || first.W != second.W;
        }

        /// <summary>
        /// Returns true iff the first index is smaller than the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first index is smaller than the second index.</returns>
        public static bool operator <(MyIndex4 first, MyIndex4 second)
        {
            return first.X < second.X && first.Y < second.Y && first.Z < second.Z && first.W < second.W;
        }

        /// <summary>
        /// Returns true iff the first index is greater than the second index.
        /// </summary>
        /// <param name="first">The first index.</param>
        /// <param name="second">The second index.</param>
        /// <returns>True, iff the first index is greater than the second index.</returns>
        public static bool operator >(MyIndex4 first, MyIndex4 second)
        {
            return first.X > second.X && first.Y > second.Y && first.Z > second.Z && first.W > second.W;
        }

        #endregion
    }

    class Program
    {
        const int AllocationSize1D = 64;
        const int AllocationSize2D = 32;
        const int AllocationSize3D = 16;
        const int AllocationSize4D = 8;

        private static readonly MyIndex4 Dimension = new MyIndex4(AllocationSize1D, AllocationSize2D, AllocationSize3D, AllocationSize4D);

        /// <summary>
        /// Allocates an nD buffer on the given accelerator and transfers memory
        /// to and from the buffer.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        /// <param name="valueConverter">A value converter to convert values of type MyIndex4 to type T.</param>
        static void AllocND<T>(Accelerator accelerator, Func<MyIndex4, MyIndex4, T> valueConverter)
            where T : struct, IEquatable<T>
        {
            Console.WriteLine($"Performing nD allocation on {accelerator.Name}");
            var data = new T[Dimension.Size];
            for (int i = 0; i < AllocationSize1D; ++i)
            {
                for (int j = 0; j < AllocationSize2D; ++j)
                {
                    for (int k = 0; k < AllocationSize3D; ++k)
                    {
                        for (int l = 0; l < AllocationSize4D; ++l)
                        {
                            var index = new MyIndex4(i, j, k, l);
                            data[index.ComputeLinearIndex(Dimension)] = valueConverter(index, Dimension);
                        }
                    }
                }
            }
            var targetData = new T[Dimension.Size];
            using (var buffer = accelerator.Allocate<T, MyIndex4>(new MyIndex4(AllocationSize1D + 2, Dimension.X, Dimension.Y, Dimension.Z)))
            {
                // Copy to accelerator
                buffer.CopyFrom(
                    data,                      // data source
                    0,                         // source index in the scope of the data source
                    new MyIndex4(2, 0, 0, 0),  // target index in the scope of the buffer
                    data.Length);              // the number of elements to copy

                // Copy from accelerator
                buffer.CopyTo(
                    targetData,                // data target
                    new MyIndex4(2, 0, 0, 0),  // target index in the scope of the buffer
                    0,                         // target index in the scope of the data target
                    Dimension);                // the number of elements to copy
            }

            // Verify data
            for (int i = 0; i < AllocationSize1D; ++i)
            {
                for (int j = 0; j < AllocationSize2D; ++j)
                {
                    for (int k = 0; k < AllocationSize3D; ++k)
                    {
                        for (int l = 0; l < AllocationSize4D; ++l)
                        {
                            var index = new MyIndex4(i, j, k, l).ComputeLinearIndex(Dimension);
                            if (!data[index].Equals(targetData[index]))
                                Console.WriteLine($"Error comparing data and target data at {index}: {targetData[index]} found, but {data[index]} expected");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A 1D kernel that will be mapped to our custom 4D implementation.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void MyKernelND(
            Index index,
            ArrayView<int, MyIndex4> dataView)
        {
            // Reconstruct a valid 4D index
            var myIdx = MyIndex4.ReconstructIndex(index, Dimension);

            // Assign the data at the associated 4D location
            dataView[myIdx] = index;
        }

        /// <summary>
        /// Demonstrates the use of a custom index type to work with indexed memory.
        /// </summary>
        static void Main()
        {
            using (var context = new Context())
            {
                // Perform memory allocations and operations on all available accelerators
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");
                        // Note:
                        // - You can only transfer contiguous chunks of memory to and from memory buffers.
                        //   A transfer of non-contiguous chunks of memory results in undefined buffer contents.
                        // - The memory layout of multi-dimensional arrays is different to the default memory layout of
                        //   a multi-dimensional array in the .Net framework. Addressing a 2D buffer, for example,
                        //   works as follows: y * width + x, where the buffer has dimensions (width, height).
                        // - All allocated buffers have to be disposed before their associated accelerator is disposed.
                        // - You have to keep a reference to the allocated buffer  for as long as you want to access it.
                        //   Otherwise, the GC might dispose it.

                        AllocND(accelerator, (idx, dimension) => idx.ComputeLinearIndex(dimension));
                        AllocND(accelerator, (idx, dimension) => (long)idx.ComputeLinearIndex(dimension));

                        var kernel = accelerator.LoadAutoGroupedStreamKernel<Index, ArrayView<int, MyIndex4>>(MyKernelND);
                        using (var buffer = accelerator.Allocate<int, MyIndex4>(Dimension))
                        {
                            kernel(Dimension.Size, buffer.View);

                            accelerator.Synchronize();
                        }
                    }
                }
            }
        }
    }
}
