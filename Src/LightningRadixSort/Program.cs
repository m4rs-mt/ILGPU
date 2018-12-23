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
using ILGPU.Lightning;
using ILGPU.Lightning.Sequencers;
using ILGPU.Runtime;
using System;

namespace LightningRadixSort
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var context = new Context())
            {
                // For each available accelerator...
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    // A lightning context encapsulates an ILGPU accelerator
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        var sourceBuffer = accelerator.Allocate<uint>(32);
                        accelerator.Sequence(sourceBuffer.View, new UInt32Sequencer());

                        // The parallel radix-sort implementation needs temporary storage.
                        // By default, the lightning context hosts a memory-buffer cache
                        // for operations that require a temporary cache.

                        // Performs a descending radix-sort operation
                        using (var targetBuffer = accelerator.Allocate<uint>(32))
                        {
                            // This overload uses the default accelerator stream and
                            // the default memory-buffer cache of the lightning context.
                            accelerator.DescendingRadixSort(sourceBuffer.View, targetBuffer.View);

                            Console.WriteLine("Descending RadixSort:");
                            accelerator.Synchronize();

                            var data = targetBuffer.GetAsArray();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {data[i]}");
                        }

                        // Performs an ascending radix-sort operation
                        using (var targetBuffer = accelerator.Allocate<uint>(32))
                        {
                            // This overload uses the default accelerator stream and
                            // the default memory-buffer cache of the lightning context.
                            accelerator.RadixSort(sourceBuffer.View, targetBuffer.View);

                            Console.WriteLine("RadixSort:");
                            accelerator.Synchronize();

                            var data = targetBuffer.GetAsArray();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {data[i]}");
                        }

                        // A RadixSortProvider hosts its own memory-buffer cach to allow 
                        // for parallel invocations of different operations that require
                        // an extra cache.
                        using (var radixSortProvider = accelerator.CreateRadixSortProvider())
                        {
                            using (var targetBuffer = accelerator.Allocate<uint>(32))
                            {
                                // This overload uses the default accelerator stream and
                                // the internal memory-buffer cache of the scan provider.
                                accelerator.DescendingRadixSort(sourceBuffer.View, targetBuffer.View);

                                Console.WriteLine("Descending RadixSort:");
                                accelerator.Synchronize();

                                var data = targetBuffer.GetAsArray();
                                for (int i = 0, e = data.Length; i < e; ++i)
                                    Console.WriteLine($"Data[{i}] = {data[i]}");
                            }

                            using (var targetBuffer = accelerator.Allocate<uint>(32))
                            {
                                // This overload uses the default accelerator stream and
                                // the internal memory-buffer cache of the scan provider.
                                accelerator.RadixSort(sourceBuffer.View, targetBuffer.View);

                                Console.WriteLine("RadixSort:");
                                accelerator.Synchronize();

                                var data = targetBuffer.GetAsArray();
                                for (int i = 0, e = data.Length; i < e; ++i)
                                    Console.WriteLine($"Data[{i}] = {data[i]}");
                            }
                        }

                        sourceBuffer.Dispose();
                    }
                }
            }
        }
    }
}
