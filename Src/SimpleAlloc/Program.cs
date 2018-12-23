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
using System.Linq;

namespace SimpleAlloc
{
    class Program
    {
        const int AllocationSize1D = 512;
        const int AllocationSize2D = 256;
        const int AllocationSize3D = 128;

        /// <summary>
        /// Allocates a simple 1D buffer on the given accelerator and initializes
        /// its contents to zero.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        static void SampleInitialization(Accelerator accelerator)
        {
            using (var data = accelerator.Allocate<int>(1024))
            {
                // Note that allocated memory is not initialized in general and
                // may contain random information.

                // Initialize the whole memory buffer to 0.
                data.MemSetToZero();
            }
        }

        /// <summary>
        /// Allocates a 1D buffer on the given accelerator and transfers memory
        /// to and from the buffer.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        static void Alloc1D(Accelerator accelerator)
        {
            Console.WriteLine($"Performing 1D allocation on {accelerator.Name}");
            var data = Enumerable.Range(0, AllocationSize1D).ToArray();
            var targetData = new int[AllocationSize1D];
            using (var buffer = accelerator.Allocate<int>(data.Length + 32))
            {
                // Copy to accelerator
                buffer.CopyFrom(
                    data,         // data source
                    0,            // source index in the scope of the data source
                    32,           // target index in the scope of the buffer
                    data.Length); // the number of elements to copy

                // Copy from accelerator
                buffer.CopyTo(
                    targetData,   // data target
                    32,           // source index in the scope of the buffer
                    0,            // target index in the scope of the data target
                    data.Length); // the number of elements to copy
            }

            // Verify data
            for (int i = 0; i < AllocationSize1D; ++i)
            {
                if (data[i] != targetData[i])
                    Console.WriteLine($"Error comparing data and target data at {i}: {targetData[i]} found, but {data[i]} expected");
            }
        }

        /// <summary>
        /// Allocates a 2D buffer on the given accelerator and transfers memory
        /// to and from the buffer.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        static void Alloc2D(Accelerator accelerator)
        {
            Console.WriteLine($"Performing 2D allocation on {accelerator.Name}");
            var data = new int[AllocationSize1D, AllocationSize2D];
            for (int i = 0; i < AllocationSize1D; ++i)
            {
                for (int j = 0; j < AllocationSize2D; ++j)
                    data[i, j] = j * AllocationSize1D + i;
            }
            var targetData = new int[AllocationSize1D, AllocationSize2D];
            using (var buffer = accelerator.Allocate<int>(AllocationSize1D + 32, AllocationSize2D))
            // You can also use:
            // using (var buffer = accl.Allocate<int>(new Index2(AllocationSize1D + 32, AllocationSize2D)))
            {
                // Copy to accelerator
                buffer.CopyFrom(
                    data,                                             // data source
                    new Index2(),                                     // source index in the scope of the data source
                    new Index2(32, 0),                                // target index in the scope of the buffer
                    new Index2(AllocationSize1D, AllocationSize2D));  // the number of elements to copy

                // Copy from accelerator
                buffer.CopyTo(
                    targetData,                                       // data target
                    new Index2(32, 0),                                // source index in the scope of the data source
                    new Index2(),                                     // target index in the scope of the buffer
                    new Index2(AllocationSize1D, AllocationSize2D));  // the number of elements to copy
            }

            // Verify data
            for (int i = 0; i < AllocationSize1D; ++i)
            {
                for (int j = 0; j < AllocationSize2D; ++j)
                {
                    if (data[i, j] != targetData[i, j])
                        Console.WriteLine($"Error comparing data and target data at {i}, {j}: {targetData[i, j]} found, but {data[i, j]} expected");
                }
            }
        }

        /// <summary>
        /// Allocates a 3D buffer on the given accelerator and transfers memory
        /// to and from the buffer.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        static void Alloc3D(Accelerator accelerator)
        {
            Console.WriteLine($"Performing 3D allocation on {accelerator.Name}");
            var data = new int[AllocationSize1D, AllocationSize2D, AllocationSize3D];
            for (int i = 0; i < AllocationSize1D; ++i)
            {
                for (int j = 0; j < AllocationSize2D; ++j)
                    for (int k = 0; k < AllocationSize3D; ++k)
                        data[i, j, k] = ((k * AllocationSize2D) + j) * AllocationSize1D + i;
            }
            var targetData = new int[AllocationSize1D, AllocationSize2D, AllocationSize3D];
            using (var buffer = accelerator.Allocate<int>(AllocationSize1D + 32, AllocationSize2D, AllocationSize3D))
            // You can also use:
            // using (var buffer = accl.Allocate<int>(new Index3(AllocationSize1D + 32, AllocationSize2D, AllocationSize3D)))
            {
                // Copy to accelerator
                buffer.CopyFrom(
                    data,                                                               // data source
                    new Index3(),                                                       // source index in the scope of the data source
                    new Index3(32, 0, 0),                                               // target index in the scope of the buffer
                    new Index3(AllocationSize1D, AllocationSize2D, AllocationSize3D));  // the number of elements to copy

                // Copy from accelerator
                buffer.CopyTo(
                    targetData,                                                         // data target
                    new Index3(32, 0, 0),                                               // target index in the scope of the buffer
                    new Index3(),                                                       // source index in the scope of the data source
                    new Index3(AllocationSize1D, AllocationSize2D, AllocationSize3D));  // the number of elements to copy
            }

            // Verify data
            for (int i = 0; i < AllocationSize1D; ++i)
            {
                for (int j = 0; j < AllocationSize2D; ++j)
                {
                    for (int k = 0; k < AllocationSize3D; ++k)
                    {
                        if (data[i, j, k] != targetData[i, j, k])
                            Console.WriteLine($"Error comparing data and target data at {i}, {j}, {k}: {targetData[i, j, k]} found, but {data[i, j, k]} expected");
                    }
                }
            }
        }

        /// <summary>
        /// Performs different memory allocations and operations on all available accelerators.
        /// Note that a MemoryBuffer<T> can only be constructed for blittable T (see
        /// "https://msdn.microsoft.com/de-de/library/75dwhxf7(v=vs.110).aspx", the gist of
        /// which is that bool, char, and class types are not allowed).
        /// Furthermore, all buffers have to be disposed before their associated accelerator is disposed!
        /// </summary>
        static void Main(string[] args)
        {
            // Create main context
            using (var context = new Context())
            {
                // Perform memory allocations and operations on all available accelerators
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        // Note:
                        // - You can only transfer contiguous chunks of memory to and from memory buffers.
                        //   A transfer of non-contiguous chunks of memory results in undefined buffer contents.
                        // - The memory layout of multi-dimensional arrays is different to the default memory layout of
                        //   a multi-dimensional array in the .Net framework. Addressing a 2D buffer, for example,
                        //   works as follows: y * width + x, where the buffer has dimensions (width, height).
                        // - All allocated buffers have to be disposed before their associated accelerator is disposed.
                        // - You have to keep a reference to the allocated buffer for as long as you want to access it.
                        //   Otherwise, the GC might dispose it.

                        SampleInitialization(accelerator);
                        Alloc1D(accelerator);
                        Alloc2D(accelerator);
                        Alloc3D(accelerator);
                    }
                }
            }

        }
    }
}
