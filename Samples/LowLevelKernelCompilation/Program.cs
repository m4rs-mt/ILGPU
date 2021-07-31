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
using ILGPU.Backends;
using ILGPU.Backends.EntryPoints;
using ILGPU.Runtime;
using System;
using System.Reflection;

namespace LowLevelKernelCompilation
{
    class Program
    {
        /// <summary>
        /// Implicitly-grouped kernels receive an index type (first parameter) of type:
        /// <see cref="Index"/>, <see cref="Index2"/> or <see cref="Index3"/>. 
        /// These kernel types hide the underlying blocking/grouping semantics of a GPU 
        /// and allow convenient kernel programming without having take grouping details into account. 
        /// The block or group size can be defined while loading a kernel via:
        /// - LoadImplicitlyGroupedKernel
        /// - LoadAutoGroupedKernel.
        /// 
        /// Note that you must not use warp-shuffle functionality within implicitly-grouped
        /// kernels since not all lanes of a warp are guaranteed to participate in the warp shuffle.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="constant">A nice uniform constant.</param>
        static void MyKernel(
            Index1D index,             // The global thread index (1D in this case)
            ArrayView<int> dataView,   // A view to a chunk of memory (1D in this case)
            int constant)              // A uniform constant
        {
            dataView[index] = index + constant;
        }

        /// <summary>
        /// Explicitly-grouped kernels receive an index type (first parameter) of type:
        /// <see cref="GroupedIndex"/>, <see cref="GroupedIndex2"/> or <see cref="GroupedIndex3"/>.
        /// These kernel types expose the underlying blocking/grouping semantics of a GPU
        /// and allow for highly efficient implementation of kernels for different GPUs.
        /// The semantics of theses kernels are equivalent to kernel implementations in CUDA.
        /// Explicitly-grouped kernels can use warp and group-based intrinsics without
        /// restrictions (in contrast to implicitly-grouped kernels).
        /// An explicitly-grouped kernel can be loaded with:
        /// - LoadKernel
        /// </summary>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="constant">A uniform constant.</param>
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
        /// Compiles and launches an explicitly grouped kernel.
        /// </summary>
        static void CompileAndLaunchKernel(Accelerator accelerator, int groupSize)
        {
            // Access the current backend for this device
            var backend = accelerator.GetBackend();

            // Resolve and compile method into a kernel
            var method = typeof(Program).GetMethod(nameof(GroupedKernel), BindingFlags.NonPublic | BindingFlags.Static);
            var entryPointDesc = EntryPointDescription.FromExplicitlyGroupedKernel(method);
            var compiledKernel = backend.Compile(entryPointDesc, default);
            // Info: If the current accelerator is a CudaAccelerator, we can cast the compiled kernel to a
            // PTXCompiledKernel in order to extract the PTX assembly code.

            // -------------------------------------------------------------------------------
            // Load the explicitly grouped kernel
            // Note that the kernel has to be disposed manually.
            using (var kernel = accelerator.LoadKernel(compiledKernel))
            {
                var launcher = kernel.CreateLauncherDelegate<Action<AcceleratorStream, KernelConfig, ArrayView<int>, int>>();
                // -------------------------------------------------------------------------------

                using (var buffer = accelerator.Allocate1D<int>(1024))
                {
                    // You can also use kernel.Launch; however, the generic launch method involves boxing.
                    launcher(
                        accelerator.DefaultStream,
                        (((int)buffer.Length + groupSize - 1) / groupSize, // Compute the number of groups (round up)
                         groupSize),                                       // Use the given group size
                        buffer.View,
                        42);

                    accelerator.Synchronize();

                    // Resolve and verify data
                    var data = buffer.GetAsArray1D();
                    for (int i = 0, e = data.Length; i < e; ++i)
                    {
                        if (data[i] != 42 + i)
                            Console.WriteLine($"Error at element location {i}: {data[i]} found");
                    }
                }
            }
        }

