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
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using System;
using System.Diagnostics;

namespace CudaIPC.Host
{
    class Program
    {
        /// <summary>
        /// Exports memory for other processes using CUDA IPC.
        /// </summary>
        static void Main()
        {
            // Create main context
            using var context = Context.CreateDefault();

            // For each available CUDA device...
            foreach (var device in context.GetCudaDevices())
            {
                // Create accelerator for the given device
                using CudaAccelerator accelerator = device.CreateCudaAccelerator(context);

                if (!device.HasIpcSupport)
                {
                    Console.WriteLine($"{device.Name} does not support inter process comunication!");
                    continue;
                }

                using MemoryBuffer1D<int, Stride1D.Dense> buffer = accelerator.Allocate1D<int>(64);

                // Export memory for other processes
                CudaIpcMemHandle cudaIpcMemHandle = accelerator.GetIpcMemoryHandle(buffer);
                string handleHex = Convert.ToHexString(cudaIpcMemHandle);

                // Launch CudaIPC.Child
                var arguments = $"{device.DeviceId} {handleHex} {buffer.Length}";
                Console.WriteLine(arguments);
                var childProcess = Process.Start(
                    OperatingSystem.IsWindows() ?
                    "CudaIPC.Child.exe" : "CudaIPC.Child",
                    arguments
                    );
                childProcess?.WaitForExit();

                // Gets changed buffer data onto the CPU and print it.
                int[] bufferData = buffer.GetAsArray1D();
                Console.WriteLine(String.Join(" ", bufferData));
            }
        }
    }
}
