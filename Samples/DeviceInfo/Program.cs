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
            Console.WriteLine($"MaxThreadsPerGroup: {accelerator.MaxNumThreadsPerGroup}");
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
        static void Main()
        {
            // Create main context
            using (var context = Context.CreateDefault())
            {
                // For each available device...
                foreach (var device in context)
                {
                    // Create accelerator for the given device.
                    // Note that all accelerators have to be disposed before the global context is disposed
                    using var accelerator = device.CreateAccelerator(context);
                    Console.WriteLine($"Accelerator: {device.AcceleratorType}, {accelerator.Name}");
                    PrintAcceleratorInfo(accelerator);
                    Console.WriteLine();
                }
            }

            // CPU accelerators can also be created manually with custom settings.
            // The following code snippet creates a CPU accelerator with 4 threads
            // and highest thread priority.
            using (var context = Context.Create(builder => builder.CPU(new CPUDevice(4, 1, 1))))
            {
                using var accelerator = context.CreateCPUAccelerator(0, CPUAcceleratorMode.Auto, ThreadPriority.Highest);
                PrintAcceleratorInfo(accelerator);
            }
        }
    }
}
