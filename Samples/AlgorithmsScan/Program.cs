// ---------------------------------------------------------------------------------------
//                                    ILGPU Samples
//                        Copyright (c) 2021-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: Program.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

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
            // Create default context and enable algorithms library
            using var context = Context.Create(builder => builder.Default().EnableAlgorithms());
            // For each available accelerator...
            foreach (var device in context)
            {
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                using var sourceBuffer = accelerator.Allocate1D<int>(32);
                accelerator.Initialize(accelerator.DefaultStream, sourceBuffer.View, 2);

                // The parallel scan implementation needs temporary storage.
                // By default, every accelerator hosts a memory-buffer cache
                // for operations that require a temporary cache.

                // Computes an inclusive parallel scan
                using (var targetBuffer = accelerator.Allocate1D<int>(32))
                {
                    // Create a new inclusive scan using the AddInt32 scan operation
                    // Use the available scan operations in the namespace ILGPU.Algorithms.ScanReduceOperations.
                    var scan = accelerator.CreateScan<
                        int,
                        Stride1D.Dense,
                        Stride1D.Dense,
                        AddInt32>(ScanKind.Inclusive);

                    // Compute the required amount of temporary memory
                    var tempMemSize = accelerator.ComputeScanTempStorageSize<int>(targetBuffer.Length);
                    using (var tempBuffer = accelerator.Allocate1D<int>(tempMemSize))
                    {
                        scan(
                            accelerator.DefaultStream,
                            sourceBuffer.View,
                            targetBuffer.View,
                            tempBuffer.View);
                    }

                    Console.WriteLine("Inclusive Scan:");

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    var data = targetBuffer.GetAsArray1D();
                    for (int i = 0, e = data.Length; i < e; ++i)
                        Console.WriteLine($"Data[{i}] = {data[i]}");
                }

                // Computes an exclusive parallel scan
                using (var targetBuffer = accelerator.Allocate1D<int>(32))
                {
                    // Create a new exclusive scan using the AddInt32 scan operation
                    // Use the available scan operations in the namespace ILGPU.Algorithms.ScanReduceOperations.
                    var scan = accelerator.CreateScan<
                        int,
                        Stride1D.Dense,
                        Stride1D.Dense,
                        AddInt32>(ScanKind.Exclusive);

                    // Compute the required amount of temporary memory
                    var tempMemSize = accelerator.ComputeScanTempStorageSize<int>(targetBuffer.Length);
                    using (var tempBuffer = accelerator.Allocate1D<int>(tempMemSize))
                    {
                        scan(
                            accelerator.DefaultStream,
                            sourceBuffer.View,
                            targetBuffer.View,
                            tempBuffer.View);
                    }

                    Console.WriteLine("Exclusive Scan:");

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    var data = targetBuffer.GetAsArray1D();
                    for (int i = 0, e = data.Length; i < e; ++i)
                        Console.WriteLine($"Data[{i}] = {data[i]}");
                }

                // Creates a ScanProvider that hosts its own memory-buffer cache to allow
                // for parallel invocations of different operations that require
                // an extra cache.
                using (var scanProvider = accelerator.CreateScanProvider<int>(sourceBuffer.Length))
                {
                    var scanUsingScanProvider = scanProvider.CreateScan<
                        int,
                        Stride1D.Dense,
                        Stride1D.Dense,
                        AddInt32>(ScanKind.Inclusive);

                    // Please note that the create scan does not need additional temporary memory
                    // allocations as they will be automatically managed by the ScanProvider instance.
                    using var targetBuffer = accelerator.Allocate1D<int>(32);
                    scanUsingScanProvider(
                        accelerator.DefaultStream,
                        sourceBuffer.View,
                        targetBuffer.View);

                    // Reads data from the GPU buffer into a new CPU array.
                    // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                    // that the kernel and memory copy are completed first.
                    var data = targetBuffer.GetAsArray1D();
                    for (int i = 0, e = data.Length; i < e; ++i)
                        Console.WriteLine($"Data[{i}] = {data[i]}");
                }
            }
        }
    }
}
