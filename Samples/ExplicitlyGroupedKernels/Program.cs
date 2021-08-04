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

namespace ExplicitlyGroupedKernels
{
    class Program
    {
        /// <summary>
        /// Explicitly-grouped kernels have access to the static classes:
        /// <see cref="Grid"/> and <see cref="Group"/>.
        /// These kernel types expose the underlying blocking/grouping semantics of a GPU
        /// and allow for highly efficient implementation of kernels for different GPUs.
        /// The semantics of theses kernels are equivalent to kernel implementations in CUDA.
        /// An explicitly-grouped kernel can be loaded with:
        /// - LoadImplicitlyGroupedKernel
        /// - LoadAutoGroupedKernel.
        /// 
        /// Note that you must not use warp-shuffle functionality within implicitly grouped
        /// kernels since not all lanes of a warp are guaranteed to participate in the warp shuffle.
        /// </summary>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="constant">A nice uniform constant.</param>
        static void GroupedKernel(
            ArrayView<int> dataView,     // A view to a chunk of memory (1D in this case)
            int constant)                // A sample uniform constant
        {
            // Get the global 1D index for accessing the data view
            var globalIndex = Grid.GlobalIndex.X;

            if (globalIndex < dataView.Length)
                dataView[globalIndex] = globalIndex + constant;

            // Note: this explicitly grouped kernel implements the same functionality
            // as MyKernel in the ImplicitlyGroupedKernels sample.
        }

        /// <summary>
        /// Demonstrates the use of a group-wide barrier.
        /// </summary>
        static void GroupedKernelBarrier(
            ArrayView<int> dataView,     // A view to a chunk of memory (1D in this case)
            ArrayView<int> outputView,   // A view to a chunk of memory (1D in this case)
            int constant)                // A sample uniform constant
        {
            // Get the global 1D index for accessing the data view
            var globalIndex = Grid.GlobalIndex.X;

            // Wait until all threads in the group reach this point
            Group.Barrier();

            if (globalIndex < dataView.Length)
                outputView[globalIndex] = dataView[globalIndex] > constant ? 1 : 0;
        }

        /// <summary>
        /// Demonstrates the use of a group-wide and-barrier.
        /// </summary>
        static void GroupedKernelAndBarrier(
            ArrayView<int> dataView,     // A view to a chunk of memory (1D in this case)
            ArrayView<int> outputView,   // A view to a chunk of memory (1D in this case)
            int constant)              // A sample uniform constant
        {
            // Get the global 1D index for accessing the data view
            var globalIndex = Grid.GlobalIndex.X;

            // Load value if the index is in range
            var value = globalIndex < dataView.Length ?
                dataView[globalIndex] :
                constant + 1;

            // Wait until all threads in the group reach this point. Moreover, BarrierAnd
            // evaluates the given predicate and returns true if the predicate evaluates
            // to true for all threads in the group.
            var found = Group.BarrierAnd(value > constant);

            if (globalIndex < outputView.Length)
                outputView[globalIndex] = found ? 1 : 0;
        }

        /// <summary>
        /// Demonstrates the use of a group-wide or-barrier.
        /// </summary>
        static void GroupedKernelOrBarrier(
            ArrayView<int> dataView,     // A view to a chunk of memory (1D in this case)
            ArrayView<int> outputView,   // A view to a chunk of memory (1D in this case)
            int constant)                // A sample uniform constant
        {
            // Get the global 1D index for accessing the data view
            var globalIndex = Grid.GlobalIndex.X;

            // Load value if the index is in range
            var value = globalIndex < dataView.Length ?
                dataView[globalIndex] :
                constant;

            // Wait until all threads in the group reach this point. Moreover, BarrierOr
            // evaluates the given predicate and returns true if the predicate evaluates
            // to true for any thread in the group.
            var found = Group.BarrierOr(value > constant);

            if (globalIndex < outputView.Length)
                outputView[globalIndex] = found ? 1 : 0;
        }

