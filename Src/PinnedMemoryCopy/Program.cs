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
        /// Performs an async copy operation to CPU memory.
        /// </summary>
        /// <param name="context">The current ILGPU context.</param>
        /// <param name="cpuBuffer">The target CPU memory buffer to copy to.</param>
        static void PerformAsyncCopy(Context context, MemoryBuffer<int> cpuBuffer)
        {
            // For each available accelerator...
            foreach (var acceleratorId in Accelerator.Accelerators)
            {
                // Create default accelerator for the given accelerator id
                using (var accelerator = Accelerator.Create(context, acceleratorId))
                {
                    Console.WriteLine($"Performing operations on {accelerator}");
                    var stream = accelerator.DefaultStream;

                    // Allocate buffer on this device
                    using (var bufferOnGPU = accelerator.Allocate<int>(cpuBuffer.Length))
                    {
                        // Use an accelerator stream to perform an async copy operation.
                        // Note that you should use the CopyTo function from the associated GPU
                        // buffer to perform the copy operation using the associated accelerator stream.
                        bufferOnGPU.CopyTo(stream, cpuBuffer, 0);

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
        /// Demonstrates async copy operations using the <see cref="CPUAccelerator"/> class to allocate
        /// pinned CPU memory.
        /// </summary>
        static void Main()
        {
            const int DataSize = 1024;

            using (var context = new Context())
            {
                // Use the CPU accelerator to allocate pinned chunks of memory in CPU host memory
                using (var cpuAccl = new CPUAccelerator(context))
                {
                    // All buffers allocated through the CPUAccelerator class are automatically pinned
                    // in memory to enable async memory transfers via AcceleratorStreams
                    using (var pinnedCPUBuffer = cpuAccl.Allocate<int>(DataSize))
                        PerformAsyncCopy(context, pinnedCPUBuffer);
                }
            }
        }
    }
}
