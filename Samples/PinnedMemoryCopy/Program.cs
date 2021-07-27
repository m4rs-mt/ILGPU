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
using ILGPU.Runtime.CPU;
using System;

namespace PinnedMemoryCopy
{
    class Program
    {
        /// <summary>
        /// Uses the CPU accelerator to allocate pinned chunks of memory in CPU host memory.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="dataSize">The number of elements to copy.</param>
        static void PerformPinnedCopyToCPUAccelerator(Accelerator accelerator,
            int dataSize)
        {
            using (var cpuAccl = new CPUAccelerator(accelerator.Context))
            {
                // All buffers allocated through the CPUAccelerator class are automatically pinned
                // in memory to enable async memory transfers via AcceleratorStreams
                using (var pinnedCPUBuffer = cpuAccl.Allocate<int>(dataSize))
                {
                    var stream = accelerator.DefaultStream;

                    // Allocate buffer on this device
                    using (var bufferOnGPU =
                        accelerator.Allocate<int>(pinnedCPUBuffer.Length))
                    {
                        // Use an accelerator stream to perform an async copy operation.
                        // Note that you should use the CopyTo function from the associated GPU
                        // buffer to perform the copy operation using the associated accelerator stream.
                        bufferOnGPU.CopyTo(stream, pinnedCPUBuffer, 0);

                        //
                        // Perform other operations...
                        //

                        // Wait for the copy operation to finish
                        stream.Synchronize();
                    }
                }
            }
        }

        /// <summary>
        /// Uses the exchange buffer to allocate pinned chunks of memory in CPU host memory.
        /// </summary>
        /// <param name="accelerator">The current accelerator.</param>
        /// <param name="dataSize">The number of elements to copy.</param>
        static void PerformPinnedCopyToExchangeBuffer(Accelerator accelerator,
            int dataSize)
        {
            // Allocate an exchange buffer that stores a buffer on the associated parent
            // device and a buffer of the same size in pinned CPU host memory
            using (var buffer = accelerator.AllocateExchangeBuffer<int>(dataSize))
            {
                // Access CPU copy
                buffer[0] = 42;
                buffer[1] = 23;

                var stream = accelerator.DefaultStream;

                // Allocate buffer on this device
                using (var bufferOnGPU = accelerator.Allocate<int>(buffer.Length))
                {
                    // Use CopyToAccelerator to copy information to the target device
                    // -> buffer.CopyToAccelerator();
                    // Note: use an accelerator stream to perform async copy operations
                    buffer.CopyToAccelerator(stream);

                    //
                    // Perform other operations...
                    //

                    stream.Synchronize();

                    // Use CopyFrom to copy information from the target device
                    // -> buffer.CopyFromAccelerator();
                    // Note: use an accelerator stream to perform async copy operations
                    buffer.CopyFromAccelerator(stream);

                    //
                    // Perform other operations...
                    //

                    stream.Synchronize();

                    // Access the updated CPU data
                    Console.WriteLine($"Data: {buffer[0]}, {buffer[1]}");
                }
            }
        }

        /// <summary>
        /// Demonstrates async copy operations using the <see cref="CPUAccelerator"/> class to allocate
        /// pinned CPU memory.
        /// </summary>
        static void Main()
        {
            const int DataSize = 1024;

            using (var context = new Context())
            {
                // For each available accelerator...
                foreach (var acceleratorId in Accelerator.Accelerators)
                {
                    // Create default accelerator for the given accelerator id
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"Performing operations on {accelerator}");

                        PerformPinnedCopyToCPUAccelerator(accelerator, DataSize);
                        PerformPinnedCopyToExchangeBuffer(accelerator, DataSize);
                    }
                }
            }
        }
    }
}
