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
using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Runtime;
using System;

namespace AlgorithmsScan
{
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
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        var sourceBuffer = accelerator.Allocate<int>(32);
                        accelerator.Initialize(accelerator.DefaultStream,
                            sourceBuffer.View, 2);

                        // The parallel scan implementation needs temporary storage.
                        // By default, every accelerator hosts a memory-buffer cache
                        // for operations that require a temporary cache.

                        // Computes an inclusive parallel scan
                        using (var targetBuffer = accelerator.Allocate<int>(32))
                        {
                            // Create a new inclusive scan using the AddInt32 scan operation
                            // Use the available scan operations in the namespace ILGPU.Algorithms.ScanReduceOperations.
                            var scan = accelerator.CreateInclusiveScan<int, AddInt32>();

                            // Compute the required amount of temporary memory
                            var tempMemSize =
                                accelerator.ComputeScanTempStorageSize<int>(targetBuffer
                                    .Length);
                            using (var tempBuffer =
                                accelerator.Allocate<int>(tempMemSize))
                            {
                                scan(
                                    accelerator.DefaultStream,
                                    sourceBuffer.View,
                                    targetBuffer.View,
                                    tempBuffer.View);
                            }

                            Console.WriteLine("Inclusive Scan:");
                            accelerator.Synchronize();

                            var data = targetBuffer.GetAsArray();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {data[i]}");
                        }

                        // Computes an exclusive parallel scan
                        using (var targetBuffer = accelerator.Allocate<int>(32))
                        {
                            // Create a new exclusive scan using the AddInt32 scan operation
                            // Use the available scan operations in the namespace ILGPU.Algorithms.ScanReduceOperations.
                            var scan = accelerator.CreateExclusiveScan<int, AddInt32>();

                            // Compute the required amount of temporary memory
                            var tempMemSize =
                                accelerator.ComputeScanTempStorageSize<int>(targetBuffer
                                    .Length);
                            using (var tempBuffer =
                                accelerator.Allocate<int>(tempMemSize))
                            {
                                scan(
                                    accelerator.DefaultStream,
                                    sourceBuffer.View,
                                    targetBuffer.View,
                                    tempBuffer.View);
                            }

                            Console.WriteLine("Exclusive Scan:");
                            accelerator.Synchronize();

                            var data = targetBuffer.GetAsArray();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {data[i]}");
                        }

                        // Creates a ScanProvider that hosts its own memory-buffer cache to allow
                        // for parallel invocations of different operations that require
                        // an extra cache.
                        using (var scanProvider = accelerator.CreateScanProvider())
                        {
                            var scanUsingScanProvider =
                                scanProvider.CreateInclusiveScan<int, AddInt32>();

                            // Please note that the create scan does not need additional temporary memory
                            // allocations as they will be automatically managed by the ScanProvider instance.
                            using (var targetBuffer = accelerator.Allocate<int>(32))
                            {
                                scanUsingScanProvider(
                                    accelerator.DefaultStream,
                                    sourceBuffer.View,
                                    targetBuffer.View);

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
