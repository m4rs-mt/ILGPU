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
using ILGPU.Runtime;
using System;

namespace LightningScan
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
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        var sourceBuffer = accelerator.Allocate<int>(32);
                        accelerator.Initialize(sourceBuffer.View, 2);

                        // The parallel scan implementation needs temporary storage.
                        // By default, the lightning context hosts a memory-buffer cache
                        // for operations that require a temporary cache.

                        // Computes an inclusive parallel scan
                        using (var targetBuffer = accelerator.Allocate<int>(32))
                        {
                            // This overload uses the default accelerator stream and
                            // the default memory-buffer cache of the lightning context.
                            accelerator.InclusiveScan(sourceBuffer.View, targetBuffer.View);

                            Console.WriteLine("Inclusive Scan:");
                            accelerator.Synchronize();

                            var data = targetBuffer.GetAsArray();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {data[i]}");
                        }

                        // Computes an exclusive parallel scan
                        using (var targetBuffer = accelerator.Allocate<int>(32))
                        {
                            // This overload uses the default accelerator stream and
                            // the default memory-buffer cache of the lightning context.
                            accelerator.ExclusiveScan(sourceBuffer.View, targetBuffer.View);

                            Console.WriteLine("Exclusive Scan:");
                            accelerator.Synchronize();

                            var data = targetBuffer.GetAsArray();
                            for (int i = 0, e = data.Length; i < e; ++i)
                                Console.WriteLine($"Data[{i}] = {data[i]}");
                        }

                        // A ScanProvider that hosts its own memory-buffer cache to allow
                        // for parallel invocations of different operations that require
                        // an extra cache.
                        using (var scanProvider = accelerator.CreateScanProvider())
                        {
                            using (var targetBuffer = accelerator.Allocate<int>(32))
                            {
                                // This overload uses the default accelerator stream and
                                // the internal memory-buffer cache of the scan provider.
                                scanProvider.InclusiveScan(sourceBuffer.View, targetBuffer.View);

                                Console.WriteLine("Inclusive Scan:");
                                accelerator.Synchronize();

                                var data = targetBuffer.GetAsArray();
                                for (int i = 0, e = data.Length; i < e; ++i)
                                    Console.WriteLine($"Data[{i}] = {data[i]}");
                            }

                            using (var targetBuffer = accelerator.Allocate<int>(32))
                            {
                                // This overload uses the default accelerator stream and
                                // the internal memory-buffer cache of the scan provider.
                                scanProvider.ExclusiveScan(sourceBuffer.View, targetBuffer.View);

                                Console.WriteLine("Exclusive Scan:");
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