        /// <summary>
        /// Compiles and launches an implicitly-grouped kernel.
        /// </summary>
        static void CompileAndLaunchImplicitlyGroupedKernel(Accelerator accelerator, int groupSize)
        {
            // Access the current backend for this device
            var backend = accelerator.GetBackend();

            // Resolve and compile method into a kernel
            var method = typeof(Program).GetMethod(nameof(MyKernel), BindingFlags.NonPublic | BindingFlags.Static);
            var entryPointDesc = EntryPointDescription.FromImplicitlyGroupedKernel(method);
            var compiledKernel = backend.Compile(entryPointDesc, default);
            // Info: If the current accelerator is a CudaAccelerator, we can cast the compiled kernel to a
            // PTXCompiledKernel in order to extract the PTX assembly code.

            // -------------------------------------------------------------------------------
            // Load the implicitly grouped kernel with the custom group size
            // Note that the kernel has to be disposed manually.
            using (var kernel = accelerator.LoadImplicitlyGroupedKernel(compiledKernel, groupSize))
            {
                var launcher = kernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, ArrayView<int>, int>>();
                // -------------------------------------------------------------------------------

                using (var buffer = accelerator.Allocate1D<int>(1024))
                {
                    // Launch buffer.Length many threads and pass a view to buffer.
                    // You can also use kernel.Launch; however, the generic launch method involves boxing.
                    launcher(
                        accelerator.DefaultStream,
                        (int)buffer.Length,
                        buffer.View,
                        42);

                    // Wait for the kernel to finish...
                    accelerator.Synchronize();

                    // Resolve and verify data
                    var data = buffer.GetAsArray1D();
                    for (int i = 0, e = data.Length; i < e; ++i)
                    {
                        if (data[i] != 42 + i)
                            Console.WriteLine($"Error at element location {i}: {data[i]} found");
                    }
                }

                accelerator.Synchronize();
            }
        }

        /// <summary>
        /// Compiles and launches an auto-grouped implicitly-grouped kernel.
        /// </summary>
        static void CompileAndLaunchAutoGroupedKernel(Accelerator accelerator)
        {
            // Access the current backend for this device
            var backend = accelerator.GetBackend();

            // Resolve and compile method into a kernel
            var method = typeof(Program).GetMethod(nameof(MyKernel), BindingFlags.NonPublic | BindingFlags.Static);
            var entryPointDesc = EntryPointDescription.FromImplicitlyGroupedKernel(method);
            var compiledKernel = backend.Compile(entryPointDesc, default);
            // Info: If the current accelerator is a CudaAccelerator, we can cast the compiled kernel to a
            // PTXCompiledKernel in order to extract the PTX assembly code.

            // -------------------------------------------------------------------------------
            // Load the implicitly grouped kernel with an automatically determined group size.
            // Note that the kernel has to be disposed manually.
            using (var kernel = accelerator.LoadAutoGroupedKernel(compiledKernel))
            {
                var launcher = kernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, ArrayView<int>, int>>();
                // -------------------------------------------------------------------------------

                using (var buffer = accelerator.Allocate1D<int>(1024))
                {
                    // Launch buffer.Length many threads and pass a view to buffer.
                    // You can also use kernel.Launch; however, the generic launch method involves boxing.
                    launcher(
                        accelerator.DefaultStream,
                        (int)buffer.Length,
                        buffer.View,
                        42);

                    // Wait for the kernel to finish...
                    accelerator.Synchronize();

                    // Resolve and verify data
                    var data = buffer.GetAsArray1D();
                    for (int i = 0, e = data.Length; i < e; ++i)
                    {
                        if (data[i] != 42 + i)
                            Console.WriteLine($"Error at element location {i}: {data[i]} found");
                    }
                }

                accelerator.Synchronize();

            }
        }

        /// <summary>
        /// Launches a simple 1D kernel using implicit and auto-grouping functionality.
        /// This sample demonstrates the creation of launcher delegates in order to avoid boxing.
        /// </summary>
        static void Main()
        {
            // Create main context
            using (var context = Context.CreateDefault())
            {
                // For each available device...
                foreach (var device in context)
                {
                    // Create accelerator for the given device
                    using (var accelerator = device.CreateAccelerator(context))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        // Compiles and launches an implicitly-grouped kernel with an automatically
                        // determined group size. The latter is determined either by ILGPU or
                        // the GPU driver. This is the most convenient way to launch kernels using ILGPU.
                        CompileAndLaunchAutoGroupedKernel(accelerator);

                        // Compiles and launches an implicitly-grouped kernel with a custom group
                        // size. Note that a group size less than the warp size can cause
                        // dramatic performance decreases since many lanes of a warp might remain
                        // unused.
                        CompileAndLaunchImplicitlyGroupedKernel(accelerator, accelerator.WarpSize);

                        // Compiles and launches an explicitly-grouped kernel with a custom group
                        // size.
                        CompileAndLaunchKernel(accelerator, accelerator.WarpSize);
                    }
                }
            }
        }
    }
}
