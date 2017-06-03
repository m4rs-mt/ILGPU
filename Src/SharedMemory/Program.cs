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
using ILGPU.Runtime;
using System;
using System.Linq;
using System.Reflection;

namespace SharedMemory
{
    class Program
    {
        /// <summary>
        /// Explicitly grouped kernels receive an index type (first parameter) of type:
        /// <see cref="GroupedIndex"/>, <see cref="GroupedIndex2"/> or <see cref="GroupedIndex3"/>.
        /// Shared memory is only supported in explicitly-grouped kernel contexts.
        /// Shared-memory parameters are automatically handled by the runtime and have to be
        /// annotated with the SharedMemoryAttribute. Note that currently, the only supported
        /// shared-memory parameters are VariableViews and ArrayViews.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="sharedVariable">Implicit shared-memory parameter that is handled by the runtime.</param>
        static void SharedMemoryVariableKernel(
            GroupedIndex index,               // The grouped thread index (1D in this case)
            ArrayView<int> dataView,          // A view to a chunk of memory (1D in this case)
            ArrayView<int> outputView,        // A view to a chunk of memory (1D in this case)

            [SharedMemory]                    // Declares a single variable of type int in 
            VariableView<int> sharedVariable) // shared memory (= 4 bytes)
        {
            // Compute the global 1D index for accessing the data view
            var globalIndex = index.ComputeGlobalIndex();

            // Initialize shared memory
            if (index.GroupIdx.IsFirst)
                sharedVariable.Value = 0;
            // Wait for the initialization to complete
            Group.Barrier();

            if (globalIndex < dataView.Length)
                Atomic.Max(sharedVariable, dataView[globalIndex]);

            // Wait for all threads to complete the maximum computation process
            Group.Barrier();

            // Write the maximum of all values into the data view
            if (globalIndex < outputView.Length)
                outputView[globalIndex] = sharedVariable.Value;
        }

        /// <summary>
        /// Demonstrates the use of shared-memory variable referencing multiple elements.
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
        /// Compiles and launches an explicltly-grouped kernel.
        /// </summary>
        static void CompileAndLaunchKernel(
            Accelerator accelerator,
            MethodInfo method,
            Index numElementsToLaunch,
            Index groupSize,
            Action<Kernel, GroupedIndex> launcher)
        {
            // Create a backend for this device
            using (var backend = accelerator.CreateBackend())
            {
                // Create a new compile unit using the created backend
                using (var compileUnit = accelerator.Context.CreateCompileUnit(backend))
                {
                    // Resolve and compile method into a kernel
                    var compiledKernel = backend.Compile(compileUnit, method);
                    // Info: use compiledKernel.GetBuffer() to retrieve the compiled kernel program data

                    // -------------------------------------------------------------------------------
                    // Load the explicitly grouped kernel
                    var kernel = accelerator.LoadKernel(compiledKernel);
                    // -------------------------------------------------------------------------------

                    launcher(
                        kernel,
                        new GroupedIndex(
                            (numElementsToLaunch + groupSize - 1) / groupSize, // Compute the number of groups (round up)
                            groupSize));                                       // Use the given group size

                    accelerator.Synchronize();
                    kernel.Dispose();
                }
            }
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
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    // Create default accelerator for the given accelerator id
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        // The maximum group size in this example is 128 since the second
                        // kernel has a shared-memory array of 128 elements.
                        var groupSize = Math.Min(accelerator.MaxThreadsPerGroup, 128);

                        var data = Enumerable.Range(1, 128).ToArray();

                        using (var dataSource = accelerator.Allocate<int>(data.Length))
                        {
                            // Initialize data source
                            dataSource.CopyFrom(data, 0, 0, data.Length);

                            using (var dataTarget = accelerator.Allocate<int>(data.Length))
                            {
                                CompileAndLaunchKernel(
                                    accelerator,
                                    typeof(Program).GetMethod(nameof(SharedMemoryVariableKernel), BindingFlags.NonPublic | BindingFlags.Static),
                                    dataSource.Length,                // The minimum number of required threads to process each element
                                    groupSize,
                                    (kernel, dimension) =>
                                    {
                                        dataTarget.MemSetToZero();

                                        // Note that *no* value is passed for the shared-memory variable
                                        // since shared memory is handled automatically inside the runtime
                                        // and shared memory has to be initialized inside a kernel.
                                        // The delegate type for this kernel would be:
                                        // Action<GroupedIndex, ArrayView<int>, ArrayView<int>>.
                                        kernel.Launch(dimension, dataSource.View, dataTarget.View);

                                        accelerator.Synchronize();

                                        Console.WriteLine("Shared-memory kernel");
                                        var target = dataTarget.GetAsArray();
                                        for (int i = 0, e = target.Length; i < e; ++i)
                                            Console.WriteLine($"Data[{i}] = {target[i]}");
                                    });


                                CompileAndLaunchKernel(
                                    accelerator,
                                    typeof(Program).GetMethod(nameof(SharedMemoryArrayKernel), BindingFlags.NonPublic | BindingFlags.Static),
                                    dataSource.Length,                // The minimum number of required threads to process each element
                                    groupSize,
                                    (kernel, dimension) =>
                                    {
                                        dataTarget.MemSetToZero();

                                        // Note that *no* value is passed for the shared-memory variable
                                        // since shared memory is handled automatically inside the runtime
                                        // and shared memory has to be initialized inside a kernel.
                                        // The delegate type for this kernel would be:
                                        // Action<GroupedIndex, ArrayView<int>, ArrayView<int>>.
                                        kernel.Launch(dimension, dataSource.View, dataTarget.View);

                                        accelerator.Synchronize();

                                        Console.WriteLine("Shared-memory-array kernel");
                                        var target = dataTarget.GetAsArray();
                                        for (int i = 0, e = target.Length; i < e; ++i)
                                            Console.WriteLine($"Data[{i}] = {target[i]}");
                                    });
                            }
                        }
                    }
                }
            }
        }
    }
}
