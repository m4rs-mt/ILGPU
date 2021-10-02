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
        public int ComputeSequenceElement(LongIndex1D sequenceIndex) => MaxValue - sequenceIndex.ToIntIndex();
    }

    class Program
    {
        static void Main()
        {
            // Create default context and enable algorithms library
            using var context = Context.Create(builder => builder.Default().EnableAlgorithms());

            // For each available device...
            foreach (var device in context)
            {
                // Create the associated accelerator
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                // Allocate the source buffer that will be sorted later on.
                var sourceBuffer = accelerator.Allocate1D<int>(32);
                accelerator.Sequence(
                    accelerator.DefaultStream,
                    sourceBuffer.View,
                    new InverseInt32Sequencer((int)sourceBuffer.Length));

                // The parallel scan implementation needs temporary storage.
                // By default, every accelerator hosts a memory-buffer cache
                // for operations that require a temporary cache.

                // Create a new radix sort instance using a descending int sorting.
                var radixSort = accelerator.CreateRadixSort<int, Stride1D.Dense, AscendingInt32>();

                // Compute the required amount of temporary memory
                var tempMemSize = accelerator.ComputeRadixSortTempStorageSize<int, AscendingInt32>((Index1D)sourceBuffer.Length);
                using (var tempBuffer = accelerator.Allocate1D<int>(tempMemSize))
                {
                    // Performs a descending radix-sort operation
                    radixSort(
                        accelerator.DefaultStream,
                        sourceBuffer.View,
                        tempBuffer.View);
                }

                Console.WriteLine("Ascending RadixSort:");

                // Reads data from the GPU buffer into a new CPU array.
                // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                // that the kernel and memory copy are completed first.
                var data = sourceBuffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"Data[{i}] = {data[i]}");

                // Creates a RadixSortProvider that hosts its own memory-buffer cache to allow
                // for parallel invocations of different operations that require
                // an extra cache.
                using (var radixSortProvider = accelerator.CreateRadixSortProvider<int, DescendingInt32>((Index1D)sourceBuffer.Length))
                {
                    // Create a new radix sort instance using an ascending int sorting.
                    var radixSortUsingSortProvider = radixSortProvider.CreateRadixSort<int, Stride1D.Dense, DescendingInt32>();

                    // Performs an ascending radix-sort operation
                    radixSortUsingSortProvider(
                        accelerator.DefaultStream,
                        sourceBuffer.View);

                    Console.WriteLine("Descending RadixSort:");

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    data = sourceBuffer.GetAsArray1D();
                    for (int i = 0, e = data.Length; i < e; ++i)
                        Console.WriteLine($"Data[{i}] = {data[i]}");
                }

                sourceBuffer.Dispose();
            }
        }
    }
}
