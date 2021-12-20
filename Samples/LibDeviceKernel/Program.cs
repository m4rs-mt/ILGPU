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
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using System;

namespace LibDeviceKernel
{
    class Program
    {
        /// <summary>
        /// A custom kernel using LibDevice functions.
        /// </summary>
        public static void KernelWithLibDevice(Index1D index, ArrayView<float> data)
        {
            data[index] = LibDevice.Cos(index);
        }

        static void Main()
        {
            // Create default context and enable LibDevice library
            using var context = Context.Create(builder => builder.Cuda().LibDevice());

            // For each available device...
            foreach (var device in context)
            {
                // Create the associated accelerator
                using var accelerator = device.CreateAccelerator(context);
                Console.WriteLine($"Performing operations on {accelerator}");

                using var buffer = accelerator.Allocate1D<float>(64);
                var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>>(
                    KernelWithLibDevice);
                kernel((int)buffer.Length, buffer.View);

                // Reads data from the GPU buffer into a new CPU array.
                // Implicitly calls accelerator.DefaultStream.Synchronize() to ensure
                // that the kernel and memory copy are completed first.
                var data = buffer.GetAsArray1D();
                for (int i = 0, e = data.Length; i < e; ++i)
                    Console.WriteLine($"Data[{i}] = {data[i]}");
            }
        }
    }
}
