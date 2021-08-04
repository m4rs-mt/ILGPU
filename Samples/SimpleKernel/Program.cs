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

namespace SimpleKernel
{
    class Program
    {
        /// <summary>
        /// A simple 1D kernel. Simple kernels also support other dimensions via Index2 and Index3.
        /// Note that the first argument of a kernel method is always the current index. All other parameters
        /// are optional. Furthermore, kernels can only receive structures as arguments; reference types are
        /// not supported.
        /// 
        /// Memory buffers are accessed via ArrayViews (<see cref="ArrayView{T}"/>, <see cref="ArrayView{T, TIndex}"/>).
        /// These views encapsulate all memory accesses and hide the underlying native pointer operations.
        /// Similar to ArrayViews, a VariableView (<see cref="VariableView{T}"/>) points to a single variable in memory.
        /// In other words, a VariableView is a special ArrayView with a length of 1.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="constant">A uniform constant.</param>
        static void MyKernel(
            Index1D index,             // The global thread index (1D in this case)
            ArrayView<int> dataView,   // A view to a chunk of memory (1D in this case)
            int constant)              // A sample uniform constant
        {
            dataView[index] = index + constant;
        }

        /// <summary>
        /// Launches a simple 1D kernel.
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

                // Compiles and loads the implicitly grouped kernel with an automatically determined
                // group size and an associated default stream.
                // This function automatically compiles the kernel (or loads the kernel from cache)
                // and returns a specialized high-performance kernel launcher.
                // Use LoadAutoGroupedKernel to create a launcher that requires an additional accelerator-stream
                // parameter. In this case the corresponding call will look like this:
                // var kernel = accelerator.LoadautoGroupedKernel<Index, ArrayView<int>, int>(MyKernel);
                // For more detail refer to the ImplicitlyGroupedKernels or ExplicitlyGroupedKernels sample.
                var kernel = accelerator.LoadAutoGroupedStreamKernel<
                    Index1D, ArrayView<int>, int>(MyKernel);

                using var buffer = accelerator.Allocate1D<int>(1024);

                // Launch buffer.Length many threads and pass a view to buffer
                // Note that the kernel launch does not involve any boxing
                kernel((int)buffer.Length, buffer.View, 42);

                // Reads data from the GPU buffer into a new CPU array.
                // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                // that the kernel and memory copy are completed first.
                var data = buffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                {
                    if (data[i] != 42 + i)
                        Console.WriteLine($"Error at element location {i}: {data[i]} found");
                }
            }
        }
    }
}
