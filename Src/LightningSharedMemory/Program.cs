// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                   Copyright (c) 2017 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU;
using ILGPU.Lightning;
using System;
using System.Linq;

namespace LightningSharedMemory
{
    class Program
    {
        /// <summary>
        /// Demonstrates a shared-memory variable referencing multiple elements.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="outputView">The view pointing to our memory buffer.</param>
        /// <param name="sharedArray">Implicit shared-memory parameter that is handled by the runtime.</param>
        static void SharedMemoryArrayKernel(
            GroupedIndex index,          // The grouped thread index (1D in this case)
            ArrayView<int> dataView,     // A view to a chunk of memory (1D in this case)
            ArrayView<int> outputView,   // A view to a chunk of memory (1D in this case)

            [SharedMemory(128)]          // Declares a shared-memory array with 128 elements of
            ArrayView<int> sharedArray)  // type int = 4 * 128 = 512 bytes shared memory per group
        {
            // Compute the global 1D index for accessing the data view
            var globalIndex = index.ComputeGlobalIndex();

            // Load the element into shared memory
            var value = globalIndex < dataView.Length ?
                dataView[globalIndex] :
                0;
            sharedArray[index.GroupIdx] = value;

            // Wait for all threads to complete the loading process
            Group.Barrier();

            // Compute the sum over all elements in the group
            int sum = 0;
            for (int i = 0, e = Group.Dimension.X; i < e; ++i)
                sum += sharedArray[i];

            // Store the sum
            if (globalIndex < outputView.Length)
                outputView[globalIndex] = sum;
        }

        /// <summary>
        /// Launches a simple 1D kernel using shared memory.
        /// </summary>
        static void Main(string[] args)
        {
            // Create main context
            using (var context = new Context())
            {
                // For each available accelerator...
                foreach (var acceleratorId in LightningContext.Accelerators)
                {
                    // A lightning context encapsulates an ILGPU accelerator
                    using (var lc = LightningContext.CreateContext(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {lc}");

                        // The maximum group size in this example is 128 since the second
                        // kernel has a shared-memory array of 128 elements.
                        var groupSize = Math.Min(lc.MaxThreadsPerGroup, 128);

                        var data = Enumerable.Range(1, 128).ToArray();

                        using (var dataSource = lc.Allocate<int>(data.Length))
                        {
                            // Initialize data source
                            dataSource.CopyFrom(data, 0, 0, data.Length);

                            var dimension = new GroupedIndex(
                                (dataSource.Length + groupSize - 1) / groupSize, // Compute the number of groups (round up)
                                groupSize);                                      // Use the given group size

                            using (var dataTarget = lc.Allocate<int>(data.Length))
                            {
                                var sharedMemArrKernel = lc.LoadSharedMemoryKernel1<
                                    GroupedIndex, ArrayView<int>, ArrayView<int>, ArrayView<int>>(SharedMemoryArrayKernel);

                                dataTarget.MemSetToZero();

                                // Note that *no* value is passed for the shared-memory variable
                                // since shared memory is handled automatically inside the runtime
                                // and shared memory has to be initialized inside a kernel.
                                sharedMemArrKernel(
                                    lc.DefaultStream,
                                    dimension,
                                    dataSource.View,
                                    dataTarget.View);

                                lc.Synchronize();

                                Console.WriteLine("Shared-memory-array kernel");
                                var target = dataTarget.GetAsArray();
                                for (int i = 0, e = target.Length; i < e; ++i)
                                    Console.WriteLine($"Data[{i}] = {target[i]}");
                            }
                        }
                    }
                }
            }
        }
    }
}