        /// <summary>
        /// Demonstrates the use of a group-wide popcount-barrier.
        /// </summary>
        static void GroupedKernelPopCountBarrier(
            ArrayView<int> dataView,     // A view to a chunk of memory (1D in this case)
            ArrayView<int> outputView,   // A view to a chunk of memory (1D in this case)
            int constant)                // A sample uniform constant
        {
            // Get the global 1D index for accessing the data view
            var globalIndex = Grid.GlobalIndex.X;

            // Load value if the index is in range
            var value = globalIndex < dataView.Length ?
                dataView[globalIndex] :
                constant;

            // Wait until all threads in the group reach this point. Moreover, BarrierPopCount
            // evaluates the given predicate and returns the number of threads in the group
            // for which the predicate evaluated to true.
            var count = Group.BarrierPopCount(value > constant);

            if (globalIndex < outputView.Length)
                outputView[globalIndex] = count;
        }

        /// <summary>
        /// Launches a simple 1D kernel using the default explicit-grouping functionality.
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

                var data = Enumerable.Range(1, 128).ToArray();

                int groupSize = accelerator.MaxNumThreadsPerGroup;
                KernelConfig launchDimension = (
                    (data.Length + groupSize - 1) / groupSize,  // Compute the number of groups (round up)
                    groupSize);                                 // Use the given group size

                // Initialize data source
                using var dataSource = accelerator.Allocate1D<int>(data.Length);
                dataSource.CopyFromCPU(data);

                using var dataTarget = accelerator.Allocate1D<int>(data.Length);

                // Launch default grouped kernel
                {
                    dataTarget.MemSetToZero();

                    var groupedKernel = accelerator.LoadStreamKernel<ArrayView<int>, int>(GroupedKernel);
                    groupedKernel(launchDimension, dataTarget.View, 64);

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    Console.WriteLine("Default grouped kernel");
                    var target = dataTarget.GetAsArray1D();
                    for (int i = 0, e = target.Length; i < e; ++i)
                        Console.WriteLine($"Data[{i}] = {target[i]}");
                }

                // Launch grouped kernel with barrier
                {
                    dataTarget.MemSetToZero();

                    var groupedKernel = accelerator.LoadStreamKernel<ArrayView<int>, ArrayView<int>, int>(GroupedKernelBarrier);
                    groupedKernel(launchDimension, dataSource.View, dataTarget.View, 64);

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    Console.WriteLine("Grouped-barrier kernel");
                    var target = dataTarget.GetAsArray1D();
                    for (int i = 0, e = target.Length; i < e; ++i)
                        Console.WriteLine($"Data[{i}] = {target[i]}");
                }

                // Launch grouped kernel with and-barrier
                {
                    dataTarget.MemSetToZero();

                    var groupedKernel = accelerator.LoadStreamKernel<ArrayView<int>, ArrayView<int>, int>(GroupedKernelAndBarrier);
                    groupedKernel(launchDimension, dataSource.View, dataTarget.View, 0);

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    Console.WriteLine("Grouped-and-barrier kernel");
                    var target = dataTarget.GetAsArray1D();
                    for (int i = 0, e = target.Length; i < e; ++i)
                        Console.WriteLine($"Data[{i}] = {target[i]}");
                }

                // Launch grouped kernel with or-barrier
                {
                    dataTarget.MemSetToZero();

                    var groupedKernel = accelerator.LoadStreamKernel<ArrayView<int>, ArrayView<int>, int>(GroupedKernelOrBarrier);
                    groupedKernel(launchDimension, dataSource.View, dataTarget.View, 64);

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    Console.WriteLine("Grouped-or-barrier kernel");
                    var target = dataTarget.GetAsArray1D();
                    for (int i = 0, e = target.Length; i < e; ++i)
                        Console.WriteLine($"Data[{i}] = {target[i]}");
                }

                // Launch grouped kernel with popcount-barrier
                {
                    dataTarget.MemSetToZero();

                    var groupedKernel = accelerator.LoadStreamKernel<ArrayView<int>, ArrayView<int>, int>(GroupedKernelPopCountBarrier);
                    groupedKernel(launchDimension, dataSource.View, dataTarget.View, 0);

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    Console.WriteLine("Grouped-popcount-barrier kernel");
                    var target = dataTarget.GetAsArray1D();
                    for (int i = 0, e = target.Length; i < e; ++i)
                        Console.WriteLine($"Data[{i}] = {target[i]}");
                }
            }
        }
    }
}
