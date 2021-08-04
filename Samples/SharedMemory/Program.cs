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

namespace SharedMemory
{
    class Program
    {
        /// <summary>
        /// Explicitly grouped kernels receive an index type (first parameter) of type:
        /// <see cref="GroupedIndex"/>, <see cref="GroupedIndex2"/> or <see cref="GroupedIndex3"/>.
        /// Shared memory is only supported in explicitly-grouped kernel contexts and can be accesses
        /// via the static <see cref="ILGPU.SharedMemory"/> class.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void SharedMemoryVariableKernel(
            ArrayView<int> dataView,          // A view to a chunk of memory (1D in this case)
            ArrayView<int> outputView)        // A view to a chunk of memory (1D in this case)
        {
            // Compute the global 1D index for accessing the data view
            int globalIndex = Grid.GlobalIndex.X;

            // 'Allocate' a single shared memory variable of type int (= 4 bytes)
            ref int sharedVariable = ref ILGPU.SharedMemory.Allocate<int>();

            // Initialize shared memory
            if (Group.IsFirstThread)
                sharedVariable = 0;
            // Wait for the initialization to complete
            Group.Barrier();

            if (globalIndex < dataView.Length)
                Atomic.Max(ref sharedVariable, dataView[globalIndex]);

            // Wait for all threads to complete the maximum computation process
            Group.Barrier();

            // Write the maximum of all values into the data view
            if (globalIndex < outputView.Length)
                outputView[globalIndex] = sharedVariable;
        }

        /// <summary>
        /// Demonstrates the use of shared-memory variable referencing multiple elements.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="outputView">The view pointing to our memory buffer.</param>
        /// <param name="sharedArray">Implicit shared-memory parameter that is handled by the runtime.</param>
        static void SharedMemoryArrayKernel(
            ArrayView<int> dataView,     // A view to a chunk of memory (1D in this case)
            ArrayView<int> outputView)   // A view to a chunk of memory (1D in this case)
        {
            // Compute the global 1D index for accessing the data view
            int globalIndex = Grid.GlobalIndex.X;

            // Declares a shared-memory array with 128 elements of type int = 4 * 128 = 512 bytes
            // of shared memory per group
            // Note that 'Allocate' requires a compile-time known constant array size.
            // If the size is unknown at compile-time, consider using `GetDynamic`.
            ArrayView<int> sharedArray = ILGPU.SharedMemory.Allocate<int>(128);

            // Load the element into shared memory
            var value = globalIndex < dataView.Length ?
                dataView[globalIndex] :
                0;
            sharedArray[Group.IdxX] = value;

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
        static void Main()
        {
            // Create main context
            using var context = Context.CreateDefault();

            // For each available device...
            foreach (var device in context)
            {
                // Create accelerator for the given device
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                // The maximum group size in this example is 128 since the second
                // kernel has a shared-memory array of 128 elements.
                var groupSize = Math.Min(accelerator.MaxNumThreadsPerGroup, 128);

                var data = Enumerable.Range(1, 128).ToArray();

                // Initialize data source
                using var dataSource = accelerator.Allocate1D<int>(data.Length);
                dataSource.CopyFromCPU(data);

                KernelConfig dimension = (
                    ((int)dataSource.Length + groupSize - 1) / groupSize, // Compute the number of groups (round up)
                    groupSize);                                           // Use the given group size

                using var dataTarget = accelerator.Allocate1D<int>(data.Length);
                var sharedMemVarKernel = accelerator.LoadStreamKernel<
                    ArrayView<int>, ArrayView<int>>(SharedMemoryVariableKernel);
                dataTarget.MemSetToZero();

                // Note that shared memory cannot be accessed from the outside
                // and must be initialized by the kernel
                sharedMemVarKernel(dimension, dataSource.View, dataTarget.View);

                // Reads data from the GPU buffer into a new CPU array.
                // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                // that the kernel and memory copy are completed first.
                Console.WriteLine("Shared-memory kernel");
                var target = dataTarget.GetAsArray1D();
                for (int i = 0, e = target.Length; i < e; ++i)
                    Console.WriteLine($"Data[{i}] = {target[i]}");

                var sharedMemArrKernel = accelerator.LoadStreamKernel<
                    ArrayView<int>, ArrayView<int>>(SharedMemoryArrayKernel);
                dataTarget.MemSetToZero();

                // Note that shared memory cannot be accessed from the outside
                // and must be initialized by the kernel
                sharedMemArrKernel(dimension, dataSource.View, dataTarget.View);

                // Reads data from the GPU buffer into a new CPU array.
                // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                // that the kernel and memory copy are completed first.
                Console.WriteLine("Shared-memory-array kernel");
                target = dataTarget.GetAsArray1D();
                for (int i = 0, e = target.Length; i < e; ++i)
                    Console.WriteLine($"Data[{i}] = {target[i]}");
            }
        }
    }
}
