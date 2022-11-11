// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using System;

namespace GroupGridIndices
{
    class Program
    {
        /// <summary>
        /// Resolves the current thread index within a group.
        /// </summary>
        /// <returns>The current thread index within a group.</returns>
        static int GetGroupIndex()
        {
            // Gets the current thread index within a group.
            // Alternative: Group.Index.X
            return Group.IdxX;
        }

        /// <summary>
        /// Resolves the current thread-group index within a grid.
        /// </summary>
        /// <returns>The current thread-group index within a grid.</returns>
        static int GetGridIndex()
        {
            // Gets the current group index within the grid.
            // Alternative: Grid.Index.X
            return Grid.IdxX;
        }

        /// <summary>
        /// Writes data to the globally unique thread index using static properties.
        /// </summary>
        /// <param name="dataView">The target view.</param>
        /// <param name="constant">The constant to write into the data view.</param>
        static void WriteToGlobalIndex(ArrayView<int> dataView, int constant)
        {
            var globalIndex = Group.DimX * GetGridIndex() + GetGroupIndex();

            if (globalIndex < dataView.Length)
                dataView[globalIndex] = constant;
        }

        /// <summary>
        /// A grouped kernel that uses a helper function
        /// </summary>
        /// <param name="dataView">The target view.</param>
        /// <param name="constant">A uniform constant.</param>
        static void GroupedKernel(
            ArrayView<int> dataView,     // A view to a chunk of memory (1D in this case)
            int constant)                // A sample uniform constant
        {
            WriteToGlobalIndex(dataView, constant);
        }

        /// <summary>
        /// Demonstrates kernels using static properties to access grid and group indices.
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

                var groupSize = accelerator.MaxNumThreadsPerGroup;
                KernelConfig kernelConfig = (2, groupSize);

                using var buffer = accelerator.Allocate1D<int>(kernelConfig.Size);
                var groupedKernel = accelerator.LoadStreamKernel<ArrayView<int>, int>(GroupedKernel);
                groupedKernel(kernelConfig, buffer.View, 64);

                // Reads data from the GPU buffer into a new CPU array.
                // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                // that the kernel and memory copy are completed first.
                Console.WriteLine("Default grouped kernel");
                var data = buffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"Data[{i}] = {data[i]}");
            }
        }
    }
}
