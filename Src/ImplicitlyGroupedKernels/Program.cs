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
using System.Reflection;

namespace ImplicitlyGroupedKernels
{
    class Program
    {
        /// <summary>
        /// Implicitly-grouped kernels receive an index type (first parameter) of type:
        /// <see cref="Index"/>, <see cref="Index2"/> or <see cref="Index3"/>. These kernel types
        /// hide the underlying blocking/grouping semantics of a GPU and allow convenient
        /// kernel programming without having take grouping details into account. 
        /// The block or group size can be defined while loading a kernel via:
        /// - LoadImplicitlyGroupedKernel
        /// - LoadAutoGroupedKernel.
        /// 
        /// Note that you must not use warp-shuffle functionality within implicitly-grouped
        /// kernels since not all lanes of a warp are guaranteed to participate in the warp shuffle.
        /// Note that group-based synchronization should also not be used in this scope.
        /// Refer to explicitly grouped kernels to use these features.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        /// <param name="constant">A nice uniform constant.</param>
        static void MyKernel(
            Index index,               // The global thread index (1D in this case)
            ArrayView<int> dataView,   // A view to a chunk of memory (1D in this case)
            int constant)              // A uniform constant
        {
            dataView[index] = index + constant;
        }

        /// <summary>
        /// Compiles and launches an implicitly-grouped kernel.
        /// </summary>
        static void CompileAndLaunchImplicitlyGroupedKernel(Accelerator accelerator, int groupSize)
        {
            // Create a backend for this device
            using (var backend = accelerator.CreateBackend())
            {
                // Create a new compile unit using the created backend
                using (var compileUnit = accelerator.Context.CreateCompileUnit(backend))
                {
                    // Resolve and compile method into a kernel
                    var method = typeof(Program).GetMethod(nameof(MyKernel), BindingFlags.NonPublic | BindingFlags.Static);
                    var compiledKernel = backend.Compile(compileUnit, method);
                    // Info: use compiledKernel.GetBuffer() to retrieve the compiled kernel program data

                    // -------------------------------------------------------------------------------
                    // Load the implicitly grouped kernel with the custom group size
                    var kernel = accelerator.LoadImplicitlyGroupedKernel(compiledKernel, groupSize);
                    // -------------------------------------------------------------------------------

                    using (var buffer = accelerator.Allocate<int>(1024))
                    {
                        // Launch buffer.Length many threads and pass a view to buffer
                        kernel.Launch(buffer.Length, buffer.View, 42);

                        // Wait for the kernel to finish...
                        accelerator.Synchronize();

                        // Resolve and verify data
                        var data = buffer.GetAsArray();
                        for (int i = 0, e = data.Length; i < e; ++i)
                        {
                            if (data[i] != 42 + i)
                                Console.WriteLine($"Error at element location {i}: {data[i]} found");
                        }
                    }

                    accelerator.Synchronize();

                    kernel.Dispose();
                }
            }
        }

        /// <summary>
        /// Compiles and launches an auto-grouped implicitly-grouped kernel.
        /// </summary>
        static void CompileAndLaunchAutoGroupedKernel(Accelerator accelerator)
        {
            // Create a backend for this device
            using (var backend = accelerator.CreateBackend())
            {
                // Create a new compile unit using the created backend
                using (var compileUnit = accelerator.Context.CreateCompileUnit(backend))
                {
                    // Resolve and compile method into a kernel
                    var method = typeof(Program).GetMethod(nameof(MyKernel), BindingFlags.NonPublic | BindingFlags.Static);
                    var compiledKernel = backend.Compile(compileUnit, method);
                    // Info: use compiledKernel.GetBuffer() to retrieve the compiled kernel program data

                    // -------------------------------------------------------------------------------
                    // Load the implicitly grouped kernel with an automatically determined group size.
                    var kernel = accelerator.LoadAutoGroupedKernel(compiledKernel);
                    // -------------------------------------------------------------------------------

                    using (var buffer = accelerator.Allocate<int>(1024))
                    {
                        // Launch buffer.Length many threads and pass a view to buffer
                        kernel.Launch(buffer.Length, buffer.View, 42);

                        // Wait for the kernel to finish...
                        accelerator.Synchronize();

                        // Resolve and verify data
                        var data = buffer.GetAsArray();
                        for (int i = 0, e = data.Length; i < e; ++i)
                        {
                            if (data[i] != 42 + i)
                                Console.WriteLine($"Error at element location {i}: {data[i]} found");
                        }
                    }

                    accelerator.Synchronize();

                    kernel.Dispose();
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
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    // Create default accelerator for the given accelerator id
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
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

                        // Note that both kernel launches involve boxing. 
                        // This can be avoided by using typed launcher delegates, 
                        // as shown in the ImplicitlyGroupedKernelDelegates sample.
                    }
                }
            }
        }
    }
}
