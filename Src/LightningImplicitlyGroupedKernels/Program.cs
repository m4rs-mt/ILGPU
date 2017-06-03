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

namespace LightningImplicitlyGroupedKernels
{
    class Program
    {
        /// <summary>
        /// Implicitly-grouped kernels receive an index type (first parameter) of type:
        /// <see cref="Index"/>, <see cref="Index2"/> or <see cref="Index3"/>. 
        /// These kernel types hide the underlying blocking/grouping semantics of a GPU 
        /// and allow convenient kernel programming without having take grouping details into account. 
        /// The block or group size can be defined while loading a kernel via:
        /// - LoadImplicitlyGroupedStreamKernel (default accelerator stream)
        /// - LoadImplicitlyGroupedKernel (custom accelerator stream - 1st parameter)
        /// - LoadAutoGroupedStreamKernel (default accelerator stream)
        /// - LoadAutoGroupedKernel (custom accelerator stream - 1st parameter)
        /// 
        /// Note that you must not use warp-shuffle functionality within implicitly grouped
        /// kernels since not all lanes of a warp are guaranteed to participate in the warp shuffle.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="constant">A nice uniform constant.</param>
        static void MyKernel(
            Index index,               // The global thread index (1D in this case)
            ArrayView<int> dataView,   // A view to a chunk of memory (1D in this case)
            int constant)              // A sample uniform constant
        {
            dataView[index] = index + constant;
        }

        static void LaunchKernel(
            LightningContext lc,
            Action<Index, ArrayView<int>, int> launcher)
        {
            using (var buffer = lc.Allocate<int>(1024))
            {
                // Launch buffer.Length many threads and pass a view to buffer
                launcher(buffer.Length, buffer.View, 42);

                // Wait for the kernel to finish...
                lc.Synchronize();

                // Resolve and verify data
                var data = buffer.GetAsArray();
                for (int i = 0, e = data.Length; i < e; ++i)
                {
                    if (data[i] != 42 + i)
                        Console.WriteLine($"Error at element location {i}: {data[i]} found");
                }
            }

        }

        /// <summary>
        /// Launches a simple 1D kernel using implicit and auto-grouping functionality.
        /// </summary>
        static void Main(string[] args)
        {
            // Create main context
            using (var context = new Context())
            {
                // For each available accelerator...
                foreach (var acceleratorId in LightningContext.Accelerators)
                {
                    // Create default accelerator for the given accelerator id
                    using (var lc = LightningContext.CreateContext(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {lc}");

                        // For details about implicitly and auto-grouped kernels refer to the 
                        // ImplicitlyGroupedKernls sample.

                        // LightningContext.LoadImplicitlyGroupedStreamKernel creates a typed launcher
                        // that implicitly uses the default accelerator stream.
                        // In order to create a launcher that receives a custom accelerator stream
                        // use: lc.LoadImplicitlyGroupedKernel<Index, ArrayView<int>, int>(...)
                        var myImplicitlyGroupedKernel = lc.LoadImplicitlyGroupedStreamKernel<
                            Index, ArrayView<int>, int>(MyKernel, lc.WarpSize);

                        LaunchKernel(lc, myImplicitlyGroupedKernel);

                        // LightningContext.LoadAutoGroupedStreamKernel creates a typed launcher
                        // that implicitly uses the default accelerator stream.
                        // In order to create a launcher that receives a custom accelerator stream
                        // use: lc.LoadAutoGroupedKernel<Index, ArrayView<int>, int>(...)
                        var myAutoGroupedKernel = lc.LoadAutoGroupedStreamKernel<
                            Index, ArrayView<int>, int>(MyKernel);

                        LaunchKernel(lc, myAutoGroupedKernel);
                    }
                }
            }
        }
    }
}
