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
using ILGPU.ReductionOperations;
using ILGPU.Runtime;
using ILGPU.ShuffleOperations;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WarpShuffle
{
    class Program
    {
        /// <summary>
        /// Explicitly grouped kernels receive an index type (first parameter) of type:
        /// <see cref="GroupedIndex"/>, <see cref="GroupedIndex2"/> or <see cref="GroupedIndex3"/>.
        /// Note that you can use warp-shuffle functionality only within 
        /// explicitly-grouped kernels.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void ShuffleDownKernel(
            GroupedIndex index,               // The grouped thread index (1D in this case)
            ArrayView<int> dataView)          // A view to a chunk of memory (1D in this case)
        {
            // Compute the global 1D index for accessing the data view
            var globalIndex = index.ComputeGlobalIndex();

            // Use native shuffle-down functionality to shuffle the 
            // given value by a delta of 2 lanes
            int value = index.GroupIdx;
            value = Warp.ShuffleDown(value, 2);

            dataView[globalIndex] = value;
        }

        /// <summary>
        /// Demonstrates a pre-defined warp-reduction functionality.
        /// </summary>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void ReduceKernel(
            GroupedIndex index,               // The grouped thread index (1D in this case)
            ArrayView<int> dataView)          // A view to a chunk of memory (1D in this case)
        {
            // Compute the global 1D index for accessing the data view
            var globalIndex = index.ComputeGlobalIndex();

            // Use native warp-reduce functionality to reduce all given
            // values in the scope of a single warp. Note that only
            // the first lane of a warp will contain the reduced value.
            // If all lanes should receive the reduced value,
            // use the Warp.AllReduce<...> function.
            var value = Warp.Reduce(
                1,
                new ShuffleDownInt32(),
                new AddInt32());

            dataView[globalIndex] = value;
        }

        /// <summary>
        /// A custom shuffle-down functionality for longs.
        /// </summary>
        struct ShuffleDownInt64 : IShuffleDown<long>
        {
            /// <summary>
            /// Meta structure for representing a long.
            /// </summary>
            [StructLayout(LayoutKind.Sequential)]
            struct Int2
            {
                /// <summary>
                /// Represents the first part of a long.
                /// </summary>
                public int X;

                /// <summary>
                /// Represents the second part of a long.
                /// </summary>
                public int Y;
            }

            /// <summary>
            /// Performs a shuffle operation. It returns the value of the variable
            /// in the context of the lane with the id current lane + delta.
            /// </summary>
            /// <param name="variable">The source variable to shuffle.</param>
            /// <param name="delta">The delta to add to the current lane.</param>
            /// <returns>The value of the variable in the scope of the desired lane.</returns>
            public unsafe long ShuffleDown(long variable, int delta)
            {
                var result = new Int2();
                var ptr = (Int2*)&variable;
                result.X = Warp.ShuffleDown(ptr->X, delta);
                result.Y = Warp.ShuffleDown(ptr->Y, delta);
                return *(long*)&result;
            }
        }

        /// <summary>
        /// Demonstrates a custom warp-reduction functionality.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="dataView"></param>
        static void CustomShuffleReduceKernel(
            GroupedIndex index,               // The grouped thread index (1D in this case)
            ArrayView<long> dataView)         // A view to a chunk of memory (1D in this case)
        {
            // Compute the global 1D index for accessing the data view
            var globalIndex = index.ComputeGlobalIndex();

            // Use native warp-reduce functionality to reduce all given
            // values in the scope of a single warp. Note that only
            // the first lane of a warp will contain the reduced value.
            // If all lanes should receive the reduced value,
            // use the Warp.AllReduce<...> function.
            var value = Warp.Reduce(
                1L,
                new ShuffleDownInt64(),
                new AddInt64());

            dataView[globalIndex] = value;
        }

        /// <summary>
        /// Compiles and launches an explicltly-grouped kernel.
        /// </summary>
        static void CompileAndLaunchKernel(
            Accelerator accelerator,
            MethodInfo method,
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

                    launcher(kernel, new GroupedIndex(1, accelerator.WarpSize));

                    accelerator.Synchronize();
                    kernel.Dispose();
                }
            }
        }

        /// <summary>
        /// Launches a simple 1D kernel using warp intrinsics.
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

                        using (var dataTarget = accelerator.Allocate<int>(accelerator.WarpSize))
                        {
                            CompileAndLaunchKernel(
                                accelerator,
                                typeof(Program).GetMethod(nameof(ShuffleDownKernel), BindingFlags.NonPublic | BindingFlags.Static),
                                (kernel, dimension) =>
                                {
                                    dataTarget.MemSetToZero();

                                    kernel.Launch(dimension, dataTarget.View);

                                    accelerator.Synchronize();

                                    Console.WriteLine("Shuffle-down kernel");
                                    var target = dataTarget.GetAsArray();
                                    for (int i = 0, e = target.Length; i < e; ++i)
                                        Console.WriteLine($"Data[{i}] = {target[i]}");
                                });


                            CompileAndLaunchKernel(
                                accelerator,
                                typeof(Program).GetMethod(nameof(ReduceKernel), BindingFlags.NonPublic | BindingFlags.Static),
                                (kernel, dimension) =>
                                {
                                    dataTarget.MemSetToZero();

                                    kernel.Launch(dimension, dataTarget.View);

                                    accelerator.Synchronize();

                                    Console.WriteLine("Reduce kernel");
                                    var target = dataTarget.GetAsArray();
                                    for (int i = 0, e = target.Length; i < e; ++i)
                                        Console.WriteLine($"Data[{i}] = {target[i]}");
                                });
                        }

                        using (var dataTarget = accelerator.Allocate<long>(accelerator.WarpSize))
                        {
                            CompileAndLaunchKernel(
                                accelerator,
                                typeof(Program).GetMethod(nameof(CustomShuffleReduceKernel), BindingFlags.NonPublic | BindingFlags.Static),
                                (kernel, dimension) =>
                                {
                                    dataTarget.MemSetToZero();

                                    kernel.Launch(dimension, dataTarget.View);

                                    accelerator.Synchronize();

                                    Console.WriteLine("Custom shuffle-reduce kernel");
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
