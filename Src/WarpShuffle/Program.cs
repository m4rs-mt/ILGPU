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
using ILGPU.ShuffleOperations;
using System;
using System.Runtime.CompilerServices;
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
        /// A custom shuffle-down functionality for longs.
        /// </summary>
        readonly struct ShuffleDownInt64 : IShuffleDown<long>
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
            public long ShuffleDown(long variable, int delta)
            {
                var source = Unsafe.As<long, Int2>(ref variable);
                var result = new Int2()
                {
                    X = Warp.ShuffleDown(source.X, delta),
                    Y = Warp.ShuffleDown(source.Y, delta),
                };
                return Unsafe.As<Int2, long>(ref result);
            }
        }

        /// <summary>
        /// Explicitly grouped kernels receive an index type (first parameter) of type:
        /// <see cref="GroupedIndex"/>, <see cref="GroupedIndex2"/> or <see cref="GroupedIndex3"/>.
        /// Note that you can use warp-shuffle functionality only within 
        /// explicitly-grouped kernels.
        /// </summary>
        /// <typeparam name="TShuffleOperation">The type of the shuffle operation.</typeparam>
        /// <param name="index">The current thread index.</param>
        /// <param name="dataView">The view pointing to our memory buffer.</param>
        static void ShuffleDownKernel<TShuffleOperation>(
            GroupedIndex index,               // The grouped thread index (1D in this case)
            ArrayView<long> dataView)          // A view to a chunk of memory (1D in this case)
            where TShuffleOperation : struct, IShuffleDown<long>
        {
            // Compute the global 1D index for accessing the data view
            var globalIndex = index.ComputeGlobalIndex();

            // Use custom shuffle-down functionality to shuffle the 
            // given value by a delta of 2 lanes
            long value = index.GroupIdx;
            TShuffleOperation shuffleOperation = default;
            value = shuffleOperation.ShuffleDown(value, 2);

            dataView[globalIndex] = value;
        }


        /// <summary>
        /// Launches a simple 1D kernel using warp intrinsics.
        /// </summary>
        static void Main()
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

                        var dimension = new GroupedIndex(1, accelerator.WarpSize);
                        using (var dataTarget = accelerator.Allocate<int>(accelerator.WarpSize))
                        {
                            // Load the explicitly grouped kernel
                            var shuffleDownKernel = accelerator.LoadStreamKernel<GroupedIndex, ArrayView<int>>(ShuffleDownKernel);
                            dataTarget.MemSetToZero();

                            shuffleDownKernel(dimension, dataTarget.View);
                            accelerator.Synchronize();

                            Console.WriteLine("Shuffle-down kernel");
                            var target = dataTarget.GetAsArray();
                            for (int i = 0, e = target.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {target[i]}");
                        }

                        using (var dataTarget = accelerator.Allocate<long>(accelerator.WarpSize))
                        {
                            // Load the explicitly grouped kernel
                            var reduceKernel = accelerator.LoadStreamKernel<GroupedIndex, ArrayView<long>>(
                                ShuffleDownKernel<ShuffleDownInt64>);
                            dataTarget.MemSetToZero();

                            reduceKernel(dimension, dataTarget.View);
                            accelerator.Synchronize();

                            Console.WriteLine("Generic shuffle-down kernel");
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
