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
using ILGPU.Algorithms;
using ILGPU.Algorithms.RadixSortOperations;
using ILGPU.Algorithms.Sequencers;
using ILGPU.Runtime;
using System;

namespace AlgorithmsRadixSort
{
    /// <summary>
    /// A custom sequencer to create inverse sequences from N-1 to 0.
    /// </summary>
    struct InverseInt32Sequencer : ISequencer<int>
    {
        /// <summary>
        /// Constructs a new inverse int32 sequencer
        /// </summary>
        /// <param name="maxValue">The maximum value to start with.</param>
        public InverseInt32Sequencer(int maxValue)
        {
            MaxValue = maxValue;
        }

        /// <summary>
        /// Returns the maximum (first) value.
        /// </summary>
        public int MaxValue { get; }

        /// <summary cref="ISequencer{T}.ComputeSequenceElement(Index1)"/>
        public int ComputeSequenceElement(Index1 sequenceIndex) => MaxValue - sequenceIndex;
    }

    class Program
    {
        static void Main()
        {
            using (var context = new Context())
            {
                // Enable algorithms library
                context.EnableAlgorithms();

                // For each available accelerator...
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    // Create the associated accelerator
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        // Allocate the source buffer that will be sorted later on.
                        var sourceBuffer = accelerator.Allocate<int>(32);
                        accelerator.Sequence(
                            accelerator.DefaultStream,
                            sourceBuffer.View,
                            new InverseInt32Sequencer(sourceBuffer.Length));

                        // The parallel scan implementation needs temporary storage.
                        // By default, every accelerator hosts a memory-buffer cache
                        // for operations that require a temporary cache.

                        // Create a new radix sort instance using a descending int sorting.
                        var radixSort = accelerator.CreateRadixSort<int, AscendingInt32>();

                        // Compute the required amount of temporary memory
                        var tempMemSize = accelerator.ComputeRadixSortTempStorageSize<int, AscendingInt32>(sourceBuffer.Length);
                        using (var tempBuffer = accelerator.Allocate<int>(tempMemSize))
                        {
                            // Performs a descending radix-sort operation
                            radixSort(
                                accelerator.DefaultStream,
                                sourceBuffer.View,
                                tempBuffer.View);
                        }

                        Console.WriteLine("Ascending RadixSort:");
                        accelerator.Synchronize();

                        var data = sourceBuffer.GetAsArray();
                        for (int i = 0, e = data.Length; i < e; ++i)
                            Console.WriteLine($"Data[{i}] = {data[i]}");

                        // Creates a RadixSortProvider that hosts its own memory-buffer cache to allow
                        // for parallel invocations of different operations that require
                        // an extra cache.
                        using (var radixSortProvider = accelerator.CreateRadixSortProvider())
                        {
                            // Create a new radix sort instance using an ascending int sorting.
                            var radixSortUsingSortProvider = radixSortProvider.CreateRadixSort<int, DescendingInt32>();

                            // Performs an ascending radix-sort operation
                            radixSortUsingSortProvider(
                                accelerator.DefaultStream,
                                sourceBuffer.View);

                            Console.WriteLine("Descending RadixSort:");
                            accelerator.Synchronize();

                            data = sourceBuffer.GetAsArray();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {data[i]}");
                        }

                        sourceBuffer.Dispose();
                    }
                }
            }
        }
    }
}
