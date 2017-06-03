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
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using System;
using System.Threading;

namespace DeviceInfo
{
    class Program
    {
        /// <summary>
        /// Prints information on the given accelerator.
        /// </summary>
        /// <param name="accelerator">The target accelerator.</param>
        static void PrintAcceleratorInfo(Accelerator accelerator)
        {
            Console.WriteLine($"Name: {accelerator.Name}");
            Console.WriteLine($"MemorySize: {accelerator.MemorySize}");
            Console.WriteLine($"MaxThreadsPerGroup: {accelerator.MaxThreadsPerGroup}");
            Console.WriteLine($"MaxSharedMemoryPerGroup: {accelerator.MaxSharedMemoryPerGroup}");
            Console.WriteLine($"MaxGridSize: {accelerator.MaxGridSize}");
            Console.WriteLine($"MaxConstantMemory: {accelerator.MaxConstantMemory}");
            Console.WriteLine($"WarpSize: {accelerator.WarpSize}");
            Console.WriteLine($"NumMultiprocessors: {accelerator.NumMultiprocessors}");
        }

        /// <summary>
        /// Detects all available accelerators and prints device information about each
        /// of them on the command line.
        /// </summary>
        static void Main(string[] args)
        {
            // Create main context
            using (var context = new Context())
            {
                // For each available accelerator...
                foreach(var acceleratorId in Accelerator.Accelerators)
                {
                    // Create default accelerator for the given accelerator id.
                    // Note that all accelerators have to be disposed before the global context is disposed
                    using (var accelerator = Accelerator.Create(context, acceleratorId))
                    {
                        Console.WriteLine($"AcceleratorId: {acceleratorId.AcceleratorType}, {acceleratorId.DeviceId}");
                        PrintAcceleratorInfo(accelerator);
                        Console.WriteLine();
                    }
                }

                // Accelerators can also be created manually with custom settings.
                // The following code snippet creates a CPU accelerator with 4 threads
                // and a warp size of 2 threads per warp and the highest thread priority.
                using (var accelerator = new CPUAccelerator(context, 2, 2, ThreadPriority.Highest))
                {
                    PrintAcceleratorInfo(accelerator);
                }
            }
        }
    }
}
